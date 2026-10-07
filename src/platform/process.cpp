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

#include "platform/process.h"

#include <tlhelp32.h>

#include <algorithm>
#include <utility>

#include "app/logging.h"
#include "app/text.h"

namespace Process {
namespace {

// Format error as "code (system message)" for log readability.
std::wstring DescribeError(DWORD code) {
    wchar_t* text = nullptr;
    const DWORD n =
        FormatMessageW(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM |
                           FORMAT_MESSAGE_IGNORE_INSERTS,
                       nullptr, code, 0, reinterpret_cast<LPWSTR>(&text), 0, nullptr);
    const std::wstring message = (n > 0 && text) ? TrimW(text) : std::wstring();
    if (text) LocalFree(text);
    return std::to_wstring(code) + (message.empty() ? L"" : L" (" + message + L")");
}

// Collect `root` and every descendant from a single snapshot, leaves first, so
// terminating in order never leaves a child reparented to something we then miss.
// A visited set guards against a cycle formed by PID reuse within the snapshot.
std::vector<DWORD> CollectTree(DWORD root) {
    HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snapshot == INVALID_HANDLE_VALUE) return {root};

    std::vector<std::pair<DWORD, DWORD>> edges;  // (parent, child)
    PROCESSENTRY32W entry = {};
    entry.dwSize = sizeof(entry);
    if (Process32FirstW(snapshot, &entry)) {
        do {
            if (entry.th32ProcessID != entry.th32ParentProcessID)
                edges.emplace_back(entry.th32ParentProcessID, entry.th32ProcessID);
        } while (Process32NextW(snapshot, &entry));
    }
    CloseHandle(snapshot);

