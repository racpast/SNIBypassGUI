// Copyright © 2026 Racpast. All Rights Reserved.
//
// This file is part of SNIBypassGUI, a proprietary software project.
//
// NOTICE: All information contained herein is, and remains the property of
// Racpast. The intellectual and technical concepts contained herein is
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

#include "ui/progress_window.h"

#include <commctrl.h>
#include <dwmapi.h>
#include <uxtheme.h>
#include <vssym32.h>

#include <atomic>
#include <condition_variable>
#include <cstdint>
#include <cstring>
#include <mutex>
#include <string>

#include "app/i18n.h"
#include "app/logging.h"
#include "app/version.h"
#include "compat/bit_cast.h"
#include "update/progress.h"

#ifndef FW_SEMIBOLD
#define FW_SEMIBOLD 600
#endif

// ---------------------------------------------------------------------------
// Design tokens.
//
// These are the values that were settled in the standalone style preview before any
// of this was written: margins, gaps and type sizes in logical pixels at 96 DPI,
// scaled per monitor. They are named rather than inlined because they are the numbers
// a reviewer argues about, and they belong in one block.
// ---------------------------------------------------------------------------
#define PAD 20             // outer margin, all four sides
#define GAP_HEAD_BAR 12    // heading to the progress bar
#define GAP_BAR_DETAIL 16  // bar to the primary detail line
#define GAP_DETAIL 7       // between the two detail lines
#define BAR_H 8            // progress bar thickness
#define GAP_DETAIL_BTN 24  // detail block to the button row
#define GLYPH 20           // box the heading glyph is drawn in
#define GAP_GLYPH_TEXT 12  // glyph to the heading text
#define BTN_W 92
#define BTN_H 30
#define WIDTH 460  // fixed, so the bar does not change length with the value

#define COL_HEAD RGB(28, 28, 28)
#define COL_DETAIL RGB(64, 64, 64)
#define COL_SMALL RGB(118, 118, 118)
#define COL_BORDER RGB(215, 215, 215)
#define COL_TRACK RGB(230, 230, 230)

// The progress green, used only when the theme service cannot be reached. Deliberately
// the colour the themed bar renders, so a degraded render is the same design rather
// than a different one.
#define FALLBACK_FILL RGB(6, 176, 37)

namespace ProgressWindow {

// The messages that carry a request to the UI thread. WM_APP + 3, next to the tray's
// own WM_APP + 1 and the dialogs' WM_APP + 2.
namespace {
constexpr UINT kBeginMessage = WM_APP + 3;
// Private messages for the two operations the worker has to ask the UI thread for.
// Both used to be expressed by borrowing standard messages -- a synthetic WM_COMMAND
// click to disable the button, and a posted WM_DESTROY to close the window -- and
// neither of those does what it looks like it does: WM_DESTROY is produced BY
// DestroyWindow rather than honoured as a request, and a fake click couples the
// intent to whatever the click handler later decides.
constexpr UINT kApplyMessage = WM_APP + 4;  // switch to the apply phase
constexpr UINT kEndMessage = WM_APP + 5;    // tear the window down
constexpr UINT_PTR kTickTimer = 1;
constexpr UINT kTickMs = 100;
constexpr wchar_t kWindowClass[] = L"SNIBypassGUI_UpdateProgress";
}  // namespace

// What the window draws, resolved once per layout. Computing it in one place is what
// keeps the painter and the sizing from drifting apart; with the arithmetic written
// twice the symptom is a clipped line that looks like a font problem.
//
// At namespace scope rather than inside the anonymous block below, because State holds
// one by value: a member whose type has internal linkage would give State a linkage
// warning of its own.
struct Layout {
    RECT glyph;
    RECT head;
    RECT bar;
    RECT detail1;
    RECT detail2;
    RECT button;
    int clientH = 0;
};

// The shared state. Opaque in the header; the window and the worker are the only two
// things that touch it.
struct State {
    // ---- Published by the worker, read by the window ----
    std::atomic<uint64_t> doneBytes{0};
    std::atomic<bool> applying{false};
    std::atomic<bool> cancelRequested{false};
    std::atomic<bool> closed{false};

    // The current file and the file counters. Guarded because a std::wstring cannot
    // be updated atomically, and the window reads it while painting.
    std::mutex infoMutex;
    std::wstring path;
    size_t doneFiles = 0;
    size_t totalFiles = 0;

    uint64_t totalBytes = 0;

