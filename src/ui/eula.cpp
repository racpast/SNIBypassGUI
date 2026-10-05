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

#include "ui/eula.h"

#include <cstdint>
#include <cstring>
#include <cwchar>
#include <utility>
#include <vector>

#include "app/i18n.h"
#include "app/settings.h"
#include "app/text.h"
#include "app/version.h"
#include "platform/dialogs.h"
#include "platform/embedded_text.h"
#include "update/crypto.h"

// The dialog is built from an in-memory DLGTEMPLATE rather than an .rc resource, so
// the OS still handles font, tab order, Esc and DPI scaling (dialog units scale with
// the shell font) without a static layout to keep in sync with the strings.

namespace Eula {
namespace {

enum : std::uint8_t { kIdIntro = 101, kIdText = 102 };

// Standard control class atoms.
enum : std::uint8_t { kAtomButton = 0x0080, kAtomEdit = 0x0081, kAtomStatic = 0x0082 };

// Builds a DLGTEMPLATE, honouring the DWORD alignment the format requires.
struct TemplateBuilder {
    std::vector<BYTE> buf;

    void Align() {
        while (buf.size() % sizeof(DWORD)) buf.push_back(0);
    }
    void U16(WORD v) {
        buf.insert(buf.end(), reinterpret_cast<BYTE*>(&v), reinterpret_cast<BYTE*>(&v) + 2);
    }
    void U32(DWORD v) {
        buf.insert(buf.end(), reinterpret_cast<BYTE*>(&v), reinterpret_cast<BYTE*>(&v) + 4);
    }
    void Str(const wchar_t* s) {
        const size_t n = std::wcslen(s) + 1;
        buf.insert(buf.end(), reinterpret_cast<const BYTE*>(s),
                   reinterpret_cast<const BYTE*>(s) + n * sizeof(wchar_t));
    }
    void Empty() { U16(0); }  // empty menu / class / text marker