    std::vector<DWORD> visited;
    std::vector<DWORD> stack = {root};
    while (!stack.empty()) {
        const DWORD current = stack.back();
        stack.pop_back();
        if (std::find(visited.begin(), visited.end(), current) != visited.end()) continue;
        visited.push_back(current);
        for (const auto& [parent, child] : edges)
            if (parent == current &&
                std::find(visited.begin(), visited.end(), child) == visited.end())
                stack.push_back(child);
    }
    // Discovery order is root-first, so reversing terminates children first.
    return std::vector<DWORD>(visited.rbegin(), visited.rend());
}

// Terminate PIDs and wait for actual exit (TerminateProcess is async).
bool TerminateAndWait(const std::vector<DWORD>& victims, DWORD timeoutMs) {
    std::vector<HANDLE> pending;
    pending.reserve(victims.size());
    bool allGone = true;

    for (DWORD pid : victims) {
        if (pid == 0 || pid == 4) continue;  // never touch Idle/System
        HANDLE process = OpenProcess(PROCESS_TERMINATE | SYNCHRONIZE, FALSE, pid);
        if (!process) {
            // An already-exited process is the outcome we wanted; anything else is
            // a process we are not allowed to stop, which the caller must know.
            const DWORD err = GetLastError();
            if (err != ERROR_INVALID_PARAMETER) {
                LOGW(L"Cannot terminate pid " + std::to_wstring(pid) + L": err " +
                     DescribeError(err));
                allGone = false;
            }
            continue;
        }
        TerminateProcess(process, 1);
        pending.push_back(process);
    }

    const ULONGLONG deadline = GetTickCount64() + timeoutMs;
    for (size_t i = 0; i < pending.size(); i += MAXIMUM_WAIT_OBJECTS) {
        const DWORD count =
            static_cast<DWORD>(std::min<size_t>(MAXIMUM_WAIT_OBJECTS, pending.size() - i));
        const ULONGLONG now = GetTickCount64();
        const DWORD remaining = (now >= deadline) ? 0 : static_cast<DWORD>(deadline - now);
        if (WaitForMultipleObjects(count, pending.data() + i, TRUE, remaining) != WAIT_OBJECT_0)
            allGone = false;
    }
    for (HANDLE process : pending) CloseHandle(process);
    return allGone;
}

// Job object with JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE (crash-safe cleanup).
HANDLE CreateContainingJob() {
    HANDLE job = CreateJobObjectW(nullptr, nullptr);
    if (!job) {
        LOGW(L"Could not create a job object (err " + DescribeError(GetLastError()) + L").");
        return nullptr;
    }
    JOBOBJECT_EXTENDED_LIMIT_INFORMATION limits = {};
    limits.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
    if (!SetInformationJobObject(job, JobObjectExtendedLimitInformation, &limits,
                                 sizeof(limits))) {
        LOGW(L"Could not configure the job object (err " + DescribeError(GetLastError()) +
             L").");
        CloseHandle(job);
        return nullptr;
    }
    return job;
}

struct Started {
    Handle process;
    HANDLE thread = nullptr;
};

// Inheritable null device handle. Returns nullptr on failure (not INVALID_HANDLE_VALUE).
HANDLE OpenNull(DWORD access) {
    SECURITY_ATTRIBUTES sa = {};
    sa.nLength = sizeof(sa);
    sa.bInheritHandle = TRUE;

    HANDLE handle = CreateFileW(L"NUL", access, FILE_SHARE_READ | FILE_SHARE_WRITE, &sa,
                                OPEN_EXISTING, 0, nullptr);
    return handle == INVALID_HANDLE_VALUE ? nullptr : handle;
}

Started StartProcess(const std::wstring& exePath, const std::wstring& args,
                     const std::wstring& workDir, bool hidden, DWORD extraFlags,
                     const OutputCapture* capture) {
    std::wstring cmd = L"\"" + exePath + L"\"";
    if (!args.empty()) cmd += L" " + args;
    std::vector<wchar_t> mutableCmd(cmd.begin(), cmd.end());
    mutableCmd.push_back(L'\0');

    STARTUPINFOW si = {};
    si.cb = sizeof(si);
    DWORD flags = extraFlags;
    if (hidden) {
        si.dwFlags = STARTF_USESHOWWINDOW;
        si.wShowWindow = SW_HIDE;
        flags |= CREATE_NO_WINDOW;
    }

    // An unset member of STARTF_USESTDHANDLES is NULL rather than "whatever we had", and
    // a null standard handle is a trap the child falls into in ways that depend on the
    // child. So once capturing, all three are real handles: the pipe for a channel being
    // read, and the null device for the rest — including stdin, which this program has
    // none of, being built -mwindows. A capture is whole or absent, so a channel is never
    // handed a pipe nobody is reading; no capture at all leaves every handle alone, as the
    // header promises.
    const bool capturing = capture != nullptr && capture->active();
    HANDLE nullIn = nullptr;
    HANDLE nullOut = nullptr;
    HANDLE nullErr = nullptr;
    if (capturing) {
        si.dwFlags |= STARTF_USESTDHANDLES;
        si.hStdOutput = capture->stdoutWrite();
        si.hStdError = capture->stderrWrite();
        if (!si.hStdOutput) si.hStdOutput = nullOut = OpenNull(GENERIC_WRITE);
        if (!si.hStdError) si.hStdError = nullErr = OpenNull(GENERIC_WRITE);
        si.hStdInput = nullIn = OpenNull(GENERIC_READ);
    }

    PROCESS_INFORMATION pi = {};
    const BOOL started = CreateProcessW(exePath.c_str(), mutableCmd.data(), nullptr, nullptr,
                                        capturing ? TRUE : FALSE, flags, nullptr,
                                        workDir.empty() ? nullptr : workDir.c_str(), &si, &pi);
    if (nullIn) CloseHandle(nullIn);
    if (nullOut) CloseHandle(nullOut);
    if (nullErr) CloseHandle(nullErr);
    if (!started) {
        LOGE(L"Launch failed: " + cmd + L" [working directory: " +
             (workDir.empty() ? std::wstring(L"<inherited>") : workDir) + L"] err " +
             DescribeError(GetLastError()));
        return {};
    }
    return {Handle(pi.hProcess, pi.dwProcessId), pi.hThread};
}

}  // namespace

// ---- Handle -----------------------------------------------------------------

bool Handle::WaitForExit(DWORD timeoutMs) const {
    if (!handle_) return true;  // nothing to wait for is indistinguishable from gone
    return WaitForSingleObject(handle_, timeoutMs) == WAIT_OBJECT_0;
}

bool Handle::ExitCode(DWORD& code) const {
    return handle_ && GetExitCodeProcess(handle_, &code) && code != STILL_ACTIVE;
}

