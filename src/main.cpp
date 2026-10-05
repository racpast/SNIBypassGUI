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

#include <windows.h>

#include <string>
#include <thread>

#include "app/bootstrap.h"
#include "app/controller.h"
#include "app/i18n.h"
#include "app/logging.h"
#include "app/services.h"
#include "app/settings.h"
#include "app/text.h"
#include "app/version.h"
#include "platform/command.h"
#include "platform/dialogs.h"
#include "platform/elevation.h"
#include "platform/process.h"
#include "ui/eula.h"
#include "ui/tray.h"

namespace {

// How long to wait for the single-instance lock after clearing the way for it.
// A bounded wait on a kernel object: it returns the moment the previous owner
// releases the mutex, which happens as that process is torn down.
constexpr DWORD kInstanceLockTimeoutMs = 5000;

// Keep THIS process and terminate every other copy running in THIS SESSION. The
// single-instance rule is per interactive session, not per machine: see the note on
// APP_MUTEX_NAME for why the mutex still spans sessions and this filter is what keeps
// one user's launch from terminating another user's.
//
// KillTree does not return until each one is actually gone, so by the time this
// returns the lock below is genuinely free to take.
void EnforceSingleInstance() {
    const DWORD self = GetCurrentProcessId();
    DWORD selfSession = 0;
    if (!ProcessIdToSessionId(self, &selfSession)) {
        // Cannot establish our own session, so cannot tell which copies are ours to
        // stop. Terminating none is the only safe answer: every candidate is another
        // user's until proven otherwise.
        LOGW(L"Could not determine this process's session; not terminating any other copy.");
        return;
    }

    for (DWORD pid : Process::FindByName(L"SNIBypassGUI.exe")) {
        if (pid == self) continue;
        DWORD session = 0;
        if (!ProcessIdToSessionId(pid, &session) || session != selfSession) continue;
        LOGW(L"Terminating another SNIBypassGUI instance in this session, pid " +
             std::to_wstring(pid));
        Process::KillTree(pid);
    }
}

// Holds the single-instance mutex for the lifetime of the run.
//
// The previous version created the mutex and never looked at the result, so it
// proved nothing and prevented nothing. Ownership has to be acquired to mean
// anything: if it cannot be, a copy we failed to terminate is still running and
// this one must not start a second stack on top of it.
class InstanceLock {
public:
    explicit InstanceLock(DWORD timeoutMs)
        : handle_(CreateMutexW(nullptr, FALSE, APP_MUTEX_NAME)) {
        if (!handle_) return;
        const DWORD result = WaitForSingleObject(handle_, timeoutMs);
        // WAIT_ABANDONED is the normal outcome here: the copy we just terminated
        // died holding the mutex, and the kernel hands ownership to us anyway.
        held_ = (result == WAIT_OBJECT_0 || result == WAIT_ABANDONED);
    }
    ~InstanceLock() {
        if (held_) ReleaseMutex(handle_);
        if (handle_) CloseHandle(handle_);
    }
    InstanceLock(const InstanceLock&) = delete;
    InstanceLock& operator=(const InstanceLock&) = delete;

    bool held() const { return held_; }

private:
    HANDLE handle_ = nullptr;
    bool held_ = false;
};

}  // namespace

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, LPWSTR lpCmdLine, int) {
    const std::wstring cmdline = lpCmdLine ? lpCmdLine : L"";

    LogInit();
    LOGI(L"=== SNIBypassGUI " + GetVersionDisplayStr() + L" (" + APP_VERSION_NUM +
         L") starting (args: " + cmdline + L") ===");

    // 1. Require administrator, elevating if necessary.
    //
    // Both dialogs before the tray exists run through Dialogs::Show like every other
    // one. There is no window yet, so it takes the ownerless inline path — which is
    // correct here, not a fallback: this is the main thread, and it is the only thread
    // in the process at this point.
    if (!IsRunningAsAdmin()) {
        if (!RelaunchElevated(cmdline)) Dialogs::Show(T(L"msg.needAdmin"), MB_ICONERROR);
        return 0;
    }

    // 2. Run-location sanity check. A missing payload while running from an archiver's
    //    scratch directory means the archive was never extracted; refuse early, before
    //    any prompt or network access. A fixed install with a missing payload is
    //    handled after the agreement, through the signed download path.
    if (!Bootstrap::PayloadPresent() && Bootstrap::RunningFromArchiveTemp()) {
        LOGE(L"Running from an archive temp dir without payload; refusing to continue.");
        Dialogs::Show(T(L"msg.extractFirst"), MB_ICONERROR | MB_OK);
        return 0;
    }

    // 3. Single instance: terminate any other copy, then take the lock that proves
    //    we are the only one left.
    EnforceSingleInstance();
    const InstanceLock instanceLock(kInstanceLockTimeoutMs);
    if (!instanceLock.held()) {
        LOGE(L"Another instance still holds the single-instance lock; exiting.");
        return 0;
    }

    // 4. Everything the service stack owns — the local DNS server's thread, the DNS
    //    policy rule, the children and their job objects — lives inside this object,
    //    and therefore ends before wWinMain returns rather than during static
    //    destruction.
    const Services::Runtime runtime;

    // 5. Only our own child binaries may be running.
    Services::EnforceCleanSlate();

    // Read from the process's own command line rather than from `cmdline` above,
    // which is lpCmdLine and therefore has the program name already removed. The
    // helper tokenizes the full form and knows that; see its declaration for why
    // handing it the stripped string finds nothing.
    const bool autostartMode = Command::IsAutostartLaunch();

    // 6. Every launch needs an accepted agreement before the tray appears. Declining
    //    exits without starting anything.
    if (!Eula::EnsureAccepted(instance)) {
        LOGI(L"User declined the agreement; exiting.");
        return 0;
    }

    if (!Tray::Create(instance)) return 1;

    // Reconcile the desktop shortcut. Skipped in autostart mode: a logon launch must
    // not put a dialog in front of the user, and the shortcut should follow the copy
    // the user launched by hand, not one the scheduler started for them.
    if (!autostartMode) Bootstrap::SyncDesktopShortcut();

    // Check payload presence after the tray is up, so there is always a visible UI.
    // If paths.ini is missing the user is asked once whether to restore it; if they
    // agree the full manifest is fetched and applied — the same engine as a normal
    // update. Returns immediately; the work runs on a detached worker thread inside.
    Controller::RepairIfNeeded();

    // Bring the stack up on a worker thread so the tray appears immediately.
    if (autostartMode) std::thread([] { Services::RunAutostartMode(); }).detach();

    // The silent check is self-threaded and never shows an error dialog, so it cannot
    // delay or disrupt the tray coming up.
    if (AutoUpdateEnabled()) {
        LOGI(L"AutoUpdate is on; running a silent startup update check.");
        Tray::StartSilentUpdateCheck();
    }

    const int exitCode = Tray::RunMessageLoop();

    if (Services::AnyRunning()) Services::Stop();
    Tray::Destroy();
    LOGI(L"Exited.");
    return exitCode;
}
