// Copyright © 2026 Racpast. All Rights Reserved.
//
// This file is part of SNIBypassGUI, a proprietary software project.
//
// NOTICE: All information contained herein is, and remains the property of
// Racpast. The intellectual and technical concepts contained herein are
// proprietary to Racpast and are protected by copyright law and international
// treaties. Dissemination of this information or reproduction of this material
// is strictly forbidden unless prior written permission is obtained from Racpast.
//
// Unauthorized copying, modification, distribution, or use of this file,
// via any medium, is strictly prohibited.
//
// For licensing inquiries: snibypassgui@gmail.com or racpast@gmail.com
//
// See the license file in the project root for full terms and conditions.

#include "platform/child_output.h"

#include <process.h>

#include <cstddef>
#include <memory>
#include <new>
#include <utility>

#include "app/logging.h"

namespace Process {
namespace {

// UTF-8 first, and strictly: without MB_ERR_INVALID_CHARS the conversion turns any
// bytes into replacement characters and still reports success, so the fallback below
// would never run.
std::wstring DecodeLine(const std::string& bytes) {
    if (bytes.empty()) return {};

    const int size = static_cast<int>(bytes.size());
    int n = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, bytes.data(), size, nullptr, 0);
    if (n > 0) {
        std::wstring wide(static_cast<size_t>(n), L'\0');
        MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, bytes.data(), size, wide.data(), n);
        return wide;
    }

    n = MultiByteToWideChar(CP_ACP, 0, bytes.data(), size, nullptr, 0);
    if (n <= 0) return {};
    std::wstring wide(static_cast<size_t>(n), L'\0');
    MultiByteToWideChar(CP_ACP, 0, bytes.data(), size, wide.data(), n);
    return wide;
}

// Only write end inheritable (prevents child keeping pipe open after exit).
// Raw handles: caller handles ownership (allocation may fail after this succeeds).
bool MakePipe(HANDLE& readOut, HANDLE& writeOut) {
    SECURITY_ATTRIBUTES sa = {};
    sa.nLength = sizeof(sa);
    sa.bInheritHandle = TRUE;

    HANDLE read = nullptr;
    HANDLE write = nullptr;
    if (!CreatePipe(&read, &write, &sa, 0)) return false;
    if (!SetHandleInformation(read, HANDLE_FLAG_INHERIT, 0)) {
        CloseHandle(read);
        CloseHandle(write);
        return false;
    }
    readOut = read;
    writeOut = write;
    return true;
}

constexpr size_t kReadChunk = 4096;

// Log from noexcept context (absorbs allocation failures to prevent std::terminate).
void WarnNoThrow(Channel channel, const wchar_t* message) {
    try {
        LOGW(std::wstring(channel == Channel::Stdout ? L"stdout" : L"stderr") + L" of a " +
             message);
    } catch (...) {  // NOLINT(bugprone-empty-catch)
    }
}

}  // namespace

// One channel's shared state, ref-counted for safe abandonment in bounded Drain.
// Owns read pipe handle and gate event. Reader thread may outlive OutputCapture.
struct OutputCapture::Reader {
    Owned read;
    Owned seal;  // duplicated from the capture's gate, so parking does not need `this`
    Channel channel = Channel::Stdout;
    std::string pending;  // bytes read but not yet ended by a newline
    OutputSink sink;
};

// One channel's thread (_beginthreadex returns handle immediately, unlike std::thread).
// Owning raw handle makes abandonment safe (closing handle = detach, no exceptions).
struct OutputCapture::Stream {
    std::shared_ptr<Reader> reader;
    Owned thread;
};

OutputCapture::OutputCapture() = default;

OutputCapture::OutputCapture(const OutputSink& sink) {
    if (!sink) return;  // not asked for: no pipes, no threads
    if (Build(sink)) live_ = true;
}