void Handle::Close() {
    if (handle_) {
        CloseHandle(handle_);
        handle_ = nullptr;
    }
    pid_ = 0;
}

// ---- Child ------------------------------------------------------------------

Child& Child::operator=(Child&& other) noexcept {
    if (this != &other) {
        // Release what we currently hold exactly as the destructor would.
        Child discarded(std::move(*this));
        process_ = std::move(other.process_);
        job_ = other.job_;
        capture_ = std::move(other.capture_);
        other.job_ = nullptr;
    }
    return *this;
}

Child::~Child() {
    // Stop the process before anything joins its readers: the other order joins a pipe
    // the living child still holds, which never reaches EOF.
    static_cast<void>(StopAndJoin(kTerminateTimeoutMs));
    if (job_) CloseHandle(job_);
}

// Stop child and join readers. Returns whether process is confirmed gone.
// noexcept: destructor calls this, only allocation can throw.
bool Child::StopAndJoin(DWORD timeoutMs) noexcept {
    bool gone = true;

    if (job_) {
        // Also when the process has already exited: a descendant in the job can outlive
        // it and hold the output pipe open.
        TerminateJobObject(job_, 1);
        gone = process_.WaitForExit(timeoutMs);
    } else if (process_) {
        // No job, so the tree comes down by hand — and also when the root has already
        // exited, for the reason stated above: a descendant outliving it holds the pipe
        // open and the join below never reaches EOF.
        //
        // The root is stopped through the handle this object owns, never by PID, and
        // before its descendants so that it cannot replace what is about to be walked
        // over. Between a snapshot and an OpenProcess a PID can name a different process,
        // and the one process this object is responsible for is the one that must not be
        // stopped by mistake. Its descendants have no handle of ours and are reached by
        // PID exactly as KillTree does — that part cannot be made race-free, and it is
        // not the part this type exists to make safe.
        //
        // Collecting the tree by the root's PID is safe even when the root has already
        // exited, and for the same reason everything else here is: an open process handle
        // keeps its PID from being reused, so this handle is what guarantees the snapshot
        // is still describing the process it names.
        const DWORD root = process_.pid();
        const ULONGLONG start = GetTickCount64();
        try {
            std::vector<DWORD> descendants = CollectTree(root);
            descendants.erase(std::remove(descendants.begin(), descendants.end(), root),
                              descendants.end());

            if (!process_.Exited()) {
                TerminateProcess(process_.get(), 1);
                gone = process_.WaitForExit(timeoutMs);
            }

            // What is left of the budget, so a root that took its whole timeout does not
            // hand the descendants a second one.
            const ULONGLONG elapsed = GetTickCount64() - start;
            const DWORD remaining =
                elapsed >= timeoutMs ? 0 : timeoutMs - static_cast<DWORD>(elapsed);
            if (!TerminateAndWait(descendants, remaining)) gone = false;
        } catch (...) {
            // Out of memory walking the tree. Fall back to the handle we own: leaving
            // the root alive would hang the join below, which is worse than the
            // descendants we cannot chase.
            TerminateProcess(process_.get(), 1);
            gone = process_.WaitForExit(timeoutMs);
        }
    }

    // On every path, so "stopped" means everything the child wrote has been delivered.
    capture_.Drain();
    return gone;
}

bool Child::Terminate(DWORD timeoutMs) {
    const DWORD pid = process_.pid();

    if (StopAndJoin(timeoutMs)) return true;

    LOGW(L"pid " + std::to_wstring(pid) + L" did not exit within the termination timeout.");
    return false;
}

// ---- Free functions ---------------------------------------------------------

