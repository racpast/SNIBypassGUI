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

#include "app/logging.h"

#include <windows.h>

#include <shlobj.h>

#include <atomic>
#include <cstdio>
#include <cwchar>
#include <iterator>

#include "app/paths.h"
#include "app/settings.h"
#include "app/text.h"

namespace {

// Trivially destructible types only (SRWLOCK, array, atomic).
// Prevents static destruction order issues when logging after main().
SRWLOCK g_lock = SRWLOCK_INIT;
wchar_t g_logPath[MAX_PATH * 2] = {};
std::atomic<bool> g_enabled{false};

void StoreLogPath(const std::wstring& path) {
    AcquireSRWLockExclusive(&g_lock);
    const size_t n =
        (path.size() < std::size(g_logPath) - 1) ? path.size() : std::size(g_logPath) - 1;
    std::wmemcpy(g_logPath, path.c_str(), n);
    g_logPath[n] = L'\0';
    ReleaseSRWLockExclusive(&g_lock);
}

HANDLE OpenForAppend() {
    return CreateFileW(g_logPath, FILE_APPEND_DATA, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr,
                       OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
}

// Create log directory only when needed (avoids empty folders for non-logging users).
// Handles deletion by cache cleanup mid-run.
//
// Called with g_lock held, since it reads g_logPath.
HANDLE OpenLogFile() {
    HANDLE file = OpenForAppend();
    if (file != INVALID_HANDLE_VALUE) return file;

    // Only a missing directory is worth a second attempt. A denied open, a path that
    // is not a directory, a full disk — retrying those would fail identically.
    if (GetLastError() != ERROR_PATH_NOT_FOUND) return INVALID_HANDLE_VALUE;

    const std::wstring path(g_logPath);
    const size_t slash = path.find_last_of(L'\\');
    if (slash == std::wstring::npos) return INVALID_HANDLE_VALUE;
    SHCreateDirectoryExW(nullptr, path.substr(0, slash).c_str(), nullptr);

    return OpenForAppend();
}

}  // namespace

// Resolves where the log goes and reads whether anyone wants one. Nothing is created
// and nothing is opened: see OpenLogFile for why the directory is left until there is
// actually a line to put in it.
void LogInit() {
    StoreLogPath(ExeDir() + L"logs\\snibypassgui.log");
    g_enabled.store(LoggingEnabled(), std::memory_order_relaxed);
}

bool LogEnabled() {
    return g_enabled.load(std::memory_order_relaxed);
}

void LogSetEnabled(bool on) {
    SetLoggingEnabled(on);
    g_enabled.store(on, std::memory_order_relaxed);
}

void LogLine(const std::wstring& level, const std::wstring& msg) {
    if (!g_enabled.load(std::memory_order_relaxed)) return;

    SYSTEMTIME now;
    GetLocalTime(&now);
    wchar_t stamp[64] = L"";
    if (std::swprintf(stamp, std::size(stamp), L"%04d-%02d-%02d %02d:%02d:%02d", now.wYear,
                      now.wMonth, now.wDay, now.wHour, now.wMinute, now.wSecond) < 0)
        stamp[0] = L'\0';

    const std::string line =
        WideToUtf8(L"[" + std::wstring(stamp) + L"] [" + level + L"] " + msg + L"\r\n");

    AcquireSRWLockExclusive(&g_lock);
    if (g_logPath[0] != L'\0') {
        HANDLE file = OpenLogFile();
        if (file != INVALID_HANDLE_VALUE) {
            DWORD written = 0;
            WriteFile(file, line.data(), static_cast<DWORD>(line.size()), &written, nullptr);
            CloseHandle(file);
        }
    }
    ReleaseSRWLockExclusive(&g_lock);
}
