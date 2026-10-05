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
// See the LICENSE.md file in the project root for full terms and conditions.

#include "platform/command.h"

#include <string>
#include <thread>
#include <vector>

#include "app/logging.h"
#include "app/text.h"
#include "platform/process.h"

namespace Command {
namespace {

// Decode captured console output.
//
// UTF-8 is tried strictly: without MB_ERR_INVALID_CHARS, MultiByteToWideChar
// happily turns arbitrary bytes into U+FFFD and reports success, so a console that
// answered in the machine's ANSI code page would decode to a non-empty string of
// replacement characters and the fallback below would never run. Strict decoding is
// what makes "is this actually UTF-8?" a question with an answer.
std::wstring DecodeConsoleOutput(const std::string& bytes) {
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

// Tokenize and compare each argument exactly. `skipFirst` decides whether argv[0] is
// treated as a program name or as an argument.
//
// The two callers below differ only in that, and the difference is the whole reason
// this takes a parameter instead of assuming one form: a full command line puts the
// program name at argv[0], while the lpCmdLine a wWinMain receives has already had it
// removed, so the first real argument is at argv[0] there. Skipping unconditionally —
// which this did — means a stripped line whose flag IS the first argument matches
// nothing at all, which is exactly how a scheduled `-autostart` launch was missed.
bool HasFlagTokenized(const std::wstring& line, const std::wstring& flag, bool skipFirst) {
    if (line.empty() || flag.empty()) return false;

    int argc = 0;
    LPWSTR* argv = CommandLineToArgvW(line.c_str(), &argc);
    if (!argv) {
        // The tokenizer failed, which for a non-empty command line means a malformed
        // one. Answering "no flag" is the conservative result: nothing is enabled on
        // the strength of an input we could not read.
        LOGW(L"Could not tokenize the command line; no flags read from it.");
        return false;
    }

    bool found = false;
    for (int i = skipFirst ? 1 : 0; i < argc && !found; ++i) found = (flag == argv[i]);
    LocalFree(static_cast<void*>(argv));
    return found;
}

}  // namespace

bool CommandLineHasFlag(const std::wstring& fullCommandLine, const std::wstring& flag) {
    return HasFlagTokenized(fullCommandLine, flag, /*skipFirst=*/true);
}

bool CommandLineHasFlagInArgs(const std::wstring& argsOnly, const std::wstring& flag) {
    return HasFlagTokenized(argsOnly, flag, /*skipFirst=*/false);
}

// The one answer to "was this a logon launch", shared by the program and the update
// helper so the two cannot drift. It reads the command line itself: the answer is only
// defined for the full form, and letting a caller hand in a string is how the wrong
// form got in before.
bool IsAutostartLaunch() {
    const wchar_t* commandLine = GetCommandLineW();
    if (commandLine == nullptr) return false;
    const std::wstring line(commandLine);
    return CommandLineHasFlag(line, L"-autostart") || CommandLineHasFlag(line, L"/autostart");
}

int RunHidden(const std::wstring& cmdline, std::wstring* out, DWORD timeoutMs) {
    SECURITY_ATTRIBUTES sa = {};
    sa.nLength = sizeof(sa);
    sa.bInheritHandle = TRUE;

    HANDLE readEnd = nullptr;
    HANDLE writeEnd = nullptr;
    if (out) {
        if (!CreatePipe(&readEnd, &writeEnd, &sa, 0))
            out = nullptr;
        else
            SetHandleInformation(readEnd, HANDLE_FLAG_INHERIT, 0);
    }

    std::vector<wchar_t> mutableCmd(cmdline.begin(), cmdline.end());
    mutableCmd.push_back(L'\0');

    STARTUPINFOW si = {};
    si.cb = sizeof(si);
    si.dwFlags = STARTF_USESHOWWINDOW;
    si.wShowWindow = SW_HIDE;
    if (out) {
        si.dwFlags |= STARTF_USESTDHANDLES;
        si.hStdOutput = writeEnd;
        si.hStdError = writeEnd;
        si.hStdInput = GetStdHandle(STD_INPUT_HANDLE);
    }

    PROCESS_INFORMATION pi = {};
    const BOOL started =
        CreateProcessW(nullptr, mutableCmd.data(), nullptr, nullptr, out ? TRUE : FALSE,
                       CREATE_NO_WINDOW, nullptr, nullptr, &si, &pi);
    if (writeEnd) CloseHandle(writeEnd);
    if (!started) {
        if (readEnd) CloseHandle(readEnd);
        return -1;
    }

    // Drain the child's output on a background thread so a chatty child cannot
    // fill the pipe and deadlock against our own timeout wait. The thread ends
    // when the write end closes, i.e. once the child exits or is terminated.
    std::string captured;
    std::thread reader;
    if (out && readEnd) {
        reader = std::thread([readEnd, &captured] {
            char buf[4096];
            DWORD n = 0;
            while (ReadFile(readEnd, buf, sizeof(buf), &n, nullptr) && n > 0)
                captured.append(buf, n);
        });
    }

    const DWORD waitResult = WaitForSingleObject(pi.hProcess, timeoutMs);
    if (waitResult == WAIT_TIMEOUT) {
        LOGW(L"Command timed out; terminating: " + cmdline);
        Process::KillTree(pi.dwProcessId);
        WaitForSingleObject(pi.hProcess, 5000);
    }

    if (reader.joinable()) reader.join();
    if (readEnd) CloseHandle(readEnd);

    if (out) *out = DecodeConsoleOutput(captured);

    DWORD code = 0;
    GetExitCodeProcess(pi.hProcess, &code);
    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    return (waitResult == WAIT_TIMEOUT) ? -2 : static_cast<int>(code);
}

}  // namespace Command
