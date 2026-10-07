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

#pragma once
#include <windows.h>

#include <cstddef>
#include <functional>
#include <memory>
#include <string>

namespace Process {

// Which stream a captured line came from. Kept rather than folded into one pipe,
// because merging the two destroys the only fact that tells them apart.
enum class Channel { Stdout, Stderr };

// One line, decoded to UTF-16 and stripped of its terminator.
struct OutputLine {
    Channel channel = Channel::Stdout;
    std::wstring text;
};

using OutputSink = std::function<void(OutputLine&&)>;

// Captures stdout+stderr. Readers start before process (parked), ensuring pipes
// are ready. If capture setup fails, launch is aborted (prevents silent drops).
//
//   OutputCapture capture(sink);
//   if (!capture) return {};  // setup failed
//   ... CreateProcess with stdoutWrite()/stderrWrite() ...
//   capture.Begin();  // close write ends, start reading
//
// The read ends cannot go into the supervisor's WaitForMultipleObjects set: reading a
// pipe is a blocking call, not a signal, so the handle is signalled from the moment it
// exists. That is why each stream gets a thread, and why Drain — which a destructor
// reaches — is bounded rather than a plain join: a process that could not be stopped
// still holds the write end, and the reader parked in ReadFile would never come back.
constexpr DWORD kDrainTimeoutMs = 5000;

// What the reader is then given to act on its cancelled read.
constexpr DWORD kReaderCancelGraceMs = 1000;

// The most unterminated output this will hold on to for a line that has not ended yet.
//
// A line is not a line until its newline arrives, so a component that separates updates
// with '\r', or that writes one very long run with no terminator at all, would grow this
// buffer for as long as it keeps writing. Past this the run is forwarded as a line of its
// own: the boundary is a guess, but the alternative is memory whose size the payload
// decides, and the bytes reach the log either way. A cut that lands inside a multi-byte
// character is decoded by the same fallback as any other undecodable byte.
constexpr size_t kMaxPendingBytes = 64 * 1024;

// OWNERSHIP: sink called from detached reader thread after Drain timeout.
// Must capture by value only. Exceptions absorbed (cannot afford thread termination).
class OutputCapture {
public:
    // Out of line because destroying streams_ needs sizeof(Stream), which only the
    // .cpp has.
    OutputCapture();

    // An empty sink means no capture: no pipes, no threads.
    explicit OutputCapture(const OutputSink& sink);

    // Runs Drain. Declared noexcept because a destructor has nobody to report a failure
    // to, and Drain is written to be genuinely incapable of throwing.
    ~OutputCapture() noexcept;

    OutputCapture(OutputCapture&&) noexcept;
    OutputCapture& operator=(OutputCapture&&) noexcept;
    OutputCapture(const OutputCapture&) = delete;
    OutputCapture& operator=(const OutputCapture&) = delete;

    // The inheritable write ends to hand to the child. Null only when there is no capture
    // at all: a capture is built whole or not at all, so these are never half-present.
    HANDLE stdoutWrite() const { return stdoutWrite_.get(); }
    HANDLE stderrWrite() const { return stderrWrite_.get(); }

    // Whether this is a capture a child may be handed. A launcher asks before deciding
    // whether the child's standard handles become its business — an empty sink must leave
    // them alone, not point them at nothing, and one that could not be built must stop the
    // launch rather than supply a stream nobody is reading.
    explicit operator bool() const { return live_; }
    bool active() const { return live_; }

    // Close the parent's write ends and let the readers start. Must be called on every
    // path, including a failed launch, or the handles leak. Idempotent.
    void Begin();

