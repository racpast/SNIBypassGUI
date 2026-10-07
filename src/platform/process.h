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

#include <string>
#include <utility>
#include <vector>

#include "platform/child_output.h"

// Process ownership and lifetime.
//
// Process identity via kernel handle (not PID, which may be reused).
// All queries go through the handle to avoid races.
namespace Process {

// Timeout for process termination wait (event-driven, not sleep).
// Caps abnormal cases (wedged driver, stuck handle holder).
constexpr DWORD kTerminateTimeoutMs = 5000;

// An owned process handle. Move-only: exactly one owner closes it.
class Handle {
public:
    Handle() = default;
    Handle(HANDLE process, DWORD pid) : handle_(process), pid_(pid) {}
    ~Handle() { Close(); }

    Handle(Handle&& other) noexcept : handle_(other.handle_), pid_(other.pid_) {
        other.handle_ = nullptr;
        other.pid_ = 0;
    }
    Handle& operator=(Handle&& other) noexcept {
        if (this != &other) {
            Close();
            handle_ = other.handle_;
            pid_ = other.pid_;
            other.handle_ = nullptr;
            other.pid_ = 0;
        }
        return *this;
    }
    Handle(const Handle&) = delete;
    Handle& operator=(const Handle&) = delete;

    explicit operator bool() const { return handle_ != nullptr; }
    DWORD pid() const { return pid_; }
    HANDLE get() const { return handle_; }

    // True once the process has exited. Answered by the kernel through the handle,
    // so it can never disagree with reality and can never be fooled by a PID that
    // has since been handed to someone else.
    bool Exited() const { return WaitForExit(0); }

    // Block until the process exits or `timeoutMs` elapses. True if it exited.
    bool WaitForExit(DWORD timeoutMs) const;

    // The process's exit code. Meaningful only once it has exited.
    bool ExitCode(DWORD& code) const;

    void Close();

private:
    HANDLE handle_ = nullptr;
    DWORD pid_ = 0;
};

// Child process in job object:
// - Terminating job kills process + all descendants atomically
// - Job kills members on last handle close (crash-safe cleanup)
// Falls back to tree walk if OS denies job containment.
class Child {
public:
    Child() = default;
    Child(Handle&& process, HANDLE job, OutputCapture capture)
        : process_(std::move(process)), job_(job), capture_(std::move(capture)) {}
    ~Child();

    Child(Child&& other) noexcept
        : process_(std::move(other.process_)),
          job_(other.job_),
          capture_(std::move(other.capture_)) {
        other.job_ = nullptr;
    }
    Child& operator=(Child&& other) noexcept;
    Child(const Child&) = delete;
    Child& operator=(const Child&) = delete;

    explicit operator bool() const { return static_cast<bool>(process_); }
    DWORD pid() const { return process_.pid(); }

    // True while the process is still running. This is a kernel query, not a
    // remembered flag, so a child that died on its own is reported as stopped the
    // first time anyone asks.
    bool Running() const { return static_cast<bool>(process_) && !process_.Exited(); }

    bool WaitForExit(DWORD timeoutMs) const { return process_.WaitForExit(timeoutMs); }
    bool ExitCode(DWORD& code) const { return process_.ExitCode(code); }

    // The process handle, for the one question the methods above cannot answer:
    // "whichever of these children dies first" needs every handle in a single wait.
    //
    // Ownership does not travel with it. The handle stays owned by this Child, is
    // valid only while this Child is alive and has not been moved from, and may be
    // waited on and nothing else — never closed, never duplicated into a longer life
    // than the Child's.
    HANDLE waitHandle() const { return process_.get(); }

    // Terminate the process and every descendant, then wait until the process is
    // actually gone. Returns true if that is confirmed. Calling it on an empty or
    // already-exited Child succeeds without doing anything.
    bool Terminate(DWORD timeoutMs = kTerminateTimeoutMs);

private:
    // Stop the child and everything it started, then join its readers. noexcept because
    // the destructor calls it, and stopping before joining is the correctness.
    bool StopAndJoin(DWORD timeoutMs) noexcept;

    Handle process_;
    HANDLE job_ = nullptr;  // null when the OS declined to contain this child

    // Destroyed first, so the join happens while the process handle that stops it is
    // still in scope.
    OutputCapture capture_;
};

// What a child is launched with, beyond its executable and arguments.
struct LaunchOptions {
    std::wstring workDir;

    // Empty captures nothing and leaves the child's standard handles untouched.
    OutputSink output;
};

// Launch a child under this program's supervision: hidden, no console, contained
// in its own job object. An empty Child means the launch failed (the reason,
// including the command line and working directory, is logged).
Child LaunchChild(const std::wstring& exePath, const std::wstring& args,
                  const LaunchOptions& options);

// Launch a process that must OUTLIVE this program — the uninstall and self-update
// helpers, which exist precisely to act after we are gone — so it gets no job.
// The returned handle is only for reporting success.
Handle LaunchDetached(const std::wstring& exePath, const std::wstring& args,
                      const std::wstring& workDir, bool hidden);

// There is deliberately no "the arguments this process was started with" accessor.
//
// One existed to hand the whole original command line back to the self-update helper,
// which meant an unparsed string of user input travelled into the code that relaunched
// the program. The updater now relaunches with a single flag derived from a boolean the
// caller decided on, so there is nothing left for such an accessor to carry — and any
// future caller that wants one should be asked why, since re-passing an unparsed
// command line is exactly the shape this program stopped using.

// Full image path of `pid`. A false return means the process could not be
// interrogated: it has already exited, or its handle could not be opened
// (protected process, another user's session). That is "unknown", NOT "some other
// program" — treating the two as the same is how a program ends up killing
// processes it merely failed to identify.
bool TryImagePath(DWORD pid, std::wstring& out);

// PIDs of every process whose image basename matches, case-insensitively.
std::vector<DWORD> FindByName(const std::wstring& baseName);

// PIDs of every process running the executable at exactly `exePath`.
//
// A PID is not ownership and this does not pretend otherwise — it is a snapshot,
// used to answer "is this program running" about a copy we did not launch and hold
// no handle to. A process whose image path cannot be read is not included, so the
// answer is always "processes positively identified as that executable", never a
// guess. Our own children are tracked by handle instead; see Child.
std::vector<DWORD> FindByImagePath(const std::wstring& exePath);

// Terminate `pid` and its descendants, children first, and wait for them to be
// gone. True if every one is confirmed exited. This is for processes we do NOT own
// a handle to; our own children are stopped through Child::Terminate, which needs
// no PID and cannot race PID reuse.
bool KillTree(DWORD pid, DWORD timeoutMs = kTerminateTimeoutMs);

// Terminate every process running the executable at exactly `exePath`, and return
// how many were terminated. Matching on the full path rather than the basename is
// what keeps this from reaching a program that merely shares a common name:
// "nginx.exe" belonging to someone else lives somewhere else.
size_t TerminateByImagePath(const std::wstring& exePath);

}  // namespace Process
