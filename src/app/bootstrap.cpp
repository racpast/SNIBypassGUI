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

#include "app/bootstrap.h"

#include <windows.h>

#include <string>
#include <vector>

#include "app/i18n.h"
#include "app/logging.h"
#include "app/paths.h"
#include "app/settings.h"
#include "app/text.h"
#include "app/version.h"
#include "platform/dialogs.h"
#include "platform/shortcut.h"

namespace Bootstrap {

// paths.ini is the payload's root: every file the application opens is located
// through it. Its absence reliably means the payload was never extracted beside
// the executable, regardless of what else the payload contains — so this is the
// one stable contract, not a check for a specific directory name.
bool PayloadPresent() {
    return GetFileAttributesW((ExeDir() + L"paths.ini").c_str()) != INVALID_FILE_ATTRIBUTES;
}

// Archivers "open" an executable by extracting only it to a scratch directory under
// %TEMP% and launching it there, leaving the sibling payload inside the archive. We
// detect that location so a missing payload in this state means "you did not extract
// the whole archive" rather than "your install is broken" — and so we do NOT go
// online, which would look like a hang to an offline user.
//
// Every branch below treats "cannot tell" as "from a scratch directory", because the
// two possible mistakes are not equal: a false positive costs an offline user one
// dialog telling them to extract the archive they were already running out of, while
// a false negative sends them into a network fetch from a directory that will be
// deleted under them. The earlier version got this exactly backwards — two unset
// environment variables made it return "not an archive", i.e. the fail-open direction.
bool RunningFromArchiveTemp() {
    const std::wstring exeDir = LowerW(ExeDir());

    // Well-known archiver scratch markers (Rar$EXa0.123\, 7zO1A2B\).
    if (exeDir.find(L"\\rar$ex") != std::wstring::npos) return true;
    if (exeDir.find(L"\\7zo") != std::wstring::npos) return true;

    // The process's temp directory, asked of the system rather than read out of the
    // environment.
    //
    // GetTempPathW is the documented answer to "where is this process's temp
    // directory" and applies the full resolution order itself — TMP, then TEMP, then
    // USERPROFILE, then the Windows directory — so it already covers both variables
    // and still answers when both are unset (running under a sanitized environment, a
    // service account, or a launcher that cleared them). Reading TEMP/TMP directly
    // cannot do that, which is the bug this replaces. GetTempPath2W would be the
    // newer choice — it answers the system temp directory for a system process — but
    // this program targets Windows 7 and that API is Windows 11 only, so it is not
    // reachable from here.
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

// Extract-and-run means no installer ever put this program anywhere findable: a user
// who extracted it into a nested folder has nothing to click next time. So we offer a
// desktop shortcut, and thereafter keep it honest.
//
// Two facts shape the rules. First, each copy carries its own config.ini, so the
// stored preference is per-copy while the desktop is shared: running a second copy
// repoints the one shortcut at itself. Second, the two ways a shortcut can stop being
// ours are NOT equivalent:
//
//   Foreign (present, pointing elsewhere) — another copy took the name. The user asked
//       THIS copy for a shortcut and no longer has one, so repointing it restores what
//       they asked for. Whichever copy ran last owns the desktop, which matches the
//       copy the user is actually using.
//   Missing (not there at all) — the user deleted it. Recreating that would overrule
//       them on every launch, so a delete is taken as a change of mind and the
//       preference is reset to declined.
//
// A decline is therefore permanent until the user acts again, and we never fight the
// user over a file they removed on purpose. Only the never-asked state prompts.
void SyncDesktopShortcut() {
    // The executable already sits on the desktop: a shortcut beside it is pointless.
    if (Shortcut::ExeIsOnDesktop()) return;

    const ShortcutPref pref = GetShortcutPref();
    if (pref == ShortcutPref::Declined) return;

    const Shortcut::State state = Shortcut::Inspect();

    if (pref == ShortcutPref::Wanted) {
        switch (state) {
            case Shortcut::State::Ours:
                // Already correct. Create() is cheap and idempotent, so refresh
                // unconditionally — the stored description does not follow a language
                // change on its own.
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

    // Never asked. If a shortcut for this executable somehow already exists, adopt it
    // rather than asking about something the user evidently already has.
    if (state == Shortcut::State::Ours) {
        SetShortcutPref(ShortcutPref::Wanted);
        return;
    }

    if (Dialogs::Show(T(L"msg.shortcutAsk"), MB_ICONQUESTION | MB_YESNO) != IDYES) {
        // Any non-Yes outcome (No, Esc, the close button) is a decline, recorded so the
        // question never comes back on its own.
        SetShortcutPref(ShortcutPref::Declined);
        return;
    }
    if (Shortcut::Create()) {
        SetShortcutPref(ShortcutPref::Wanted);
    } else {
        // Do not record a preference we failed to honour: leaving it unset lets the
        // next launch try again rather than silently giving up forever.
        Dialogs::Show(T(L"msg.shortcutFail"), MB_ICONWARNING);
    }
}

}  // namespace Bootstrap
