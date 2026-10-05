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
// See the LICENSE.md file in the project root for full license terms.

#include "updater/updater.h"

#include <windows.h>

#include <shellapi.h>

#include <string>
#include <vector>

#include "updater/plan.h"

namespace Updater {
namespace {

// How long to wait for the process that owns the image being replaced, and how long to
// keep retrying the move afterwards.
//
// The wait itself is on the process object, so it returns the instant that process is
// gone and costs nothing on the normal path. The retry window covers the gap between a
// process exiting and the kernel releasing its image: MoveFileExW fails with
// ERROR_ACCESS_DENIED or ERROR_SHARING_VIOLATION until the mapping is torn down, which
// is a moment after the process object signals.
constexpr DWORD kParentWaitMs = 120000;
constexpr DWORD kRetryWindowMs = 60000;
constexpr DWORD kRetryIntervalMs = 100;

// This program has no console (it is a windows-subsystem binary) and its caller has
// already exited, so a failure here has nobody to tell and a message box would block a
// background process forever. The exit code is the record, and the caller's log is
// where the reason it could not proceed lives.
constexpr int kExitBadArgs = 2;
constexpr int kExitPlanUnreadable = 3;
constexpr int kExitFailed = 4;
constexpr int kExitOk = 0;

// Wait for `pid` to exit. A process that cannot be opened has already gone, which is
// the state this needs, so that counts as success too.
bool WaitForProcessExit(DWORD pid, DWORD timeoutMs) {
    if (pid == 0) return true;
    HANDLE process = OpenProcess(SYNCHRONIZE, FALSE, pid);
    if (!process) return true;
    const DWORD result = WaitForSingleObject(process, timeoutMs);
    CloseHandle(process);
    return result == WAIT_OBJECT_0;
}

// Move `from` onto `to`, retrying while the destination is still held open by the
// process that just exited. True once the move is done; otherwise `error` holds the
// last failure.
bool MoveWithRetry(const std::wstring& from, const std::wstring& to, DWORD& error) {
    const ULONGLONG deadline = GetTickCount64() + kRetryWindowMs;
    for (;;) {
        if (MoveFileExW(from.c_str(), to.c_str(), MOVEFILE_REPLACE_EXISTING)) return true;
        error = GetLastError();
        // Anything other than the image still being held open will not clear on its own,
        // so only the lock cases are worth retrying.
        if (error != ERROR_ACCESS_DENIED && error != ERROR_SHARING_VIOLATION) return false;
        if (GetTickCount64() >= deadline) return false;
        Sleep(kRetryIntervalMs);
    }
}

// Schedule `path` for deletion at the next restart: the fallback for an image that
// stays locked for longer than the retry window (a scanner holding it open, say).
bool ScheduleDeleteOnReboot(const std::wstring& path) {
    return MoveFileExW(path.c_str(), nullptr, MOVEFILE_DELAY_UNTIL_REBOOT) != 0;
}

// Start the freshly installed executable.
//
// The command line is composed here from an explicit flag rather than carried over from
// the process that exited. The only thing worth preserving is whether this was a logon
// start, and that is a plan field — a boolean, never a path or a shell fragment. Two
// strings are joined and handed to CreateProcessW, which is not a shell, so neither can
// become syntax.
bool Relaunch(const std::wstring& exe, const Plan& plan) {
    std::wstring command = L"\"" + exe + L"\"";
    if (plan.autostart) command += L" -autostart";

    std::vector<wchar_t> mutableCommand(command.begin(), command.end());
    mutableCommand.push_back(L'\0');

    const size_t slash = exe.find_last_of(L'\\');
    const std::wstring dir =
        (slash == std::wstring::npos) ? std::wstring() : exe.substr(0, slash);

    STARTUPINFOW si = {};
    si.cb = sizeof(si);
    si.dwFlags = STARTF_USESHOWWINDOW;
    si.wShowWindow = SW_SHOWNORMAL;

    PROCESS_INFORMATION pi = {};
    const BOOL started =
        CreateProcessW(exe.c_str(), mutableCommand.data(), nullptr, nullptr, FALSE, 0, nullptr,
                       dir.empty() ? nullptr : dir.c_str(), &si, &pi);
    if (!started) return false;
    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    return true;
}

// Replace the executable and start the new one.
//
// The order is the same as the script this replaces: move the running image out of the
// way (which now succeeds, because the process holding it has exited), put the staged
// copy in its place, and only then start it. If the staged copy cannot be installed the
// previous executable goes back, so a failure here is a no-op install rather than one
// with no executable at all.
int ApplyReplace(const Plan& plan) {
    const std::wstring& target = plan.target;
    const std::wstring& staged = plan.newFile;
    const std::wstring& backup = plan.backup;

    const DWORD targetAttrs = GetFileAttributesW(target.c_str());
    if (targetAttrs == INVALID_FILE_ATTRIBUTES ||
        (targetAttrs & FILE_ATTRIBUTE_REPARSE_POINT)) {
        // The executable named for replacement is not there as a regular file. Stop:
        // installing over whatever is at that path is not this program's decision.
        return kExitFailed;
    }

    DeleteFileW(backup.c_str());

    DWORD moveError = 0;
    if (!MoveWithRetry(target, backup, moveError)) {
        // The old image could not be moved aside, so the swap will happen at the next
        // boot instead. The staged copy is left where it is so that remains possible,
        // and nothing is started: the running install is unchanged.
        ScheduleDeleteOnReboot(target);
        return kExitFailed;
    }

    DWORD installError = 0;
    if (!MoveWithRetry(staged, target, installError)) {
        // Put the previous executable back. If even that fails the install would be left
        // without an executable, which is what the reboot-time request below repairs.
        MoveFileExW(backup.c_str(), target.c_str(), MOVEFILE_REPLACE_EXISTING);
        if (GetFileAttributesW(target.c_str()) == INVALID_FILE_ATTRIBUTES)
            ScheduleDeleteOnReboot(target);
        return kExitFailed;
    }

    // The swap is confirmed, so the previous executable is no longer needed.
    if (!DeleteFileW(backup.c_str())) ScheduleDeleteOnReboot(backup);

    Relaunch(target, plan);
    return kExitOk;
}

// Delete the executable and remove its directory if nothing else is left in it.
//
// The rmdir is deliberately non-recursive: the program directory is not assumed to be
// ours alone, so this succeeds only when the removal left it empty and does nothing at
// all when the user keeps their own files there.
int ApplyRemove(const Plan& plan) {
    const std::wstring& target = plan.target;

    // Retrying the delete IS the wait condition: it succeeds as soon as this process's
    // own image is released, which is exactly the state that matters. Watching the
    // process name instead would hang on an unrelated copy running elsewhere.
    const ULONGLONG deadline = GetTickCount64() + kRetryWindowMs;
    for (;;) {
        if (DeleteFileW(target.c_str())) break;
        const DWORD error = GetLastError();
        if (error == ERROR_FILE_NOT_FOUND || error == ERROR_PATH_NOT_FOUND) break;
        if (error != ERROR_ACCESS_DENIED && error != ERROR_SHARING_VIOLATION) break;
        if (GetTickCount64() >= deadline) {
            ScheduleDeleteOnReboot(target);
            break;
        }
        Sleep(kRetryIntervalMs);
    }

    if (GetFileAttributesW(target.c_str()) != INVALID_FILE_ATTRIBUTES) return kExitFailed;

    if (!plan.dir.empty()) RemoveDirectoryW(plan.dir.c_str());
    return kExitOk;
}

}  // namespace

int Run(const wchar_t* commandLine) {
    // Tokenized, never scanned. A substring search over the raw command line would let
    // a path or a quoted argument containing the flag turn it on.
    int argc = 0;
    LPWSTR* argv = CommandLineToArgvW(commandLine ? commandLine : L"", &argc);
    if (!argv) return kExitBadArgs;

    std::wstring planPath;
    for (int i = 1; i < argc; ++i) {
        const std::wstring arg = argv[i];
        if (arg == L"--apply-plan") {
            if (i + 1 >= argc || !planPath.empty()) {
                LocalFree(static_cast<void*>(argv));
                return kExitBadArgs;
            }
            planPath = argv[++i];
        } else {
            // Unknown arguments are refused rather than ignored: this program has one
            // documented form, and running it any other way is a mistake worth failing.
            LocalFree(static_cast<void*>(argv));
            return kExitBadArgs;
        }
    }
    LocalFree(static_cast<void*>(argv));

    if (planPath.empty()) return kExitBadArgs;

    Plan plan;
    std::wstring why;
    if (!ReadPlan(planPath, plan, why)) return kExitPlanUnreadable;

    if (!WaitForProcessExit(plan.parentPid, kParentWaitMs)) return kExitFailed;

    const int result = (plan.op == Op::Replace) ? ApplyReplace(plan) : ApplyRemove(plan);

    // The plan is this process's to consume. Removing it here, whatever the outcome,
    // keeps the directory from accumulating work orders that will never run again.
    DeletePlanFile(planPath);
    return result;
}

}  // namespace Updater