// Build all resources before child launch. Failure here prevents silent output loss.
bool OutputCapture::Build(const OutputSink& sink) {
    // Manual reset: this is a starting gate, not a count of arrivals.
    sealEvent_.reset(CreateEventW(nullptr, TRUE, FALSE, nullptr));
    if (!sealEvent_.get()) return false;

    const auto channel = [this, &sink](Channel which, bool isStdout) -> bool {
        Owned& write = isStdout ? stdoutWrite_ : stderrWrite_;
        std::shared_ptr<Stream>& slot = isStdout ? stdoutStream_ : stderrStream_;

        std::shared_ptr<Stream> stream = std::make_shared<Stream>();
        std::shared_ptr<Reader> reader = std::make_shared<Reader>();
        reader->channel = which;
        reader->sink = sink;

        HANDLE readRaw = nullptr;
        HANDLE writeRaw = nullptr;
        if (!MakePipe(readRaw, writeRaw)) return false;
        reader->read.reset(readRaw);
        write.reset(writeRaw);

        // The reader parks on its own handle to the gate rather than on the capture's,
        // so it never has to be handed a reference to an object that may move or die
        // before it wakes.
        HANDLE seal = nullptr;
        if (!DuplicateHandle(GetCurrentProcess(), sealEvent_.get(), GetCurrentProcess(), &seal,
                             0, FALSE, DUPLICATE_SAME_ACCESS))
            return false;
        reader->seal.reset(seal);

        stream->reader = std::move(reader);
        slot = std::move(stream);
        return true;
    };

    if (!channel(Channel::Stdout, true)) return false;
    if (!channel(Channel::Stderr, false)) return false;

    // Last, and either both run or Build fails with neither: a capture that cannot read
    // is not a capture, and the caller has a launch to refuse.
    if (!StartReader(stdoutStream_) || !StartReader(stderrStream_)) return false;
    return true;
}

OutputCapture::~OutputCapture() noexcept {
    Drain();
}

OutputCapture::OutputCapture(OutputCapture&& other) noexcept
    : stdoutStream_(std::move(other.stdoutStream_)),
      stderrStream_(std::move(other.stderrStream_)),
      sealEvent_(std::move(other.sealEvent_)),
      stdoutWrite_(std::move(other.stdoutWrite_)),
      stderrWrite_(std::move(other.stderrWrite_)),
      live_(other.live_) {
    other.live_ = false;
}

OutputCapture& OutputCapture::operator=(OutputCapture&& other) noexcept {
    if (this != &other) {
        Drain();
        stdoutStream_ = std::move(other.stdoutStream_);
        stderrStream_ = std::move(other.stderrStream_);
        sealEvent_ = std::move(other.sealEvent_);
        stdoutWrite_ = std::move(other.stdoutWrite_);
        stderrWrite_ = std::move(other.stderrWrite_);
        live_ = other.live_;
        other.live_ = false;
    }
    return *this;
}

void OutputCapture::EndWrite() {
    stdoutWrite_.reset();
    stderrWrite_.reset();
}

bool OutputCapture::StartReader(const std::shared_ptr<Stream>& stream) {
    if (!stream || !stream->reader || stream->thread.get()) return false;

    // Hand shared_ptr to thread via raw pointer (allocated before thread creation).
    std::shared_ptr<Reader>* param = new (std::nothrow) std::shared_ptr<Reader>(stream->reader);
    if (!param) return false;

    const uintptr_t handle =
        _beginthreadex(nullptr, 0, &OutputCapture::ThreadMain, param, 0, nullptr);
    if (handle == 0) {
        delete param;
        return false;
    }
    // Use _beginthreadex (not CreateThread) for proper CRT state setup.
    // NOLINTNEXTLINE(performance-no-int-to-ptr)
    stream->thread.reset(reinterpret_cast<HANDLE>(handle));
    return true;
}

unsigned __stdcall OutputCapture::ThreadMain(void* param) {
    // A unique_ptr of the shared_ptr: the reference is released however ReadReader leaves,
    // including by an exception this layer cannot see.
    const std::unique_ptr<std::shared_ptr<Reader>> owner(
        static_cast<std::shared_ptr<Reader>*>(param));
    ReadReader(*owner);
    return 0;
}

void OutputCapture::Begin() {
    if (!live_) return;
    EndWrite();  // the readers' EOF waits on this, so it precedes the release
    Release();
}

// Let the parked readers go.
void OutputCapture::Release() noexcept {
    if (sealEvent_.get()) SetEvent(sealEvent_.get());
}

// Bounded wait: cancels reads after timeout, abandons non-returning threads.
// Deadline shared across both readers (total time, not per-reader).
void OutputCapture::Drain(DWORD timeoutMs) noexcept {
    EndWrite();  // in case Begin was never reached, e.g. the launch failed
    Release();   // and a reader still parked must not be waited on from here

    const ULONGLONG deadline = GetTickCount64() + timeoutMs;
    JoinReader(stdoutStream_, deadline);
    JoinReader(stderrStream_, deadline);

    // Release the capture's references. A reader that was abandoned still holds its own,
    // so nothing it is using goes away under it — and its thread handle went with this
    // Stream, which is what abandoned it.
    stdoutStream_.reset();
    stderrStream_.reset();
    live_ = false;
}

