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
#include <windows.h>

#include <string>

// Message boxes, owned by the tray window and raised on the thread that owns it.
//
// Almost every dialog this program shows is requested from a worker thread — a tray
// command, the update check, the supervisor reporting a component that died. A
// MessageBox called from there has no owner: it is modal to nothing, lands wherever
// the window manager feels like putting it, does not get raised with the tray, and
// can end up behind an unrelated window. It also runs a nested message loop on a
// thread that owns no windows, which is not what a modal dialog is defined against.
//
// The fix is the one the tray already uses for its exit path (ui/tray.cpp): the
// request is marshalled to the thread that owns the window. Here that means the
// dialog runs on the UI thread with the tray window as its owner, and the calling
// thread blocks until it is dismissed, so the call still behaves like the blocking
// MessageBox it replaces — a caller that branches on the answer keeps working.
namespace Dialogs {

// Arm the layer with the window that will own every dialog and the thread that owns
// it. Called once, from Tray::Create, as soon as the window exists. Until this runs a
// request is executed inline on the calling thread with no owner, which is the
// correct behaviour for the startup dialogs in main() — that path runs before any
// window exists and on the only thread there is.
void Init(HWND owner);

// Disarm, before the window is destroyed. A request that arrives afterwards (a
// detached worker still finishing a teardown, the update thread unwinding) executes
// inline rather than posting to a window that no longer exists.
void Shutdown();

// The armed owner window, or null before Init or after Shutdown.
//
// Exposed because the owner is a fact about the program that more than one thing
// needs: this module uses it to parent its dialogs, and the update progress window
// needs it for the same reason. The alternative was a second global holding the same
// handle, which is one more thing that can be armed and disarmed out of step with
// this one.
HWND Owner();

// Show `text` with `flags` (MB_ICON* | MB_YESNO and friends) and return the result.
//
// Blocks the caller until the dialog is dismissed, on every thread. The return value
// is the same one MessageBoxW would have given: IDOK, IDYES, IDRETRY, ...
int Show(const std::wstring& text, UINT flags);

// ---- For the tray's window procedure only ----
//
// The marshalled request is a window message, so the window procedure is what
// dispatches it. These two are the whole interface between the two modules; nothing
// else should call either.
bool IsShowMessage(UINT msg);
void HandleShowMessage(HWND owner, LPARAM lp);

}  // namespace Dialogs
