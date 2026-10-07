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

#include "platform/command.h"

#include <string>
#include <vector>

#include "app/logging.h"
#include "platform/process.h"

namespace Command {
namespace {

// Tokenize and search for flag. skipFirst: whether argv[0] is program name.
bool HasFlagTokenized(const std::wstring& line, const std::wstring& flag, bool skipFirst) {
    if (line.empty() || flag.empty()) return false;

    int argc = 0;
    LPWSTR* argv = CommandLineToArgvW(line.c_str(), &argc);
    if (!argv) {
        // Tokenizer failed on non-empty input: conservatively return false.
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

// Check if launched via autostart. Reads GetCommandLineW() directly to ensure
// correct form (full command line, not lpCmdLine).
bool IsAutostartLaunch() {
    const wchar_t* commandLine = GetCommandLineW();
    if (commandLine == nullptr) return false;
    const std::wstring line(commandLine);
    return CommandLineHasFlag(line, L"-autostart") || CommandLineHasFlag(line, L"/autostart");
}

int RunHidden(const std::wstring& cmdline, DWORD timeoutMs) {
    std::vector<wchar_t> mutableCmd(cmdline.begin(), cmdline.end());
    mutableCmd.push_back(L'\0');

    STARTUPINFOW si = {};
    si.cb = sizeof(si);
    si.dwFlags = STARTF_USESHOWWINDOW;
    si.wShowWindow = SW_HIDE;

    PROCESS_INFORMATION pi = {};
    if (!CreateProcessW(nullptr, mutableCmd.data(), nullptr, nullptr, FALSE, CREATE_NO_WINDOW,
                        nullptr, nullptr, &si, &pi)) {
        return -1;
    }

    const DWORD waitResult = WaitForSingleObject(pi.hProcess, timeoutMs);
    if (waitResult == WAIT_TIMEOUT) {
        LOGW(L"Command timed out; terminating: " + cmdline);
        Process::KillTree(pi.dwProcessId);
        WaitForSingleObject(pi.hProcess, 5000);
    }

    DWORD code = 0;
    GetExitCodeProcess(pi.hProcess, &code);
    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    return (waitResult == WAIT_TIMEOUT) ? -2 : static_cast<int>(code);
}

}  // namespace Command
