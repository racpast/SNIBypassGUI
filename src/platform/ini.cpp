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

#include "platform/ini.h"

#include <windows.h>

#include <cerrno>
#include <climits>
#include <cstdlib>
#include <utility>

#include "app/logging.h"
#include "app/text.h"

namespace Ini {
namespace {

// Ordinal, case-insensitive equality for a key or section name.
//
// Names here are identifiers, not prose, so the comparison must not depend on the
// locale: it uses the ordinal API rather than a lowercase-then-compare, which would
// fold differently under a Turkish locale. This is the same property LowerW provides
// for paths, obtained with the narrower tool.
bool EqualsNoCase(const std::wstring& a, const std::wstring& b) {
    return a.size() == b.size() &&
           CompareStringOrdinal(a.c_str(), static_cast<int>(a.size()), b.c_str(),
                                static_cast<int>(b.size()), TRUE) == CSTR_EQUAL;
}

// Read the whole file, decoded to UTF-16.
//
// A byte order mark decides first, because it is the author's explicit statement of
// the encoding. Without one, UTF-8 is attempted strictly: MultiByteToWideChar without
// MB_ERR_INVALID_CHARS maps arbitrary bytes to U+FFFD and reports success, so a file in
// the machine's ANSI code page would decode to replacement characters and the ANSI
// fallback below would never run. The ANSI code page is the last resort, which is what
// the profile API uses for a file without a mark.
bool ReadFileAsWide(const std::wstring& file, std::wstring& out) {
    HANDLE handle = CreateFileW(file.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
                                nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (handle == INVALID_HANDLE_VALUE) return false;

    LARGE_INTEGER size = {};
    // An INI this large is not a configuration file; refusing keeps a hostile or
    // corrupt file from asking for an unbounded read.
    constexpr long long kMaxFileBytes = 64LL * 1024 * 1024;
    if (!GetFileSizeEx(handle, &size) || size.QuadPart <= 0 || size.QuadPart > kMaxFileBytes) {
        CloseHandle(handle);
        return false;
    }

    std::vector<char> bytes(static_cast<size_t>(size.QuadPart));
    DWORD read = 0;
    const bool ok =
        ReadFile(handle, bytes.data(), static_cast<DWORD>(bytes.size()), &read, nullptr) != 0;
    CloseHandle(handle);
    if (!ok || read == 0) return false;
    bytes.resize(read);

    const auto* p = reinterpret_cast<const unsigned char*>(bytes.data());
    const size_t n = bytes.size();

    if (n >= 2 && p[0] == 0xFF && p[1] == 0xFE) {  // UTF-16LE
        // Decoded byte by byte rather than by reinterpreting the buffer: a char array
        // carries no alignment promise, and reading a wchar_t out of it would be
        // undefined behavior on a stricter compiler.
        const size_t count = (n - 2) / 2;
        out.resize(count);
        for (size_t i = 0; i < count; ++i)
            out[i] = static_cast<wchar_t>(static_cast<unsigned>(p[2 + i * 2]) |
                                          (static_cast<unsigned>(p[3 + i * 2]) << 8u));
        return true;
    }
    if (n >= 2 && p[0] == 0xFE && p[1] == 0xFF) {  // UTF-16BE
        const size_t count = (n - 2) / 2;
        out.resize(count);
        for (size_t i = 0; i < count; ++i)
            out[i] = static_cast<wchar_t>((static_cast<unsigned>(p[2 + i * 2]) << 8u) |
                                          static_cast<unsigned>(p[3 + i * 2]));
        return true;
    }

    const char* text = bytes.data();
    size_t textBytes = n;
    if (n >= 3 && p[0] == 0xEF && p[1] == 0xBB && p[2] == 0xBF) {  // UTF-8
        text += 3;
        textBytes -= 3;
    }

    const int len = static_cast<int>(textBytes);
    UINT codePage = CP_UTF8;
    DWORD flags = MB_ERR_INVALID_CHARS;
    int wide = MultiByteToWideChar(codePage, flags, text, len, nullptr, 0);
    if (wide <= 0) {
        codePage = CP_ACP;
        flags = 0;
        wide = MultiByteToWideChar(codePage, flags, text, len, nullptr, 0);
    }
    if (wide <= 0) return false;

    out.resize(static_cast<size_t>(wide));
    MultiByteToWideChar(codePage, flags, text, len, out.data(), wide);
    return true;
}

// One logical line without its terminator. CRLF, LF and a lone CR all end a line.
bool NextLine(const std::wstring& text, size_t& pos, std::wstring& line) {
    if (pos >= text.size()) return false;
    const size_t start = pos;
    while (pos < text.size() && text[pos] != L'\n' && text[pos] != L'\r') ++pos;
    line.assign(text, start, pos - start);
    if (pos < text.size()) {
        if (text[pos] == L'\r' && pos + 1 < text.size() && text[pos + 1] == L'\n')
            pos += 2;
        else
            ++pos;
    }
    return true;
}

void Add(std::vector<Section>& sections, const std::wstring& key, const std::wstring& value) {
    if (sections.empty()) return;  // a key before any section header has no section
    Section& section = sections.back();
    for (const auto& entry : section.entries)
        // First value wins, which is the one the profile API returns for a repeated key.
        if (EqualsNoCase(entry.first, key)) return;
    section.entries.emplace_back(key, value);
}

}  // namespace

std::vector<Section> Read(const std::wstring& file) {
    std::vector<Section> sections;

    std::wstring text;
    if (!ReadFileAsWide(file, text)) return sections;

    // A byte order mark that survived decoding: a UTF-16LE file keeps it as its first
    // character, since the mark is data at that point rather than something the decoder
    // consumed. Left in place, it would make "[Paths]" parse as "[Paths]" with an
    // invisible leading character, and every lookup would miss.
    if (!text.empty() && text[0] == 0xFEFF) text.erase(0, 1);

    size_t pos = 0;
    std::wstring line;
    while (NextLine(text, pos, line)) {
        const std::wstring trimmed = TrimW(line);
        if (trimmed.empty()) continue;

        if (trimmed[0] == L'[') {
            const size_t close = trimmed.find(L']');
            if (close == std::wstring::npos) continue;  // malformed header; ignore the line
            Section section;
            section.name = TrimW(trimmed.substr(1, close - 1));
            if (!section.name.empty()) sections.push_back(std::move(section));
            continue;
        }

        // A comment only when it starts the line. A ';' or '#' inside a value is data,
        // because these values are paths and glob patterns and a comment rule applied
        // there would cut a pattern in half.
        if (trimmed[0] == L';' || trimmed[0] == L'#') continue;

        const size_t eq = trimmed.find(L'=');
        if (eq == std::wstring::npos) continue;

        const std::wstring key = TrimW(trimmed.substr(0, eq));
        if (key.empty()) continue;
        Add(sections, key, TrimW(trimmed.substr(eq + 1)));
    }
    return sections;
}

size_t SectionCount(const std::vector<Section>& sections) {
    return sections.size();
}

std::wstring Value(const std::vector<Section>& sections, const wchar_t* section,
                   const wchar_t* key, size_t maxChars) {
    if (!section || !key) return {};
    const std::wstring wantSection = TrimW(section);
    const std::wstring wantKey = TrimW(key);
    if (wantKey.empty()) return {};

    for (const Section& s : sections) {
        if (!EqualsNoCase(s.name, wantSection)) continue;
        for (const auto& entry : s.entries) {
            if (!EqualsNoCase(entry.first, wantKey)) continue;
            if (entry.second.size() > maxChars) {
                std::wstring msg;
                msg += L"[Ini] value of [";
                msg += wantSection;
                msg += L"] ";
                msg += wantKey;
                msg += L" is longer than ";
                msg += std::to_wstring(maxChars);
                msg += L" characters; rejecting it rather than returning a partial value.";
                LOGE(msg);
                return {};
            }
            return entry.second;
        }
    }
    return {};
}

std::wstring Value(const std::wstring& file, const wchar_t* section, const wchar_t* key,
                   size_t maxChars) {
    return Value(Read(file), section, key, maxChars);
}

int Int(const std::vector<Section>& sections, const wchar_t* section, const wchar_t* key,
        int fallback) {
    // Bounded well below the 32 KiB profile limit: an integer is a handful of digits, so
    // anything this long is not one and reading it is not worth the allocation.
    const std::wstring value = Value(sections, section, key, 4096);
    if (value.empty()) return fallback;

    errno = 0;
    wchar_t* end = nullptr;
    const long parsed = std::wcstol(value.c_str(), &end, 10);
    // No digits consumed, or a value outside an int — either way not an integer, which
    // is the same answer the profile API gives: the caller's default.
    if (end == value.c_str() || errno == ERANGE || parsed < INT_MIN || parsed > INT_MAX)
        return fallback;
    return static_cast<int>(parsed);
}

std::vector<std::wstring> Split(const std::wstring& value) {
    std::vector<std::wstring> out;
    size_t start = 0;
    for (;;) {
        const size_t bar = value.find(L'|', start);
        std::wstring item = TrimW(bar == std::wstring::npos ? value.substr(start)
                                                            : value.substr(start, bar - start));
        if (!item.empty()) out.push_back(std::move(item));
        if (bar == std::wstring::npos) break;
        start = bar + 1;
    }
    return out;
}

std::vector<std::wstring> List(const std::vector<Section>& sections, const wchar_t* section,
                               const wchar_t* key) {
    // An oversized value comes back empty from Value(), so a truncated list can never
    // be produced: either every item arrives in full or the list reads as absent.
    //
    // That guarantee is the reason these lists are read through this module at all
    // rather than through the profile API, and it is not cosmetic here: these lists
    // drive deletion and port claims. A list cut mid-item would be a glob that still
    // compiles and still deletes, or a port that is not the one written.
    return Split(Value(sections, section, key));
}

}  // namespace Ini
