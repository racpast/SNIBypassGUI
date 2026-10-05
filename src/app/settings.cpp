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

#include "app/settings.h"

#include <windows.h>

#include <mutex>
#include <vector>

#include "app/paths.h"
#include "app/text.h"
#include "platform/ini.h"

namespace {

constexpr wchar_t kSection[] = L"General";

// One file's worth of parsed settings, kept until the file changes underneath it.
//
// Every accessor below used to parse config.ini from scratch, and the menu asks for
// several of these on every right-click — so opening the tray menu meant reading and
// decoding the whole file once per setting. i18n.cpp had already solved this for the
// language key, and its comment records why; this is the same fix for the rest.
//
// Re-reading is not simply avoided, because config.ini is a file the user is expected
// to edit by hand (settings.h says so). So the cache is keyed on the file's size and
// write time: an edit made in Notepad between two calls is noticed, and an unchanged
// file is parsed once. That is the same invalidation supported_sites.cpp uses for its
// data file, for the same reason — a hand-editable file that a program reads often.
//
// Not thread-safe, and does not need to be: the settings are read from the tray (UI
// thread), the tray's command threads and the DNS supervisor. The lock below is what
// makes that true rather than an assumption.
struct Stamp {
    long long size = -1;
    long long mtime = -1;
    bool known = false;

    bool operator==(const Stamp& o) const {
        return size == o.size && mtime == o.mtime && known == o.known;
    }
};

std::mutex g_cacheMutex;
Stamp g_cachedStamp;
std::vector<Ini::Section> g_cachedSections;

Stamp StampOf(const std::wstring& path) {
    Stamp stamp;
    WIN32_FILE_ATTRIBUTE_DATA data = {};
    if (!GetFileAttributesExW(path.c_str(), GetFileExInfoStandard, &data)) return stamp;
    stamp.size =
        static_cast<long long>((static_cast<unsigned long long>(data.nFileSizeHigh) << 32u) |
                               static_cast<unsigned long long>(data.nFileSizeLow));
    stamp.mtime = static_cast<long long>(
        (static_cast<unsigned long long>(data.ftLastWriteTime.dwHighDateTime) << 32u) |
        static_cast<unsigned long long>(data.ftLastWriteTime.dwLowDateTime));
    stamp.known = true;
    return stamp;
}

// The parsed settings, from the cache when the file has not moved since last time.
//
// Returned BY VALUE rather than by reference. A reference would hand the caller a view
// into a global that a different thread can reassign the moment this returns — the
// lock protects the swap, not the use that follows it — and several accessors below do
// spread across threads. A copy of a few small structs is nothing next to the file
// read and decode it replaces.
std::vector<Ini::Section> Sections() {
    const std::wstring path = SettingsPath();
    const Stamp stamp = StampOf(path);

    std::lock_guard<std::mutex> lock(g_cacheMutex);
    if (g_cachedStamp == stamp && g_cachedStamp.known) return g_cachedSections;

    // Parsed while the lock is held. Reading a small INI is far cheaper than the
    // contention of a second lock, and this runs once per actual change.
    g_cachedSections = Ini::Read(path);
    g_cachedStamp = stamp;
    return g_cachedSections;
}

// Drop the cache so the next read goes to disk. Called by every writer: the profile
// API updates the file's size or time, but a write that happened to leave both
// unchanged would otherwise be invisible to the readers below.
void Invalidate() {
    std::lock_guard<std::mutex> lock(g_cacheMutex);
    g_cachedStamp = Stamp{};
    g_cachedSections.clear();
}

bool ReadFlag(const wchar_t* key) {
    return Ini::Int(Sections(), kSection, key, 0) != 0;
}

void WriteFlag(const wchar_t* key, bool on) {
    WritePrivateProfileStringW(kSection, key, on ? L"1" : L"0", SettingsPath().c_str());
    Invalidate();
}

}  // namespace

std::wstring SettingsPath() {
    return ExeDir() + L"config.ini";
}

bool LoggingEnabled() {
    return ReadFlag(L"LoggingEnabled");
}
void SetLoggingEnabled(bool on) {
    WriteFlag(L"LoggingEnabled", on);
}

// The stored value is the SHA-256 (lowercase hex) of the agreement text that was
// accepted. An absent or empty key means "not accepted", which is also what an
// install that predates this key reports — see the note in settings.h for why that
// is the intended behavior rather than a bug to migrate around.
bool EulaAccepted(const std::string& textHash) {
    if (textHash.empty()) return false;
    const std::wstring stored = Ini::Value(Sections(), kSection, L"EulaAcceptedHash");
    return LowerW(stored) == Utf8ToWide(textHash);
}
void SetEulaAccepted(const std::string& textHash) {
    WritePrivateProfileStringW(kSection, L"EulaAcceptedHash",
                               textHash.empty() ? L"" : Utf8ToWide(textHash).c_str(),
                               SettingsPath().c_str());
    Invalidate();
}

bool AutoUpdateEnabled() {
    return ReadFlag(L"AutoUpdate");
}
void SetAutoUpdateEnabled(bool on) {
    WriteFlag(L"AutoUpdate", on);
}

// Stored as a word rather than 0/1 because the preference is genuinely
// tri-state: an absent key ("never asked") must not read as "declined". An
// unknown value falls back to Unset, so a hand-edited config.ini degrades to
// asking again rather than to a silently assumed decision.
ShortcutPref GetShortcutPref() {
    const std::wstring v = LowerW(Ini::Value(SettingsPath(), kSection, L"DesktopShortcut"));
    if (v == L"wanted") return ShortcutPref::Wanted;
    if (v == L"declined") return ShortcutPref::Declined;
    return ShortcutPref::Unset;
}

void SetShortcutPref(ShortcutPref p) {
    const wchar_t* v = (p == ShortcutPref::Wanted)     ? L"wanted"
                       : (p == ShortcutPref::Declined) ? L"declined"
                                                       : L"";
    WritePrivateProfileStringW(kSection, L"DesktopShortcut", v, SettingsPath().c_str());
}