    // Invoked once on the UI thread when the user cancels.
    std::function<void()> onCancel;
    bool cancelDelivered = false;

    // ---- Owned by the UI thread ----
    HWND hwnd = nullptr;
    DWORD uiThread = 0;
    HFONT fontHead = nullptr;
    HFONT fontDetail = nullptr;
    HFONT fontSmall = nullptr;
    HFONT fontIcon = nullptr;
    bool hasIcon = false;
    UINT dpi = 96;
    Layout layout;
    Update::Meter meter;
    uint64_t lastSeenBytes = 0;
    ULONGLONG startedAt = 0;

    // ---- Handshake ----
    // Signalled once the window exists, so Begin returns with a usable state rather
    // than a window that may not be up yet.
    HANDLE opened = nullptr;
    HANDLE closedEvent = nullptr;
};

// Read by the download loop on the worker thread, so it is an atomic rather than a
// lock: the loop asks this between reads and must not be able to block on the UI.
//
// Defined after State, since touching the member needs the complete type.
bool CancelRequested(const std::shared_ptr<State>& state) {
    return state && state->cancelRequested.load(std::memory_order_acquire);
}

namespace {

// ---- Small helpers ---------------------------------------------------------

// Whether the session has any icon font at all, and which. Two faces because the font
// was renamed: Windows 11 ships Segoe Fluent Icons, Windows 10 ships Segoe MDL2
// Assets, and the codepoints match. Both the face and the codepoint are checked --
// CreateFontW silently substitutes when a face is missing, and a face can exist while
// lacking the glyph, either of which would draw a wrong shape or a box.
HFONT MakeIconFont(UINT dpi, bool& outHasIcon) {
    outHasIcon = false;
    const int h = -MulDiv(14, (int)dpi, 72);
    static const wchar_t* kFaces[] = {L"Segoe Fluent Icons", L"Segoe MDL2 Assets"};
    const wchar_t kGlyph = 0xE896;  // "Download"

    for (int i = 0; i < 2; ++i) {
        const wchar_t* face = kFaces[i];
        HFONT f = CreateFontW(h, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE, DEFAULT_CHARSET,
                              OUT_TT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
                              DEFAULT_PITCH | FF_DONTCARE, face);
        if (!f) continue;

        HDC dc = GetDC(nullptr);
        HGDIOBJ old = SelectObject(dc, f);
        wchar_t actual[LF_FACESIZE] = {0};
        GetTextFaceW(dc, LF_FACESIZE, actual);
        const bool faceOk = (_wcsicmp(actual, face) == 0);

        WORD index = 0;
        const DWORD rc = GetGlyphIndicesW(dc, &kGlyph, 1, &index, GGI_MARK_NONEXISTING_GLYPHS);
        const bool glyphOk = (rc != GDI_ERROR) && (index != 0xFFFF);

        SelectObject(dc, old);
        ReleaseDC(nullptr, dc);

        if (faceOk && glyphOk) {
            outHasIcon = true;
            return f;
        }
        DeleteObject(f);
    }
    return nullptr;
}

HFONT MakeFont(int points, LONG weight, UINT dpi) {
    const int h = -MulDiv(points, (int)dpi, 72);
    return CreateFontW(h, 0, 0, 0, weight, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_TT_PRECIS,
                       CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_DONTCARE,
                       L"Segoe UI");
}

int LineHeight(HFONT f) {
    HDC dc = GetDC(nullptr);
    HGDIOBJ old = SelectObject(dc, f);
    TEXTMETRICW tm = {};
    GetTextMetricsW(dc, &tm);
    SelectObject(dc, old);
    ReleaseDC(nullptr, dc);
    return tm.tmHeight;
}

COLORREF Accent() {
    DWORD c = 0;
    BOOL opaque = FALSE;
    if (SUCCEEDED(DwmGetColorizationColor(&c, &opaque))) {
        // dwmapi returns ABGR; GDI wants the byte order the other way round.
        const COLORREF rgb = RGB(GetBValue(c), GetGValue(c), GetRValue(c));
        if (rgb != RGB(0, 0, 0) && rgb != RGB(255, 255, 255)) return rgb;
    }
    return RGB(0, 120, 212);
}

// Resolve a function from user32, satisfying both gates at once.
//
// GetProcAddress returns FARPROC, and every way of turning that into a concrete
// signature is rejected by one of the two checks this project runs:
//
//   * a cast -- C-style or reinterpret_cast -- is what GCC's -Wcast-function-type
//     exists to reject, and it is right to: the conversion is precisely how a
//     mismatched prototype becomes a crash rather than a compile error;
//   * std::memcpy between the two pointer types is rejected by clang-tidy's
//     bugprone-bitwise-pointer-cast, which also has a point -- memcpy is not a
//     conversion operator.
//
// BitCast is neither, and it is a defined operation rather than a tolerated one; see
// compat/bit_cast.h. A union also passes both gates, and was the first version of
// this, but reading the member that was not written is undefined behaviour in C++.
template <typename Fn>
Fn ResolveUser32(const char* name) {
    return BitCast<Fn>(GetProcAddress(GetModuleHandleW(L"user32.dll"), name));
}

UINT DpiForHwnd(HWND hwnd) {
    typedef UINT(WINAPI * PFN)(HWND);
    static PFN pfn = ResolveUser32<PFN>("GetDpiForWindow");
    if (pfn) {
        const UINT d = pfn(hwnd);
        if (d) return d;
    }
    HDC dc = GetDC(nullptr);
    const UINT d = (UINT)GetDeviceCaps(dc, LOGPIXELSX);
    ReleaseDC(nullptr, dc);
    return d;
}

// A tiny helper so a DPI change rebuilding the fonts cannot leak the old ones.
void ReplaceFont(HFONT& target, int points, LONG weight, UINT dpi) {
    if (target) DeleteObject(target);
    target = MakeFont(points, weight, dpi);
}

Layout ComputeLayout(State* st, int clientW) {
    const UINT dpi = st->dpi;
    const int pad = MulDiv(PAD, (int)dpi, 96);
    const int glyph = MulDiv(GLYPH, (int)dpi, 96);
    const int headH = LineHeight(st->fontHead);
    const int detH = LineHeight(st->fontDetail);
    const int smlH = LineHeight(st->fontSmall);
    const int barH = MulDiv(BAR_H, (int)dpi, 96);
    const int btnW = MulDiv(BTN_W, (int)dpi, 96);
    const int btnH = MulDiv(BTN_H, (int)dpi, 96);
    const int right = clientW - pad;

    Layout L;
    int y = pad;

    // With an icon the heading row is as tall as whichever of the glyph and the text
    // needs more, so neither is squeezed. Without one there is nothing to make room
    // for, so the heading starts at the left margin and the row is just the line box.
    if (st->hasIcon) {
        const int rowH = headH > glyph ? headH : glyph;
        L.glyph = {pad, y + (rowH - glyph) / 2, pad + glyph, y + (rowH - glyph) / 2 + glyph};
        L.head = {L.glyph.right + MulDiv(GAP_GLYPH_TEXT, (int)dpi, 96), y, right, y + rowH};
        y += rowH + MulDiv(GAP_HEAD_BAR, (int)dpi, 96);
    } else {
        L.glyph = {pad, y, pad, y};
        L.head = {pad, y, right, y + headH};
        y += headH + MulDiv(GAP_HEAD_BAR, (int)dpi, 96);
    }

    L.bar = {pad, y, right, y + barH};
    y = L.bar.bottom + MulDiv(GAP_BAR_DETAIL, (int)dpi, 96);

    L.detail1 = {pad, y, right, y + detH};
    y = L.detail1.bottom + MulDiv(GAP_DETAIL, (int)dpi, 96);
    L.detail2 = {pad, y, right, y + smlH};

    const int btnTop = L.detail2.bottom + MulDiv(GAP_DETAIL_BTN, (int)dpi, 96);
    L.button = {right - btnW, btnTop, right, btnTop + btnH};
    L.clientH = L.button.bottom + pad;
    return L;
}

void DrawGlyph(HDC dc, HFONT font, RECT r, COLORREF color) {
    HGDIOBJ old = SelectObject(dc, font);
    SetTextColor(dc, color);
    const wchar_t glyph[] = {0xE896, 0};
    DrawTextW(dc, glyph, -1, &r, DT_CENTER | DT_SINGLELINE | DT_VCENTER);
    SelectObject(dc, old);
}

// The bar, drawn with the same theme parts the shell's own copy dialog uses:
// PROGRESS/PP_TRANSPARENTBAR for the track and PROGRESS/PP_FILL for the run. The
// theme is opened on the WINDOW's handle rather than NULL, since a null handle asks
// for the class theme with no window to resolve against and may answer with the
// classic one. This renders green, which is the modern themed Win32 progress bar's
// colour and not a fallback.
//
// `indeterminate` draws a moving chunk instead of a fixed fill, for the apply phase
// where no percentage applies.
void DrawBar(HWND hwnd, HDC dc, RECT r, int percent, bool indeterminate, ULONGLONG now) {
    const int w = r.right - r.left;

    HTHEME theme = OpenThemeData(hwnd, L"PROGRESS");
    if (theme) {
        DrawThemeBackground(theme, dc, PP_TRANSPARENTBAR, PBBS_NORMAL, &r, NULL);
        int left = r.left, right = r.left;
        if (indeterminate) {
            // A third of the track, sweeping once per 1.2 s. It reports "working" and
            // deliberately not "how far", because in this phase there is no answer.
            const int sweep = (int)((now % 1200) * w / 1200);
            const int len = w / 3;
            left = r.left + sweep - len;
            right = left + len;
            if (left < r.left) left = r.left;
            if (right > r.right) right = r.right;
        } else {
            const int fill = (int)((long long)percent * w / 100);
            right = r.left + (fill < 4 && fill > 0 ? 4 : fill);
        }
        if (right > left) {
            RECT fr = {left, r.top, right, r.bottom};
            DrawThemeBackground(theme, dc, PP_FILL, PBFS_NORMAL, &fr, NULL);
        }
        CloseThemeData(theme);
        return;
    }

    HBRUSH track = CreateSolidBrush(COL_TRACK);
    FillRect(dc, &r, track);
    DeleteObject(track);
    RECT fr = r;
    fr.right = r.left + (indeterminate ? w / 3 : (int)((long long)percent * w / 100));
    HBRUSH on = CreateSolidBrush(FALLBACK_FILL);
    FillRect(dc, &fr, on);
    DeleteObject(on);
}

void DrawFlatButton(const DRAWITEMSTRUCT* di, HFONT font, bool enabled) {
    HDC dc = di->hDC;
    RECT r = di->rcItem;
    const bool focused = (di->itemState & ODS_FOCUS) != 0;
    const bool down = (di->itemState & ODS_SELECTED) != 0;

    FillRect(dc, &r, GetSysColorBrush(COLOR_WINDOW));

    COLORREF bg = RGB(251, 251, 251);
    if (!enabled)
        bg = RGB(247, 247, 247);
    else if (down)
        bg = RGB(232, 232, 232);

    HBRUSH brush = CreateSolidBrush(bg);
    HPEN pen = CreatePen(PS_SOLID, 1, COL_BORDER);
    HGDIOBJ ob = SelectObject(dc, brush);
    HGDIOBJ op = SelectObject(dc, pen);
    RoundRect(dc, r.left, r.top, r.right, r.bottom, 8, 8);
    if (focused && enabled) {
        RECT f = r;
        InflateRect(&f, -3, -3);
        HGDIOBJ o2 = SelectObject(dc, GetStockObject(NULL_BRUSH));
        HPEN fp = CreatePen(PS_SOLID, 1, Accent());
        HGDIOBJ o3 = SelectObject(dc, fp);
        RoundRect(dc, f.left, f.top, f.right, f.bottom, 6, 6);
        SelectObject(dc, o3);
        DeleteObject(fp);
        SelectObject(dc, o2);
    }
    SelectObject(dc, ob);
    SelectObject(dc, op);
    DeleteObject(brush);
    DeleteObject(pen);

    SetBkMode(dc, TRANSPARENT);
    SetTextColor(dc, enabled ? COL_HEAD : COL_SMALL);
    HGDIOBJ of = SelectObject(dc, font);
    wchar_t label[64] = {0};
    GetWindowTextW(di->hwndItem, label, 63);
    DrawTextW(dc, label, -1, &r, DT_CENTER | DT_SINGLELINE | DT_VCENTER);
    SelectObject(dc, of);
}

// One row of text from the top of its rect, so nothing overflows the box.
void TextRow(HDC dc, HFONT f, RECT r, COLORREF col, const std::wstring& text) {
    HGDIOBJ old = SelectObject(dc, f);
    SetTextColor(dc, col);
    DrawTextW(dc, text.c_str(), -1, &r, DT_LEFT | DT_SINGLELINE | DT_TOP | DT_NOPREFIX);
    SelectObject(dc, old);
}

void Paint(State* st) {
    PAINTSTRUCT ps;
    HDC dc = BeginPaint(st->hwnd, &ps);

    RECT rc;
    GetClientRect(st->hwnd, &rc);

    // Double-buffered: the text and the bar both change on the timer, and without this
    // every tick flickers.
    HDC mem = CreateCompatibleDC(dc);
    HBITMAP bmp = CreateCompatibleBitmap(dc, rc.right, rc.bottom);
    HGDIOBJ oldBmp = SelectObject(mem, bmp);
    FillRect(mem, &rc, GetSysColorBrush(COLOR_WINDOW));
    SetBkMode(mem, TRANSPARENT);

    const Layout& L = st->layout;
    const Update::Meter::Snapshot snap = st->meter.Now();
    const bool applying = st->applying.load(std::memory_order_acquire);
    const bool cancelling = st->cancelRequested.load(std::memory_order_acquire);

    if (st->hasIcon && st->fontIcon) DrawGlyph(mem, st->fontIcon, L.glyph, Accent());
    TextRow(mem, st->fontHead, L.head, COL_HEAD,
            applying ? std::wstring(T(L"msg.updApplying"))
                     : std::wstring(T(L"msg.updProgressTitle")));

    DrawBar(st->hwnd, mem, L.bar, snap.percent, applying, GetTickCount64());

    // The primary fact: absolute progress. No translation needed, since it is numbers
    // and a unit.
    TextRow(
        mem, st->fontDetail, L.detail1, COL_DETAIL,
        Update::FormatBytes(snap.doneBytes) + L" / " + Update::FormatBytes(snap.totalBytes));

    // The supporting line: speed and estimate while downloading, and the file being
    // fetched so a stall is legible. Either part may be absent, and the separator only
    // appears between two parts that are both there.
    std::wstring second;
    if (applying) {
        second = T(L"msg.updApplyingDetail");
    } else if (cancelling) {
        second = T(L"msg.updCancelling");
    } else {
        const std::wstring rate = Update::FormatRate(snap.bytesPerSecond);
        const std::wstring eta =
            snap.etaSeconds >= 0
                ? TFmt(L"msg.updEtaRemaining", Update::FormatDuration(snap.etaSeconds))
                : std::wstring();
        if (!rate.empty()) second = rate;
        if (!eta.empty()) {
            if (!second.empty()) second += L"  \x00B7  ";
            second += eta;
        } else if (second.empty()) {
            // No rate and no estimate: say which file, which is the only true thing
            // left to report.
            std::lock_guard<std::mutex> lock(st->infoMutex);
            second = st->path;
        }
    }
    TextRow(mem, st->fontSmall, L.detail2, COL_SMALL, second);

    BitBlt(dc, 0, 0, rc.right, rc.bottom, mem, 0, 0, SRCCOPY);
    SelectObject(mem, oldBmp);
    DeleteObject(bmp);
    DeleteDC(mem);
    EndPaint(st->hwnd, &ps);
}

// Ask the UI thread to cancel, once. Runs on the UI thread (button click, Esc, close),
// so it does not need the lock beyond guarding the string-free fields.
void RequestCancel(State* st) {
    if (st->applying.load(std::memory_order_acquire)) {
        // Interrupting the apply would leave a half-installed tree, so this is a
        // correctness rule and not a matter of taste: the button is disabled in this
        // phase and any stray request is ignored.
        return;
    }
    if (!st->cancelRequested.exchange(true)) {
        // First time only: this is what tells the download loop to stop.
        if (st->onCancel) st->onCancel();
        // Reflect the click immediately rather than waiting for the loop to notice.
        HWND btn = GetDlgItem(st->hwnd, IDCANCEL);
        if (btn) {
            SetWindowTextW(btn, T(L"msg.updCancelling"));
            EnableWindow(btn, FALSE);
        }
        InvalidateRect(st->hwnd, nullptr, FALSE);
    }
}

LRESULT CALLBACK WindowProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp) {
    // Recover the state. WM_NCCREATE supplies it; every later message reads it back
    // out of the window's own storage. GWLP_USERDATA is an integer slot, so the bits
    // come back through BitCast rather than a cast -- see compat/bit_cast.h.
    State* st = BitCast<State*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));

    switch (msg) {
        case WM_NCCREATE: {
            auto* cs = BitCast<CREATESTRUCTW*>(lp);
            st = static_cast<State*>(cs->lpCreateParams);
            SetWindowLongPtrW(hwnd, GWLP_USERDATA, BitCast<LONG_PTR>(st));
            // Delegated, not swallowed, and the return value is the delegate's.
            //
            // DefWindowProc's WM_NCCREATE handler is what copies CREATESTRUCT::
            // lpszName into the window's text. Returning TRUE without calling it skips
            // that step, and the window comes up with a caption bar and no caption --
            // measured, not assumed: with this return TRUE the window's title read back
            // as empty, and with the delegation it read back as the name passed to
            // CreateWindowExW.
            return DefWindowProcW(hwnd, msg, wp, lp);
        }

        case WM_CREATE: {
            SetTimer(hwnd, kTickTimer, kTickMs, nullptr);
            return 0;
        }

        case WM_PAINT:
            if (st) Paint(st);
            return 0;

        // WM_PAINT covers every pixel of the client area, so erasing first only adds a
        // flash.
        case WM_ERASEBKGND: return 1;

        case WM_TIMER:
            if (st && wp == kTickTimer) {
                // The meter is fed on the UI thread, from a snapshot of what the worker
                // has published. This is the only place the speed is computed, so the
                // window never races the download for it.
                const uint64_t now = st->doneBytes.load(std::memory_order_acquire);
                if (now > st->lastSeenBytes) {
                    st->meter.AddBytes(now - st->lastSeenBytes);
                    st->lastSeenBytes = now;
                }
                {
                    std::lock_guard<std::mutex> lock(st->infoMutex);
                    st->meter.SetFile(st->doneFiles, st->path);
                }
                const ULONGLONG elapsed = GetTickCount64() - st->startedAt;
                st->meter.Sample((double)elapsed / 1000.0);
                InvalidateRect(hwnd, nullptr, FALSE);
            }
            return 0;

        // The apply phase began. Handled here, on the thread that owns the button, so
        // it is disabled and relabelled on the same thread that created it.
        case kApplyMessage: {
            if (!st) return 0;
            st->meter.SetApplying(true);
            HWND btn = GetDlgItem(hwnd, IDCANCEL);
            if (btn) {
                SetWindowTextW(btn, T(L"msg.updCancelling"));
                EnableWindow(btn, FALSE);
            }
            InvalidateRect(hwnd, nullptr, FALSE);
            return 0;
        }

        // Tear down. The transfer is over by the time this arrives, so there is
        // nothing left to cancel and the window closes unconditionally.
        case kEndMessage: DestroyWindow(hwnd); return 0;

        case WM_DRAWITEM: {
            const DRAWITEMSTRUCT* di = BitCast<const DRAWITEMSTRUCT*>(lp);
            if (di->CtlID == IDCANCEL && st) {
                const bool enabled = (di->itemState & ODS_DISABLED) == 0;
                DrawFlatButton(di, st->fontDetail, enabled);
                return TRUE;
            }
            break;
        }

        case WM_COMMAND:
            if (LOWORD(wp) == IDCANCEL && st) {
                RequestCancel(st);
                return 0;
            }
            break;

        // Closing mid-download means "cancel", not "hide and keep going": a download
        // the user cannot see is one they cannot stop.
        case WM_CLOSE:
            if (st) {
                RequestCancel(st);
                return 0;
            }
            DestroyWindow(hwnd);
            return 0;

        case WM_DPICHANGED: {
            if (!st) break;
            st->dpi = HIWORD(wp);
            ReplaceFont(st->fontHead, 12, FW_SEMIBOLD, st->dpi);
            ReplaceFont(st->fontDetail, 9, FW_NORMAL, st->dpi);
            ReplaceFont(st->fontSmall, 9, FW_NORMAL, st->dpi);
            if (st->fontIcon) DeleteObject(st->fontIcon);
            st->fontIcon = MakeIconFont(st->dpi, st->hasIcon);
            const RECT* sug = BitCast<const RECT*>(lp);
            SetWindowPos(hwnd, nullptr, sug->left, sug->top, sug->right - sug->left,
                         sug->bottom - sug->top, SWP_NOZORDER | SWP_NOACTIVATE);
            return 0;
        }

        case WM_THEMECHANGED:
        case WM_SYSCOLORCHANGE: InvalidateRect(hwnd, nullptr, TRUE); return 0;

        case WM_DESTROY:
            if (st) {
                KillTimer(hwnd, kTickTimer);
                st->hwnd = nullptr;
                // The fonts are deleted HERE and not in End(): a GDI object belongs to
                // the thread that created it, and End() may well be a different one.
                // This message runs on the UI thread, which is where they were made.
                if (st->fontHead) DeleteObject(st->fontHead);
                if (st->fontDetail) DeleteObject(st->fontDetail);
                if (st->fontSmall) DeleteObject(st->fontSmall);
                if (st->fontIcon) DeleteObject(st->fontIcon);
                st->fontHead = st->fontDetail = st->fontSmall = st->fontIcon = nullptr;
                st->closed.store(true);
                if (st->closedEvent) SetEvent(st->closedEvent);
            }
            return 0;

        default: break;
    }
    return DefWindowProcW(hwnd, msg, wp, lp);
}

