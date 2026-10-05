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

#include "app/text.h"

#include <windows.h>

#include <iterator>
#include <vector>

std::string WideToUtf8(const std::wstring& w) {
    if (w.empty()) return {};
    int n = WideCharToMultiByte(CP_UTF8, 0, w.c_str(), static_cast<int>(w.size()), nullptr, 0,
                                nullptr, nullptr);
    std::string s(static_cast<size_t>(n), '\0');
    WideCharToMultiByte(CP_UTF8, 0, w.c_str(), static_cast<int>(w.size()), s.data(), n, nullptr,
                        nullptr);
    return s;
}

std::wstring Utf8ToWide(const std::string& s) {
    if (s.empty()) return {};
    int n = MultiByteToWideChar(CP_UTF8, 0, s.c_str(), static_cast<int>(s.size()), nullptr, 0);
    std::wstring w(static_cast<size_t>(n), L'\0');
    MultiByteToWideChar(CP_UTF8, 0, s.c_str(), static_cast<int>(s.size()), w.data(), n);
    return w;
}

std::wstring TrimW(const std::wstring& s) {
    size_t a = s.find_first_not_of(L" \t\r\n");
    if (a == std::wstring::npos) return L"";
    size_t b = s.find_last_not_of(L" \t\r\n");
    return s.substr(a, b - a + 1);
}

// Ordinal, locale-independent case folding.
//
// Every caller of this function compares a path, an argument or a filename, and for
// those the question is only ever "do these name the same thing?" — which is an
// ordinal question with a fixed answer, not a linguistic one that may vary by
// language, thread locale or Windows version.
//
// std::towlower is the wrong tool on both counts. It folds according to the C
// locale's LC_CTYPE, so what it returns depends on process state this program never
// sets today but could acquire from any later setlocale(LC_ALL, "") — under a Turkish
// locale "II" and "ii" would stop comparing equal and an image-path match would
// silently miss. It also folds nothing outside that locale's alphabet, leaving
// non-ASCII paths compared case-sensitively.
//
// LCMapStringEx with LOCALE_NAME_INVARIANT and LCMAP_LOWERCASE is defined to produce
// the same result for every user and every machine: it folds the full Unicode range
// this build maps, and no caller has to think about locale to know what it gets.
std::wstring LowerW(std::wstring s) {
    if (s.empty()) return s;

    const int needed =
        LCMapStringEx(LOCALE_NAME_INVARIANT, LCMAP_LOWERCASE, s.c_str(),
                      static_cast<int>(s.size()), nullptr, 0, nullptr, nullptr, 0);
    if (needed <= 0) return s;  // cannot happen with a valid argument; keep the input

    std::vector<wchar_t> out(static_cast<size_t>(needed) + 1, L'\0');
    const int written =
        LCMapStringEx(LOCALE_NAME_INVARIANT, LCMAP_LOWERCASE, s.c_str(),
                      static_cast<int>(s.size()), out.data(), needed, nullptr, nullptr, 0);
    if (written <= 0) return s;
    return std::wstring(out.data(), static_cast<size_t>(written));
}
