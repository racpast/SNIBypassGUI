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
#include <cwchar>
#include <string>
#include <tuple>
#include <utility>
#include <vector>

enum class Lang { English, Chinese };

// Resolved from [General] Language in config.ini, falling back to the OS UI language.
Lang GetLang();
void SetLang(Lang l);

// Translate a stable string key to the current language. An unknown key is
// returned verbatim, so a missing entry degrades to a visible identifier rather
// than to empty text.
const wchar_t* T(const wchar_t* key);

// ---------------------------------------------------------------------------
// Lists in user-facing text.
//
// A bulleted list of items is built here rather than at each call site, and there
// are two reasons for that beyond not writing the same loop twice.
//
// The bullet is a translated glyph, not a constant. "•" is what this project uses
// in both languages, but the separator for a comma-joined list is not: English
// takes ", " and Chinese takes "、", and a call site that decided for itself would
// get one of them wrong. So the join is a table entry too.
//
// The truncation is a policy, and it has to be one policy. Lists that grow with
// the user's data — the files an update will replace, the ports something else is
// holding — are capped with a "... and N more" line, and the cap belongs with the
// rendering rather than with each caller, which is how two of them ended up
// truncating at different counts.
//
// Items are NOT translated by this function: a caller passes text it has already
// resolved, because whether an item is a translated phrase or a file path is
// something only the caller knows.
// ---------------------------------------------------------------------------

// Items a capped list shows before it stops.
//
// One value for every list in the program, because they appear in the same dialogs
// and a reader who sees one shorten at eight and the next at twelve learns nothing
// from either. It was two local constants in two files before, which is exactly how
// they came to disagree.
inline constexpr size_t kMaxListItems = 8;

// `items` as a bulleted list, one per line, with no trailing newline.
//
// `maxShown` of 0 shows every item. Otherwise at most that many are listed and the
// remainder is replaced by a single "… and N more" line, which is itself translated
// and counted rather than described. The default is the program's one cap.
std::wstring BulletList(const std::vector<std::wstring>& items,
                        size_t maxShown = kMaxListItems);

// ---------------------------------------------------------------------------
// Formatting a translated string.
//
// A translated sentence cannot be assembled by concatenation: word order is the
// translator's to choose, and the place a value belongs in an English sentence is
// not where it belongs in a Chinese one. So a template carries printf specifiers —
// "about %s remaining" / "约剩余 %s" — and the value is substituted only after the
// language has been chosen.
//
// The specifiers these templates use are the project's own closed set, not an
// arbitrary printf dialect. An argument is converted to exactly the type its
// specifier expects, and a type with no conversion is a compile error at the call
// site rather than undefined behaviour inside printf:
//
//   %s    std::wstring, const wchar_t*, or a wide string literal
//   %d    int
//   %u    unsigned int
//   %llu  unsigned long long
//   %lld  long long
//   %.1f  double
//
// `%s` being the wide conversion is what this toolchain gives: MinGW-w64 defines
// __USE_MINGW_ANSI_STDIO=1, so its printf family follows C99, where the wide
// function's %s takes wchar_t*. The static_assert below pins that, so a toolchain
// that ever dropped it would fail the build rather than start printing junk.
//
// Signatures are not checked at compile time, and cannot be: the format string is
// chosen at run time from the table, once the language is known. That is why the
// set above is small and stated, why every conversion is explicit, and why the
// templates that use one have a test over them.
static_assert(__USE_MINGW_ANSI_STDIO != 0,
              "The i18n templates rely on C99 wide printf semantics: %s takes wchar_t*.");