// Create the window itself. Runs on the UI thread, called from the marshalled begin
// message, so everything it touches belongs to the thread that owns it.
void CreateOnUiThread(State* st, HWND owner) {
    st->uiThread = GetCurrentThreadId();
    st->dpi = DpiForHwnd(owner);

    st->fontHead = MakeFont(12, FW_SEMIBOLD, st->dpi);
    st->fontDetail = MakeFont(9, FW_NORMAL, st->dpi);
    st->fontSmall = MakeFont(9, FW_NORMAL, st->dpi);
    st->fontIcon = MakeIconFont(st->dpi, st->hasIcon);

    st->meter.Plan(st->totalBytes, st->totalFiles);
    st->startedAt = GetTickCount64();

    WNDCLASSEXW wc = {};
    wc.cbSize = sizeof(wc);
    wc.lpfnWndProc = WindowProc;
    wc.hInstance = GetModuleHandleW(nullptr);
    wc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    wc.hbrBackground = GetSysColorBrush(COLOR_WINDOW);
    wc.lpszClassName = kWindowClass;

    // Registered every time; RegisterClassExW fails harmlessly when the class already
    // exists, which is the case for a second update in the same session.
    RegisterClassExW(&wc);

    // Created at its final size directly: the layout is known before the window exists,
    // so there is no second resize and no flash of a wrongly-sized window.
    const int cw = MulDiv(WIDTH, (int)st->dpi, 96);
    st->layout = ComputeLayout(st, cw);
    const int ch = st->layout.clientH;

    RECT wr = {0, 0, cw, ch};
    typedef BOOL(WINAPI * AdjustFn)(LPRECT, DWORD, BOOL, DWORD, UINT);
    AdjustFn adjust = ResolveUser32<AdjustFn>("AdjustWindowRectExForDpi");
    const DWORD style = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU;
    if (adjust)
        adjust(&wr, style, FALSE, 0, st->dpi);
    else
        AdjustWindowRectEx(&wr, style, FALSE, 0);

    const int w = wr.right - wr.left;
    const int h = wr.bottom - wr.top;

    // Centred on whichever monitor the tray window is on, not on the primary one.
    RECT area = {0, 0, GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN)};
    HMONITOR mon = MonitorFromWindow(owner, MONITOR_DEFAULTTONEAREST);
    MONITORINFO mi = {};
    mi.cbSize = sizeof(mi);
    if (mon && GetMonitorInfoW(mon, &mi)) area = mi.rcWork;

    // WS_EX_APPWINDOW: the window is owned by the tray window, and owned windows get
    // no taskbar button by default; this opts back in.
    //
    // WS_EX_DLGMODALFRAME: suppresses the title-bar icon, matching the appearance of
    // the EULA dialog which uses DS_MODALFRAME for the same effect.
    const DWORD exStyle = WS_EX_APPWINDOW | WS_EX_DLGMODALFRAME;

    st->hwnd = CreateWindowExW(
        exStyle, kWindowClass, APP_NAME, style, area.left + ((area.right - area.left) - w) / 2,
        area.top + ((area.bottom - area.top) - h) / 2, w, h, owner, nullptr, wc.hInstance, st);

    if (st->hwnd) {
        HWND btn = CreateWindowExW(0, L"BUTTON", T(L"msg.updCancel"),
                                   WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_OWNERDRAW, 0, 0, 0,
                                   0, st->hwnd, (HMENU)IDCANCEL, wc.hInstance, nullptr);
        SendMessageW(btn, WM_SETFONT, (WPARAM)st->fontDetail, TRUE);
        SetWindowPos(btn, nullptr, st->layout.button.left, st->layout.button.top,
                     st->layout.button.right - st->layout.button.left,
                     st->layout.button.bottom - st->layout.button.top,
                     SWP_NOZORDER | SWP_NOACTIVATE);
        ShowWindow(st->hwnd, SW_SHOW);
        UpdateWindow(st->hwnd);
    } else {
        LOGE(L"Update: could not create the progress window.");
    }

    // Release whoever is waiting in Begin.
    if (st->opened) SetEvent(st->opened);
}