void OutputCapture::JoinReader(const std::shared_ptr<Stream>& stream,
                               ULONGLONG deadline) noexcept {
    if (!stream || !stream->thread.get()) return;

    const HANDLE handle = stream->thread.get();
    const ULONGLONG now = GetTickCount64();
    const DWORD remaining = now >= deadline ? 0 : static_cast<DWORD>(deadline - now);

    if (WaitForSingleObject(handle, remaining) != WAIT_OBJECT_0) {
        // Nothing will close the write end until the read ends, and the read will not end
        // until it is cancelled. The thread handle is what CancelSynchronousIo takes for
        // exactly this: it cancels the blocking call THIS thread is parked in.
        WarnNoThrow(stream->reader->channel,
                    L"child process did not reach end-of-file; cancelling its read.");
        CancelSynchronousIo(handle);
        if (WaitForSingleObject(handle, kReaderCancelGraceMs) != WAIT_OBJECT_0)
            WarnNoThrow(stream->reader->channel,
                        L"child process could not be stopped; abandoning its reader.");
    }

    // Closed either way. A thread that finished has nothing left to do; a thread that did
    // not is released by this close, which is what detaching from it means, and the
    // Reader it still holds keeps its pipe and its sink alive until it returns on its own.
    stream->thread.reset();
}

// The sink is other people's code running on a thread this program owns — in production
// a lambda that formats a line and hands it to the log file. An exception out of a thread
// function is std::terminate, so letting one through here would turn "a component's line
// could not be logged" into "the program died", which is a strictly worse outcome than
// the line being dropped.
void OutputCapture::Deliver(const std::shared_ptr<Reader>& reader, OutputLine&& line) noexcept {
    try {
        if (reader->sink) reader->sink(std::move(line));
    } catch (...) {
        WarnNoThrow(reader->channel,
                    L"child process wrote a line its sink rejected; the line was dropped.");
    }
}

void OutputCapture::ReadReader(const std::shared_ptr<Reader>& reader) {
    // Parked until the parent's write ends are shut: a read started any earlier would find
    // this program's own handle still open and report EOF for a child that has not written
    // a byte yet. Both happens-before edges live on this one handle — the write ends were
    // closed, and this reader's thread was already running, before the event was set.
    WaitForSingleObject(reader->seal.get(), INFINITE);
    reader->seal.reset();

    char buffer[kReadChunk];

    for (;;) {
        DWORD got = 0;
        if (!ReadFile(reader->read.get(), buffer, sizeof(buffer), &got, nullptr) || got == 0)
            break;
        reader->pending.append(buffer, got);

        // A line can arrive in pieces: a read boundary falls wherever the pipe fills.
        size_t start = 0;
        for (;;) {
            const size_t nl = reader->pending.find('\n', start);
            if (nl == std::string::npos) break;
            size_t end = nl;
            if (end > start && reader->pending[end - 1] == '\r') --end;

            OutputLine line;
            line.channel = reader->channel;
            line.text = DecodeLine(reader->pending.substr(start, end - start));
            Deliver(reader, std::move(line));
            start = nl + 1;
        }
        reader->pending.erase(0, start);

        // Everything complete has been handed over, so what is left is a line still in
        // progress — and a writer that never ends it must not be able to grow this buffer
        // without limit. Past the cap the assumption becomes that it never will.
        if (reader->pending.size() > kMaxPendingBytes) {
            OutputLine line;
            line.channel = reader->channel;
            line.text = DecodeLine(reader->pending);
            reader->pending.clear();
            Deliver(reader, std::move(line));
        }
    }

    // The last line may have no terminator, and that is the line a crash leaves.
    if (!reader->pending.empty()) {
        OutputLine line;
        line.channel = reader->channel;
        line.text = DecodeLine(reader->pending);
        reader->pending.clear();
        Deliver(reader, std::move(line));
    }

    // Released here rather than at the destruction of the Reader: this thread is the only
    // reader of the pipe, and after this it is finished with it whatever else happens to
    // the capture.
    reader->read.reset();
}

}  // namespace Process
