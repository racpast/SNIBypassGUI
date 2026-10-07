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

#include "app/bootstrap.h"

#include <windows.h>

#include <string>
#include <vector>

#include "app/i18n.h"
#include "app/logging.h"
#include "app/paths.h"
#include "app/payload.h"
#include "app/settings.h"
#include "app/text.h"
#include "app/version.h"
#include "platform/dialogs.h"
#include "platform/shortcut.h"

namespace Bootstrap {

// Check if meta.ini exists. Every file location is resolved through it,
// so its absence means incomplete extraction.
bool PayloadPresent() {
    return Payload::Present();
}

// Detect if running from archiver temp directory (e.g., Rar$EXa0.123\, 7zO1A2B\).
// This prevents network fetches from a transient location that will be deleted.
// Defaults to true on uncertainty (fail-safe: one extra dialog is better than
// fetching into a directory about to disappear).
bool RunningFromArchiveTemp() {
    const std::wstring exeDir = LowerW(ExeDir());

    // Well-known archiver scratch markers (Rar$EXa0.123\, 7zO1A2B\).
    if (exeDir.find(L"\\rar$ex") != std::wstring::npos) return true;
    if (exeDir.find(L"\\7zo") != std::wstring::npos) return true;

    // Use GetTempPathW (not environment variables) to get temp directory.
    // Handles TMP/TEMP/USERPROFILE fallback chain and works when variables are unset.
    // GetTempPath2W unavailable (requires Windows 11, we target Windows 7).
    //
    // An empty result is itself the archive case: we could not establish where a
    // normal install would be running from, so we cannot say this is not one.
    std::wstring temp;
    {
        std::vector<wchar_t> buf(MAX_PATH + 1, L'\0');
        DWORD n = GetTempPathW(static_cast<DWORD>(buf.size()), buf.data());
        if (n >= buf.size()) {
            // The returned size exceeds the buffer: grow to that size and ask again.
            buf.assign(n, L'\0');
            n = GetTempPathW(n, buf.data());
        }
        if (n == 0 || n >= buf.size()) return true;  // undetermined -> refuse to go online
        temp.assign(buf.data(), n);
    }

    std::wstring prefix = LowerW(temp);
    if (prefix.empty()) return true;
    if (prefix.back() != L'\\') prefix.push_back(L'\\');
    return exeDir.compare(0, prefix.size(), prefix) == 0;
}

// Manage desktop shortcut for extract-and-run installs.
// - Foreign shortcut (points elsewhere): repoint to this copy
// - Missing shortcut: user deleted it, mark preference as declined
// Each copy has its own config.ini, so last-run copy owns the desktop shortcut.
void SyncDesktopShortcut() {
    // The executable already sits on the desktop: a shortcut beside it is pointless.
    if (Shortcut::ExeIsOnDesktop()) return;

    const ShortcutPref pref = GetShortcutPref();
    if (pref == ShortcutPref::Declined) return;

    const Shortcut::State state = Shortcut::Inspect();

    if (pref == ShortcutPref::Wanted) {
        switch (state) {
            case Shortcut::State::Ours:
                // Already correct. Refresh description (handles language changes).
                Shortcut::Create();
                return;
            case Shortcut::State::Foreign:
                LOGI(L"Shortcut: desktop link points at another copy; repointing it here.");
                Shortcut::Create();
                return;
            case Shortcut::State::Missing:
                LOGI(L"Shortcut: desktop link was removed by the user; not recreating it.");
                SetShortcutPref(ShortcutPref::Declined);
                return;
        }
        return;
    }

    // Never asked. Adopt existing shortcut if present.
    if (state == Shortcut::State::Ours) {
        SetShortcutPref(ShortcutPref::Wanted);
        return;
    }

    if (Dialogs::Show(T(L"msg.shortcutAsk"), MB_ICONQUESTION | MB_YESNO) != IDYES) {
        // Non-Yes outcomes recorded to prevent re-asking.
        SetShortcutPref(ShortcutPref::Declined);
        return;
    }
    if (Shortcut::Create()) {
        SetShortcutPref(ShortcutPref::Wanted);
    } else {
        // Don't record failed preference (allows retry on next launch).
        Dialogs::Show(T(L"msg.shortcutFail"), MB_ICONWARNING);
    }
}

}  // namespace Bootstrap