// A request in flight between the thread that opens the window and the UI thread that
// creates it. It lives on the requester's stack: Begin blocks until the window exists,
// so the payload cannot outlive the wait.
struct BeginRequest {
    State* state = nullptr;
    HWND owner = nullptr;
};

}  // namespace

std::shared_ptr<State> Begin(HWND owner, uint64_t totalBytes, size_t totalFiles,
                             std::function<void()> onCancel) {
    // No window to own it: the first-run path fetches the payload before any window
    // exists. Returning null makes every other call here a no-op, which is exactly the
    // behaviour that path needs.
    if (!owner) return nullptr;

    auto state = std::make_shared<State>();
    state->totalBytes = totalBytes;
    state->totalFiles = totalFiles;
    state->onCancel = std::move(onCancel);
    state->opened = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    state->closedEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    if (!state->opened || !state->closedEvent) {
        LOGE(L"Update: could not create the progress window's events.");
        return nullptr;
    }

    BeginRequest request;
    request.state = state.get();
    request.owner = owner;

    // If we are already on the UI thread there is nothing to marshal. This is not the
    // normal path — the update runs on a worker — but it keeps the function correct
    // wherever it is called from.
    if (GetWindowThreadProcessId(owner, nullptr) == GetCurrentThreadId()) {
        CreateOnUiThread(state.get(), owner);
    } else {
        if (!PostMessageW(owner, kBeginMessage, 0, reinterpret_cast<LPARAM>(&request))) {
            LOGE(L"Update: could not ask the UI thread to open the progress window.");
            return nullptr;
        }
        // Wait for the window to exist, so the caller can report bytes immediately
        // rather than racing the creation.
        WaitForSingleObject(state->opened, 10000);
    }

    if (!state->hwnd) return nullptr;
    return state;
}

