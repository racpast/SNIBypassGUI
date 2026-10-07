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

#pragma once
#include <string>
#include <vector>

// Reading INI files without the profile API's ceiling.
//
// GetPrivateProfileStringW cannot serve a value longer than 32767 characters: at
// exactly 32768 it returns 0, and one character past that it returns a wrapped
// fragment — a short value that looks real rather than an error. Growing the buffer
// does not help, because the limit is the API's, not the caller's. For meta.ini, whose
// values are glob patterns the uninstall and cache-clean paths delete by, a wrapped
// fragment is worse than a refusal: the file's last entry becomes a prefix glob that
// still compiles and still deletes, just not what was written.
//
// Every read of a deployed INI therefore goes through here. Writing still goes through
// WritePrivateProfileStringW: writing has no such limit, and the profile API is what
// keeps the file's encoding, key order and formatting consistent when this program
// edits a file a human also edits.
//
// What this reader implements is the documented INI subset the profile API reads, so a
// file behaves the same either way:
//   - `[Section]` headers; keys and section names are case-insensitive.
//   - `key=value`, with the value's leading and trailing spaces and tabs removed.
//   - A line whose first non-blank character is ';' or '#' is a comment.
//   - A ';' or '#' inside a value is data, not a comment: these values are paths and
//     glob patterns, and cutting one at a semicolon is the failure this module exists
//     to avoid.
//   - CRLF, LF and lone-CR line endings.
//   - UTF-16LE/BE with a byte order mark, UTF-8 with or without one, and otherwise the
//     machine's ANSI code page — the same resolution the profile API applies.
namespace Ini {

// Everything under one section header, in file order. A key repeated within a section
// keeps its first value, which is what the profile API returns.
struct Section {
    std::wstring name;
    std::vector<std::pair<std::wstring, std::wstring>> entries;
};

// Parse `file`. An unreadable or oversized file yields no sections, which every
// accessor below reports as "absent" rather than as an error.
std::vector<Section> Read(const std::wstring& file);

// How many section headers the file declares. A convenience for callers that only
// need the count, so the Section vector does not have to be named at the call site.
size_t SectionCount(const std::vector<Section>& sections);

// Value of `key` under `section`, or empty when either is missing.
//
// Returns empty and logs an error if the value is longer than `maxChars` — an
// unreachable size for a real layout, so exceeding it means a corrupt or hostile file
// rather than a value to support.
std::wstring Value(const std::vector<Section>& sections, const wchar_t* section,
                   const wchar_t* key, size_t maxChars = 1024 * 1024);

// Same, reading the file itself. Use the `sections` overload when several values come
// from one file, so it is parsed once.
std::wstring Value(const std::wstring& file, const wchar_t* section, const wchar_t* key,
                   size_t maxChars = 1024 * 1024);

// ---------------------------------------------------------------------------
// Writing: the profile API, and why this module does not implement it.
//
// Every WRITE of a deployed INI goes through WritePrivateProfileStringW, and that is
// deliberate rather than an oversight. It was checked, not assumed: a hand-maintained
// file — comments, blank lines, deliberate spacing, an unrelated section, keys in an
// order the author chose — came back from that call with all of it intact and only the
// named value changed. Its readers and writers are the same pair of Windows APIs, so
// the file stays one thing to every program that opens it.
//
// An earlier note here proposed adding a writer to this module so that reads and
// writes would share a code path. The probe above is why that did not happen: what it
// would replace is a tested implementation that preserves the parts of a file a human
// wrote, and what it would add is the risk of getting quoting, whitespace, encoding or
// key replacement subtly wrong in a file this program only edits to store a handful of
// switches.
//
// What the split requires instead is that the two halves agree, and that is not left to
// inspection: TestIniWriteReadAgreement writes values with the boundary characters —
// an embedded ';', a '=', leading and trailing spaces, non-ASCII — with the profile API
// and reads each back with Value() above, requiring the exact characters to survive.
// A divergence between the two sides fails that test rather than reaching a user.
//
// So: READ with this module, because the profile API cannot serve a long value without
// silently truncating it; WRITE with the profile API, because it edits a file without
// destroying the parts of it no program owns.
// ---------------------------------------------------------------------------

// Decimal integer under `section`, or `fallback` when the key is absent or does not
// start with a number. Trailing text is ignored, as the profile API ignores it.
int Int(const std::vector<Section>& sections, const wchar_t* section, const wchar_t* key,
        int fallback);

// A '|'-separated value split into items, each trimmed, blanks dropped.
//
// Deployed INI values are short lists — the paths an uninstall removes, the ports a
// payload claims, the directories that must exist — and '|' rather than a comma is
// the convention those lists were written to. Every one of them is read by more than
// one caller: the uninstall list is walked by the uninstall and by the cache clean,
// and the port list by both the check and the cleanup. Splitting them was written
// three times before this, once per caller, which is how three copies of "trim and
// drop empties" end up disagreeing about the empties.
//
// A value with no separator yields one item, and an empty value yields none — so a
// caller never has to special-case a single-element list.
std::vector<std::wstring> List(const std::vector<Section>& sections, const wchar_t* section,
                               const wchar_t* key);

// The same, on a bare value already in hand.
std::vector<std::wstring> Split(const std::wstring& value);

}  // namespace Ini