Child LaunchChild(const std::wstring& exePath, const std::wstring& args,
                  const LaunchOptions& options) {
    OutputCapture capture(options.output);

    // A capture was asked for and could not be built. Launching anyway would hand the
    // child a standard handle it cannot write to successfully, and its own account of why
    // it failed — the reason the caller asked for a capture — would be the part lost. So
    // the launch does not happen, and the caller gets the same empty Child it gets for a
    // program that is not there.
    if (options.output && !capture) {
        LOGE(L"Could not capture the output of " + exePath +
             L"; the launch was refused so that no component runs unobserved.");
        return {};
    }

    HANDLE job = CreateContainingJob();

    // Suspended, so containment is decided before a single instruction runs: a
    // child that started first could spawn a descendant outside the job.
    Started started =
        StartProcess(exePath, args, options.workDir, true, CREATE_SUSPENDED, &capture);
    if (!started.process) {
        capture.Drain();
        if (job) CloseHandle(job);
        return {};
    }

    if (job && !AssignProcessToJobObject(job, started.process.get())) {
        LOGW(L"Could not contain " + exePath + L" in a job object (err " +
             DescribeError(GetLastError()) +
             L"); falling back to process-tree termination for it.");
        CloseHandle(job);
        job = nullptr;
    }

    if (ResumeThread(started.thread) == static_cast<DWORD>(-1)) {
        LOGE(L"Could not resume " + exePath + L" (err " + DescribeError(GetLastError()) +
             L").");
        TerminateProcess(started.process.get(), 1);
        CloseHandle(started.thread);
        capture.Drain();
        if (job) CloseHandle(job);
        return {};
    }
    CloseHandle(started.thread);

    // The child owns the write ends now, so this is the earliest the readers can run
    // without waiting on a pipe we ourselves are holding open.
    capture.Begin();
    return Child(std::move(started.process), job, std::move(capture));
}

Handle LaunchDetached(const std::wstring& exePath, const std::wstring& args,
                      const std::wstring& workDir, bool hidden) {
    // No capture: it means joining a reader, and a process that must outlive us cannot
    // be joined.
    Started started = StartProcess(exePath, args, workDir, hidden, 0, nullptr);
    if (started.thread) CloseHandle(started.thread);
    return std::move(started.process);
}

bool TryImagePath(DWORD pid, std::wstring& out) {
    out.clear();
    if (pid == 0) return false;
    HANDLE process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);
    if (!process) process = OpenProcess(PROCESS_QUERY_INFORMATION, FALSE, pid);
    if (!process) return false;

    wchar_t buf[MAX_PATH * 2];
    DWORD size = static_cast<DWORD>(std::size(buf));
    const bool ok = QueryFullProcessImageNameW(process, 0, buf, &size) != 0;
    if (ok) out.assign(buf, size);
    CloseHandle(process);
    return ok && !out.empty();
}

std::vector<DWORD> FindByName(const std::wstring& baseName) {
    std::vector<DWORD> found;
    const std::wstring want = LowerW(baseName);
    HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snapshot == INVALID_HANDLE_VALUE) return found;
    PROCESSENTRY32W entry = {};
    entry.dwSize = sizeof(entry);
    if (Process32FirstW(snapshot, &entry)) {
        do {
            if (LowerW(entry.szExeFile) == want) found.push_back(entry.th32ProcessID);
        } while (Process32NextW(snapshot, &entry));
    }
    CloseHandle(snapshot);
    return found;
}

std::vector<DWORD> FindByImagePath(const std::wstring& exePath) {
    std::vector<DWORD> found;
    if (exePath.empty()) return found;

    // Narrow the snapshot scan by basename first, then confirm each candidate's full
    // path. Comparing paths for every process on the machine would open a handle to
    // each one for nothing.
    const size_t slash = exePath.find_last_of(L"\\/");
    const std::wstring baseName =
        (slash == std::wstring::npos) ? exePath : exePath.substr(slash + 1);
    const std::wstring want = LowerW(exePath);

    for (DWORD pid : FindByName(baseName)) {
        std::wstring image;
        if (TryImagePath(pid, image) && LowerW(image) == want) found.push_back(pid);
    }
    return found;
}

bool KillTree(DWORD pid, DWORD timeoutMs) {
    if (pid == 0 || pid == 4) return false;  // never touch Idle/System
    return TerminateAndWait(CollectTree(pid), timeoutMs);
}

size_t TerminateByImagePath(const std::wstring& exePath) {
    size_t terminated = 0;
    for (DWORD pid : FindByImagePath(exePath)) {
        LOGW(L"Terminating " + exePath + L" pid " + std::to_wstring(pid) +
             L", which this program did not launch.");
        if (KillTree(pid)) ++terminated;
    }
    return terminated;
}

}  // namespace Process
