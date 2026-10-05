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

#include "updater/module.h"

#include <windows.h>

#include <string>
#include <vector>

#include "app/logging.h"
#include "app/paths.h"
#include "updater/plan.h"
#include "updater/version.h"

namespace UpdaterModule {
namespace {

// The resource id of the embedded module, matching APP_RCDATA_UPDATER in app.rc.in.
//
// Declared here rather than in EmbeddedText's enum, where it would have been alongside
// the agreement documents. That module reads TEXT and strips a byte-order mark on the
// way out; a BOM strip applied to a PE image would shift every byte of it. The id lives
// with the only code that reads it, and the .rc is the other half of the pair.
constexpr int kUpdaterModuleResource = 200;

// The embedded module's image, byte for byte.
//
// No text interpretation of any kind: this is a PE image, and the only correct
// treatment is to hand back exactly what the resource holds.
std::vector<char> ReadModuleImage() {
    HMODULE self = GetModuleHandleW(nullptr);
    HRSRC found = FindResourceW(self, MAKEINTRESOURCEW(kUpdaterModuleResource), RT_RCDATA);
    if (!found) return {};
    HGLOBAL loaded = LoadResource(self, found);
    if (!loaded) return {};
    const auto* bytes = static_cast<const char*>(LockResource(loaded));
    const DWORD size = SizeofResource(self, found);
    if (!bytes || size == 0) return {};
    return std::vector<char>(bytes, bytes + size);
}

// The extracted module's name: a fixed prefix plus the module's own version.
//
// The version is in the name so that two builds of the module never collide on disk.
// It is not a random name like the work order's, and does not need to be: the directory
// it lands in is writable only by administrators and SYSTEM, so there is no one to hide
// the name from. What the name must do is keep a module that is mid-flight — the one a
// running updater was started from — from being confused with the one this build wants
// to start.
std::wstring ModuleFileName() {
    return L"updater-" UPDATER_VERSION_NUM L".exe";
}

// Quote one argument for a CreateProcessW command line.
//
// This is not shell quoting; it is the documented algorithm for the single command line
// the Win32 process API takes (backslash-doubling before a quote, embedded quotes
// escaped). Doing it here means callers pass a list of arguments and cannot produce an
// ambiguous line by concatenation.
std::wstring QuoteArgument(const std::wstring& arg) {
    std::wstring out = L"\"";

    size_t backslashes = 0;
    for (const wchar_t c : arg) {
        if (c == L'\\') {
            ++backslashes;
            continue;
        }
        if (c == L'"') {
            // Backslashes before a quote are doubled, and the quote is escaped.
            out.append(backslashes * 2 + 1, L'\\');
            out.push_back(L'"');
            backslashes = 0;
            continue;
        }
        out.append(backslashes, L'\\');
        backslashes = 0;
        out.push_back(c);
    }
    // Trailing backslashes are doubled so they cannot escape the closing quote.
    out.append(backslashes * 2, L'\\');
    out.push_back(L'"');
    return out;
}

}  // namespace

bool Launch(const std::vector<std::wstring>& args) {
    std::vector<char> image = ReadModuleImage();
    if (image.empty()) {
        LOGE(L"Updater: the embedded module is missing from this build.");
        return false;
    }

    const std::wstring dir = Updater::PlanDirectory();
    if (dir.empty()) {
        LOGE(L"Updater: the module directory could not be secured; refusing to extract.");
        return false;
    }
    const std::wstring modulePath = dir + L"\\" + ModuleFileName();

    // Write it if it is not already there. CREATE_ALWAYS is wrong here — it would
    // truncate a file another process may be executing — so the existence check comes
    // first, and the exclusive create closes the remaining race, which is harmless when
    // it fires because the winner wrote the same bytes.
    const DWORD attrs = GetFileAttributesW(modulePath.c_str());
    if (attrs == INVALID_FILE_ATTRIBUTES) {
        HANDLE file = CreateFileW(modulePath.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW,
                                  FILE_ATTRIBUTE_NORMAL, nullptr);
        if (file == INVALID_HANDLE_VALUE && GetLastError() != ERROR_ALREADY_EXISTS) {
            LOGE(L"Updater: cannot write the module " + modulePath + L" (err " +
                 std::to_wstring(GetLastError()) + L").");
            return false;
        }
        if (file != INVALID_HANDLE_VALUE) {
            DWORD written = 0;
            const bool wrote = WriteFile(file, image.data(), static_cast<DWORD>(image.size()),
                                         &written, nullptr) &&
                               written == image.size();
            if (wrote) FlushFileBuffers(file);
            CloseHandle(file);
            if (!wrote) {
                DeleteFileW(modulePath.c_str());
                LOGE(L"Updater: incomplete write of the module " + modulePath + L".");
                return false;
            }
        }
    }

    std::wstring command = QuoteArgument(modulePath);
    for (const std::wstring& arg : args) command += L" " + QuoteArgument(arg);

    std::vector<wchar_t> mutableCommand(command.begin(), command.end());
    mutableCommand.push_back(L'\0');

    STARTUPINFOW si = {};
    si.cb = sizeof(si);
    si.dwFlags = STARTF_USESHOWWINDOW;
    si.wShowWindow = SW_HIDE;

    PROCESS_INFORMATION pi = {};
    // No job object: this module exists to act after this program is gone, so tying its
    // lifetime to ours would defeat it. Its working directory is the module directory,
    // never the install tree, so nothing it starts inherits a handle into the folder it
    // is about to delete or replace.
    const BOOL started =
        CreateProcessW(modulePath.c_str(), mutableCommand.data(), nullptr, nullptr, FALSE,
                       CREATE_NO_WINDOW, nullptr, dir.c_str(), &si, &pi);
    if (!started) {
        LOGE(L"Updater: cannot start the module " + modulePath + L" (err " +
             std::to_wstring(GetLastError()) + L").");
        return false;
    }
    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    return true;
}

void SweepStaleCopies() {
    const std::wstring dir = Updater::PlanDirectory();
    if (dir.empty()) return;

    const std::wstring pattern = dir + L"\\updater-*.exe";
    WIN32_FIND_DATAW find = {};
    HANDLE handle = FindFirstFileW(pattern.c_str(), &find);
    if (handle == INVALID_HANDLE_VALUE) return;

    const std::wstring keep = ModuleFileName();
    do {
        if (find.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) continue;
        if (keep == find.cFileName) continue;
        // A copy still executing cannot be deleted; that is expected, not a failure.
        if (DeleteFileW((dir + L"\\" + find.cFileName).c_str()))
            LOGI(L"Updater: removed a stale module copy " + std::wstring(find.cFileName));
    } while (FindNextFileW(handle, &find));
    FindClose(handle);
}

}  // namespace UpdaterModule