    void AddItem(DWORD style, short x, short y, short cx, short cy, WORD id, WORD classAtom,
                 const wchar_t* text) {
        Align();
        U32(style);
        U32(0);  // extended style
        U16(static_cast<WORD>(x));
        U16(static_cast<WORD>(y));
        U16(static_cast<WORD>(cx));
        U16(static_cast<WORD>(cy));
        U16(id);
        U16(0xFFFF);
        U16(classAtom);  // class as an ordinal atom
        Str(text);
        U16(0);  // no creation data
    }
};

// In gated mode there are two buttons (Decline/Accept); in read-only mode a single
// centered Close button.
std::vector<BYTE> BuildTemplate(bool gated) {
    TemplateBuilder t;
    t.U32(WS_POPUP | WS_BORDER | WS_CAPTION | WS_SYSMENU | DS_MODALFRAME | DS_CENTER |
          DS_SETFONT);
    t.U32(0);                 // extended style
    t.U16(gated ? 4 : 3);     // control count
    t.U16(0);                 // x, ignored under DS_CENTER
    t.U16(0);                 // y, ignored under DS_CENTER
    t.U16(300);               // width in dialog units
    t.U16(220);               // height in dialog units
    t.Empty();                // no menu
    t.Empty();                // default window class
    t.Str(T(L"eula.title"));  // caption
    t.U16(9);                 // DS_SETFONT point size
    t.Str(L"Segoe UI");

    t.AddItem(WS_CHILD | WS_VISIBLE | SS_LEFT, 7, 7, 286, 18, kIdIntro, kAtomStatic,
              gated ? T(L"eula.intro") : T(L"eula.introView"));

    t.AddItem(WS_CHILD | WS_VISIBLE | WS_BORDER | WS_VSCROLL | WS_TABSTOP | ES_MULTILINE |
                  ES_READONLY | ES_AUTOVSCROLL,
              7, 28, 286, 165, kIdText, kAtomEdit, L"");

    if (gated) {
        t.AddItem(WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_PUSHBUTTON, 181, 199, 55, 15,
                  IDCANCEL, kAtomButton, T(L"eula.disagree"));
        t.AddItem(WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_DEFPUSHBUTTON, 240, 199, 55, 15, IDOK,
                  kAtomButton, T(L"eula.agree"));
    } else {
        t.AddItem(WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_DEFPUSHBUTTON, 123, 199, 55, 15, IDOK,
                  kAtomButton, T(L"eula.close"));
    }
    return std::move(t.buf);
}

INT_PTR CALLBACK DialogProc(HWND dlg, UINT msg, WPARAM wp, LPARAM lp) {
    switch (msg) {
        case WM_INITDIALOG: {
            const void* textPtr = nullptr;
            std::memcpy(reinterpret_cast<void*>(&textPtr), &lp, sizeof(textPtr));
            SetWindowTextW(GetDlgItem(dlg, kIdText), static_cast<const wchar_t*>(textPtr));
            // Move the caret to the top so long text starts at the beginning.
            SendDlgItemMessageW(dlg, kIdText, EM_SETSEL, 0, 0);
            SetFocus(GetDlgItem(dlg, IDOK));
            return FALSE;  // focus was set explicitly
        }

        case WM_COMMAND:
            switch (LOWORD(wp)) {
                case IDOK:
                case IDCANCEL: EndDialog(dlg, LOWORD(wp)); return TRUE;
                default: break;
            }
            break;

        case WM_CLOSE:
            // Closing via [X] or Esc counts as declining.
            EndDialog(dlg, IDCANCEL);
            return TRUE;

        default: break;
    }
    return FALSE;
}

// Load the language-appropriate agreement from the executable's own resources, so
// the first launch never depends on files on disk. Falls back to English. Returns
// text with CRLF line breaks, as EDIT controls require.
std::wstring LoadText() {
    std::string utf8 = EmbeddedText::Read(
        GetLang() == Lang::Chinese ? EmbeddedText::kEulaChinese : EmbeddedText::kEulaEnglish);
    if (utf8.empty()) utf8 = EmbeddedText::Read(EmbeddedText::kEulaEnglish);
    if (utf8.empty()) return L"";

    const std::wstring wide = Utf8ToWide(utf8);
    std::wstring text;
    text.reserve(wide.size() + 64);
    for (size_t i = 0; i < wide.size(); ++i) {
        if (wide[i] == L'\n' && (i == 0 || wide[i - 1] != L'\r')) text.push_back(L'\r');
        text.push_back(wide[i]);
    }
    return text;
}

// Returns true only when the user accepted, which is meaningful in gated mode.
bool RunDialog(HINSTANCE instance, bool gated) {
    const std::wstring text = LoadText();
    if (text.empty()) {
        Dialogs::Show(T(L"eula.loadFail"), MB_ICONERROR);
        return false;
    }
    const std::vector<BYTE> tmpl = BuildTemplate(gated);
    return DialogBoxIndirectParamW(instance, reinterpret_cast<LPCDLGTEMPLATEW>(tmpl.data()),
                                   nullptr, DialogProc,
                                   reinterpret_cast<LPARAM>(text.c_str())) == IDOK;
}

}  // namespace

// Identifies the agreement BY ITS CONTENT.
//
// Several properties are deliberate here and each one has a failure mode it avoids:
//
//   * It hashes the DOCUMENT, not one rendering of it. The dialog text is CRLF-
//     converted and localized; hashing that would make a language switch look like a
//     changed agreement and re-prompt a user who agreed to the same document.
//   * It hashes BOTH embedded documents, in a fixed order, rather than the one for
//     the current language. Same reason: which language this process happens to be
//     in must not change what "the agreement" is.
//   * It covers the bytes as embedded, so any edit to the resource — including one
//     nobody would notice — changes the identity and renews consent.
//
// The bytes hashed are exactly what LoadText() shows (EmbeddedText::Read strips the
// BOM in both paths), so a stored acceptance always describes the text that was on
// screen when the user clicked Accept.
std::string AcceptedTextHash() {
    Crypto::Sha256 hash;
    if (!hash.valid()) return {};

    const int documents[] = {EmbeddedText::kEulaEnglish, EmbeddedText::kEulaChinese};
    for (int id : documents) {
        const std::string doc = EmbeddedText::Read(id);
        if (doc.empty()) return {};  // an unreadable document means nothing to accept
        // Length-prefixed so two documents can never concatenate into a third
        // document's byte string.
        const uint64_t n = doc.size();
        hash.Add(&n, sizeof(n));
        hash.Add(doc.data(), doc.size());
    }
    return WideToUtf8(hash.Hex());
}

bool EnsureAccepted(HINSTANCE instance) {
    const std::string hash = AcceptedTextHash();
    if (hash.empty()) {
        // The document could not be read, so there is nothing to accept. Fail closed:
        // no agreement, no program.
        Dialogs::Show(T(L"eula.loadFail"), MB_ICONERROR);
        return false;
    }
    if (EulaAccepted(hash)) return true;
    const bool agreed = RunDialog(instance, true);
    if (agreed) SetEulaAccepted(hash);
    return agreed;
}

void ShowForReading(HINSTANCE instance) {
    RunDialog(instance, false);
}

}  // namespace Eula