void Report(const std::shared_ptr<State>& state, const std::wstring& path,
            uint64_t bytesOverall, size_t doneFiles, size_t totalFiles) {
    if (!state) return;
    {
        std::lock_guard<std::mutex> lock(state->infoMutex);
        state->path = path;
        state->doneFiles = doneFiles;
        state->totalFiles = totalFiles;
    }
    // Published after the rest, so a UI tick that reads the new byte count sees the
    // file name that goes with it.
    state->doneBytes.store(bytesOverall, std::memory_order_release);
}

void EnterApply(const std::shared_ptr<State>& state) {
    if (!state) return;
    // Published first, so a UI tick that happens to run before the message below still
    // draws the apply phase rather than the download phase.
    state->applying.store(true, std::memory_order_release);

    // The button lives on the UI thread, so it is disabled there. Posted rather than
    // sent, so this cannot deadlock if the UI thread is inside a paint.
    HWND hwnd = state->hwnd;
    if (hwnd) PostMessageW(hwnd, kApplyMessage, 0, 0);
}

void End(const std::shared_ptr<State>& state) {
    if (!state) return;

    HWND hwnd = state->hwnd;
    if (hwnd) {
        if (state->uiThread == GetCurrentThreadId()) {
            // Already on the owning thread: destroy directly rather than posting and
            // then waiting for a message this thread would have to pump to deliver.
            DestroyWindow(hwnd);
        } else {
            PostMessageW(hwnd, kEndMessage, 0, 0);
            WaitForSingleObject(state->closedEvent, 5000);
        }
    }

    if (state->opened) CloseHandle(state->opened);
    if (state->closedEvent) CloseHandle(state->closedEvent);
    state->opened = nullptr;
    state->closedEvent = nullptr;

    // The fonts are deleted in WM_NCDESTROY, on the thread that created them, rather
    // than here: a GDI object belongs to the thread that made it, and this call may
    // well be a different one.
}

bool IsBeginMessage(UINT msg) {
    return msg == kBeginMessage;
}

void HandleBeginMessage(HWND owner, LPARAM lp) {
    auto* request = BitCast<BeginRequest*>(lp);
    if (request && request->state) CreateOnUiThread(request->state, owner);
}

}  // namespace ProgressWindow
