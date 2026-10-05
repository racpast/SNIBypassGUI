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

#include <cstdint>
#include <functional>
#include <memory>
#include <string>

// The update progress window: how far the download is, how fast, how long left, and
// a Cancel button.
//
// The calling shape is deliberately the same one Dialogs uses (platform/dialogs.h):
// a worker transfers, and the window lives on the UI thread, so every touch of a
// window handle is marshalled there rather than reached for directly. What differs
// is the lifetime: a dialog blocks its requester until it is dismissed, whereas a
// download runs FOR the requesting thread — the worker is the one doing the work and
// must keep running while the window is up. So the progress window is opened,
// updated and closed around a transfer that continues in between, and the state both
// sides share is reference-counted rather than living on a stack.
//
// Threading: Begin/Report/End/EnterApply/CancelRequested may be called from any
// thread. The window itself only ever runs on the one that owns `owner`.
namespace ProgressWindow {

// Everything the worker and the window both touch. Opaque so the window's drawing
// state stays out of translation units that only want to report bytes.
struct State;

// Whether the user has asked to stop. Read by the download loop to decide whether to
// keep going. Safe from any thread.
bool CancelRequested(const std::shared_ptr<State>& state);

// Open the window, owned by `owner` (the tray window) and shown on its thread.
//
// Returns null when there is no window to show it in — the first-run path runs
// before any window exists — in which case the caller proceeds without one and every
// other call in this file becomes a no-op on the null state.
//
// `totalBytes` and `totalFiles` are the whole job's size, so the bar and the
// percentage describe the run rather than the current file. `onCancel` is invoked
// once, on the UI thread, when the user cancels; it must not block.
std::shared_ptr<State> Begin(HWND owner, uint64_t totalBytes, size_t totalFiles,
                             std::function<void()> onCancel);

// The transfer is now downloading `path`, and `bytesForFile` of it have arrived.
// Cumulative within a file, and reset when `path` changes.
void Report(const std::shared_ptr<State>& state, const std::wstring& path,
            uint64_t bytesForFile, size_t doneFiles, size_t totalFiles);

// The download is finished and the install is about to be replaced. Cancellation
// stops being possible here — interrupting the apply would leave a half-installed
// tree — so the window switches to an indeterminate bar and disables Cancel.
void EnterApply(const std::shared_ptr<State>& state);

// Close the window and wait for it to be gone. Idempotent.
void End(const std::shared_ptr<State>& state);

// ---- For the tray's window procedure only ----
//
// The open request is a window message, so the window procedure is what dispatches
// it, exactly as it does for Dialogs. Nothing else should call this.
bool IsBeginMessage(UINT msg);
void HandleBeginMessage(HWND owner, LPARAM lp);

}  // namespace ProgressWindow