    // Join both readers, spending at most `timeoutMs` between them — one budget, not one
    // per reader, so what the name promises is what a caller gets. Idempotent, and a
    // no-op before Begin. noexcept because the destructor runs it — a reader that does
    // not return in time has the read it is parked in cancelled, and one that still does
    // not return is detached rather than thrown from, since there is nothing a caller
    // could do about it and an exception out of this destructor would end the program.
    // See the note above about what a sink may hold once a reader has been abandoned.
    void Drain(DWORD timeoutMs = kDrainTimeoutMs) noexcept;

private:
    // Owns one handle for exactly as long as it exists.
    //
    // Every handle in this class is owned this way rather than closed by hand at the
    // points that happen to know they are finished with it. That is not tidiness: the
    // leaks were on the paths that did *not* know — a pipe whose reader never started
    // because the launch failed — and a type cannot forget a path that nobody thought of.
    struct Owned {
        Owned() = default;
        explicit Owned(HANDLE handle) noexcept : handle_(handle) {}
        ~Owned() { reset(); }

        Owned(Owned&& other) noexcept : handle_(other.handle_) { other.handle_ = nullptr; }
        Owned& operator=(Owned&& other) noexcept {
            if (this != &other) {
                reset();
                handle_ = other.handle_;
                other.handle_ = nullptr;
            }
            return *this;
        }
        Owned(const Owned&) = delete;
        Owned& operator=(const Owned&) = delete;

        HANDLE get() const noexcept { return handle_; }

        // Release a handle at a moment the owner chooses, or by the destructor.
        void reset() noexcept {
            if (handle_) {
                CloseHandle(handle_);
                handle_ = nullptr;
            }
        }

        // Take ownership of another handle, releasing whatever this held. Separate from
        // the constructor because it is reached after the handle already exists — the
        // moment a CreatePipe or _beginthreadex returns is not a moment a constructor can
        // be waiting in, and a failure in between must not orphan what was just made.
        void reset(HANDLE handle) noexcept {
            reset();
            handle_ = handle;
        }

    private:
        HANDLE handle_ = nullptr;
    };

    struct Reader;  // the read end and the bytes on it: shared with the thread
    struct Stream;  // one channel's thread, owned here and nowhere else

    // Build both channels or neither, and only then let a caller do anything with it.
    // False means no capture could be provided at all.
    bool Build(const OutputSink& sink);

    // Release the readers — the one event that lets a parked reader start reading.
    void Release() noexcept;

    // The thread entry point. Takes ownership of the shared_ptr passed across the
    // process-less boundary the Win32 thread API insists on.
    static unsigned __stdcall ThreadMain(void* param);
    static void ReadReader(const std::shared_ptr<Reader>& reader);

    // Start one channel's reader, which parks immediately and reads nothing yet. False if
    // the thread could not be created — the only reader failure a caller can still answer,
    // which is why it is reported rather than logged and endured.
    bool StartReader(const std::shared_ptr<Stream>& stream);

    // Wait for one reader to finish within `deadline`, cancelling its read and finally
    // abandoning it if it will not. `deadline` is absolute, so the whole Drain shares one
    // budget rather than handing each reader a fresh one.
    static void JoinReader(const std::shared_ptr<Stream>& stream, ULONGLONG deadline) noexcept;

    // Hand one line to the sink, absorbing whatever the sink does about it.
    static void Deliver(const std::shared_ptr<Reader>& reader, OutputLine&& line) noexcept;

    void EndWrite();

    // One per channel, reference-counted. The readers hold these rather than `this`,
    // so a move of the capture leaves running threads valid — and a reader that was
    // detached keeps the stream it is reading into alive until it finishes, instead of
    // writing into memory the capture has already freed.
    std::shared_ptr<Stream> stdoutStream_;
    std::shared_ptr<Stream> stderrStream_;

    // The write ends, and the gate every reader waits behind until those ends are shut:
    // a reader that started reading while this program still held a write end would meet
    // its own handle's EOF, not the child's.
    Owned sealEvent_;
    Owned stdoutWrite_;
    Owned stderrWrite_;

    // True only once both readers are running and parked. Everything a caller can ask is
    // answered from this, so a capture that could not be built is inert rather than
    // half-there.
    bool live_ = false;
};

}  // namespace Process