namespace I18nDetail {

// How one argument type maps to the type its specifier expects. The primary
// template is declared and never defined, so an unsupported argument type is a
// compile error at the call site — "incomplete type Arg<X> used in nested name
// specifier" — which names the type, rather than a printf that would read the
// argument as the wrong width.
template <typename T>
struct Arg;

template <>
struct Arg<int> {
    static int Get(int v) { return v; }
};
template <>
struct Arg<unsigned int> {
    static unsigned int Get(unsigned int v) { return v; }
};
template <>
struct Arg<long long> {
    static long long Get(long long v) { return v; }
};
template <>
struct Arg<unsigned long long> {
    static unsigned long long Get(unsigned long long v) { return v; }
};
template <>
struct Arg<long> {
    static long Get(long v) { return v; }
};
template <>
struct Arg<unsigned long> {
    static unsigned long Get(unsigned long v) { return v; }
};
template <>
struct Arg<double> {
    static double Get(double v) { return v; }
};
// The string cases converge on const wchar_t*. The owner is borrowed, and by the
// time this is called it is a temporary that lives until the end of the enclosing
// full-expression — which is longer than the formatting call that reads it.
template <>
struct Arg<std::wstring> {
    static const wchar_t* Get(const std::wstring& v) { return v.c_str(); }
};
template <>
struct Arg<const wchar_t*> {
    static const wchar_t* Get(const wchar_t* v) { return v; }
};
template <int N>
struct Arg<const wchar_t[N]> {
    static const wchar_t* Get(const wchar_t* v) { return v; }
};

// Grow the buffer until the whole result fits, then return it.
//
// Two passes would be the natural shape — ask for the length, then fill a buffer
// of it — and it is not available here. A C99 printf is supposed to support the
// "measure" call (a null buffer, a zero count) by returning the length the result
// WOULD need, and MinGW's swprintf does not: it returns -1 and prints nothing.
// Worse, its ordinary call does not report overflow either. Both were measured
// rather than assumed, and both are exercised by the tests below:
//
//   swprintf(buf, 256, L"big: %s", three_thousand_chars)   ->    6  (silently clipped)
//   _snwprintf(buf, 256, L"big: %s", three_thousand_chars) ->   -1  (overflow reported)
//   _snwprintf(buf, 8192, L"big: %s", three_thousand_chars)-> 3005  (what it needs)
//
// A silent clip is the one failure this cannot tolerate: the result would look
// like a finished string, be stored and shown as one, and every call site would
// be left believing it had been handed the whole sentence. So the sizing is
// driven by _snwprintf, whose -1 is what distinguishes "grow" from "done".
inline constexpr size_t kMaxFormatted = 4096;

// A starting point that fits every template in the table today, so the ordinary
// call formats once and returns.
inline constexpr size_t kInitialFormatted = 256;

}  // namespace I18nDetail

// Translate `key` and substitute `args`. An unknown key is passed through with
// whatever specifiers it contains intact.
//
//     TFmt(L"punct.andMore", 3)              -> "… and 3 more"
//     TFmt(L"msg.updEtaRemaining", duration)   -> "about 2 min remaining"
template <typename... Args>
std::wstring TFmt(const wchar_t* key, const Args&... args) {
    const wchar_t* format = T(key);

    // Converted into locals first, so the buffer growth below re-reads the same
    // objects rather than reconstructing them, and so the string owners outlive
    // every attempt.
    const auto conversion = std::make_tuple(I18nDetail::Arg<Args>::Get(args)...);

    size_t capacity = I18nDetail::kInitialFormatted;
    for (;;) {
        std::wstring out(capacity, L'\0');
        const int written = std::apply(
            [format, &out](const auto&... v) {
                // The count is the buffer's own length, and _snwprintf writes at
                // most that many characters plus a terminator — so the terminator
                // always has room, and the resize below is what drops it.
                return _snwprintf(out.data(), out.size(), format, v...);
            },
            conversion);

        if (written >= 0) {
            out.resize(static_cast<size_t>(written));
            return out;
        }
        // Too small, or a specifier printf rejects. Growing distinguishes the two:
        // a too-small buffer fits at some larger size, a broken template never
        // does — and that one returns the template, so the defect is visible in
        // the string rather than silent as an empty one.
        if (capacity >= I18nDetail::kMaxFormatted) return std::wstring(format);
        capacity *= 2;
    }
}
