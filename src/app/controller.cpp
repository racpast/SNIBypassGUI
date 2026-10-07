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
// See the LICENSE.md file in the project root for full license terms.

#include "app/controller.h"

#include <atomic>
#include <mutex>
#include <system_error>
#include <thread>
#include <utility>

#include "app/bootstrap.h"
#include "app/logging.h"
#include "app/services.h"
#include "app/settings.h"
#include "app/text.h"
#include "app/version.h"
#include "platform/autostart.h"
#include "platform/dialogs.h"
#include "platform/shortcut.h"
#include "update/client.h"

namespace Controller {
namespace {

// The attached presenter, read and written only from the UI thread except that a
// worker reads the callbacks while it runs. A worker that reads after Detach() gets
// an empty one and does nothing, which is the behaviour a teardown needs; the
// snapshot below is what keeps that from tearing.
std::mutex g_presenterMx;
Presenter g_presenter;

Presenter Snapshot() {
    std::lock_guard<std::mutex> lock(g_presenterMx);
    return g_presenter;
}

void Balloon(const std::wstring& title, const std::wstring& text) {
    const Presenter p = Snapshot();
    if (p.balloon) p.balloon(title, text);
}

void SetTip(const std::wstring& tip) {
    const Presenter p = Snapshot();
    if (p.setTip) p.setTip(tip);
}

void ResetTip() {
    const Presenter p = Snapshot();
    if (p.resetTip) p.resetTip();
}

void Quit() {
    const Presenter p = Snapshot();
    if (p.quit) p.quit();
}

// ---- Busy gates --------------------------------------------------------------

// Set for the whole lifetime of an update check (fetch, confirm, download, apply).
// While it is set the tray disables "Check for Updates" and Start/Stop, so a second
// check cannot be launched and — critically — the user cannot start the services back
// up in the window where PerformUpdate has stopped them to replace a locked binary.
std::atomic<bool> g_updateBusy{false};

// Set during cache cleanup to prevent Start/Stop operations that would interfere.
std::atomic<bool> g_cleanupBusy{false};

// Clears whichever gate it was given when the worker thread unwinds, on every path.
//
// One type for both gates: they differ only in which flag they release, and the two
// near-identical structs this replaced would have been joined by a third the next time
// an operation wanted a gate of its own.
class BusyGuard {
public:
    explicit BusyGuard(std::atomic<bool>& flag) : m_flag(flag) {}
    ~BusyGuard() { m_flag.store(false, std::memory_order_release); }

    BusyGuard(const BusyGuard&) = delete;
    BusyGuard& operator=(const BusyGuard&) = delete;

private:
    std::atomic<bool>& m_flag;
};

// Claim a gate, or report that someone else holds it. False means an operation of this
// kind is already running and the caller must do nothing. Deciding this before the
// thread exists is what closes the double-click race.
bool Claim(std::atomic<bool>& flag) {
    bool expected = false;
    return flag.compare_exchange_strong(expected, true);
}

// Run `fn` on a detached thread, or report that no thread could be created.
//
// Every command below used to construct a std::thread inline, so a failure to create
// one — resource exhaustion, a handle limit reached — threw std::system_error out of a
// window procedure or a detached worker. Neither has a handler: the first is undefined
// behaviour, the second is an unhandled exception, and both end the process while the
// DNS policy rule is still installed and the child services are still running. The rest
// of the program already guards its own thread creation for that reason (resolver,
// forwarder, redirector, file watcher, supervisor); this is the same guard, in the one
// place the commands live.
//
// `what` names the operation for the log. A false return has already logged why, and
// the caller has nothing to do about it beyond not pretending the command ran.
bool RunDetached(const char* what, std::function<void()> fn) {
    try {
        std::thread(std::move(fn)).detach();
        return true;
    } catch (const std::system_error& e) {
        LOGE(Utf8ToWide(what) + L": could not start a worker thread (" + Utf8ToWide(e.what()) +
             L"); the command was not carried out.");
        return false;
    }
}

// ---- Commands ----------------------------------------------------------------

void DoStart() {
    if (g_updateBusy.load() || g_cleanupBusy.load())
        return;  // an update or cleanup owns the service state
    RunDetached("Start", [] {
        if (Services::Start(true)) Balloon(APP_NAME, T(L"msg.started"));
    });
}

void DoStop() {
    if (g_updateBusy.load() || g_cleanupBusy.load()) return;
    RunDetached("Stop", [] {
        Services::Stop();
        Balloon(APP_NAME, T(L"msg.stopped"));
    });
}

void DoToggleAutostart() {
    RunDetached("Autostart", [] {
        if (Autostart::IsEnabled()) {
            if (Autostart::Disable())
                Balloon(APP_NAME, T(L"msg.autoOff"));
            else
                Dialogs::Show(T(L"msg.autoFail"), MB_ICONERROR);
        } else if (Autostart::Enable()) {
            Balloon(APP_NAME, T(L"msg.autoOn"));
        } else {
            Dialogs::Show(T(L"msg.autoFail"), MB_ICONERROR);
        }
    });
}

void DoSetLang(Lang lang) {
    if (GetLang() == lang) return;
    SetLang(lang);
    // Update the tray icon tooltip to reflect the new language.
    ResetTip();
    // A .lnk stores its description as a literal string, so the shortcut we own would
    // otherwise keep the previous language's text forever. Rewrite it.
    if (GetShortcutPref() == ShortcutPref::Wanted &&
        Shortcut::Inspect() == Shortcut::State::Ours)
        Shortcut::Create();
    Balloon(APP_NAME, T(L"msg.langChanged"));
}

// Download and apply an already-fetched, already-confirmed update. The caller is
// responsible for any confirmation dialog; this function proceeds unconditionally.
// Shared by the interactive update path (which asks first) and the repair path
// (which has already asked its own question).
void ApplyUpdate(const Update::Info& info) {
    const bool wasRunning = Services::AnyRunning();

    uint64_t totalBytes = 0;
    size_t totalFiles = 0;
    for (const Update::File& f : info.files) {
        if (!Update::NeedsUpdate(f, info)) continue;
        totalBytes += f.size;
        ++totalFiles;
    }

    Http::Request transfer;

    const Presenter presenter = Snapshot();
    std::shared_ptr<ProgressWindow::State> window;
    if (presenter.progressBegin) {
        window =
            presenter.progressBegin(totalBytes, totalFiles, [&transfer] { transfer.Stop(); });
    }

    Update::Progress progress;
    progress.request = &transfer;
    size_t filesDone = 0;
    size_t filesTotal = 0;
    progress.onFile = [&filesDone, &filesTotal](size_t done, size_t total,
                                                const std::wstring&) {
        filesDone = done;
        filesTotal = total;
        SetTip(std::wstring(APP_NAME) + T(L"punct.colon") + T(L"msg.updDownloading") + L" (" +
               std::to_wstring(done) + L"/" + std::to_wstring(total) + L")");
    };
    progress.onBytes = [&presenter, &window, &filesDone, &filesTotal](const std::wstring& path,
                                                                      uint64_t bytesOverall) {
        if (presenter.progressReport)
            presenter.progressReport(window, path, bytesOverall, filesDone, filesTotal);
    };
    progress.onBeforeApply = [&presenter, &window, wasRunning] {
        if (presenter.progressApplying) presenter.progressApplying(window);
        SetTip(std::wstring(APP_NAME) + T(L"punct.colon") + T(L"msg.updApplying"));
        if (wasRunning) Services::Stop();
    };

    bool exeSwapPending = false;
    const Update::Outcome outcome = Update::PerformUpdate(info, progress, &exeSwapPending);

    if (presenter.progressEnd) presenter.progressEnd(window);
    window.reset();

    if (outcome == Update::Outcome::Cancelled) {
        LOGI(L"Update: cancelled by the user.");
        ResetTip();
        return;
    }

    if (outcome == Update::Outcome::Failed) {
        ResetTip();
        if (wasRunning && !Services::AnyRunning()) {
            if (!Services::Start(false)) {
                LOGE(L"Update: FAILED to restart services after a failed update.");
                Dialogs::Show(T(L"msg.restartFailed"), MB_ICONERROR);
            }
        }
        return;
    }

    if (exeSwapPending) {
        Quit();
        return;
    }

    ResetTip();
    Balloon(APP_NAME, T(L"msg.updDone"));
    if (wasRunning && !Services::AnyRunning()) {
        if (!Services::Start(false)) {
            LOGE(L"Update: FAILED to restart services after data-only update.");
            Dialogs::Show(T(L"msg.restartFailed"), MB_ICONERROR);
        }
    }
}

// Prompt for and apply an already-fetched update. Assumes info.ok and that an update
// is available; runs on a worker thread. Shared by the manual check and the silent
// startup one.
void PromptAndApply(const Update::Info& info, const std::wstring& summary) {
    // The direction comes from the one place that decides it, so the wording here
    // cannot drift from the update decision itself.
    const Update::UpdateKind kind = Update::Classify(info);
    const bool exeChanges = (kind != Update::UpdateKind::None);
    const bool isDowngrade = (kind == Update::UpdateKind::Downgrade);

    const wchar_t* titleKey =
        exeChanges ? (isDowngrade ? L"msg.updDowngrade" : L"msg.updAvail") : L"msg.updDataOnly";
    const wchar_t* confirmKey =
        exeChanges ? (isDowngrade ? L"msg.updConfirmDowngrade" : L"msg.updConfirm")
                   : L"msg.updConfirmData";

    std::wstring message = std::wstring(T(titleKey)) + L" (" + info.version + L")" +
                           T(L"punct.colonEol") + L"\n\n" + summary;
    // Bulleted here rather than in the manifest. One item reads as a sentence under
    // the heading; several read as a list, which is what BulletList decides — and the
    // cap keeps a long changelog from pushing the question off the dialog.
    if (!info.notes.empty())
        message +=
            std::wstring(L"\n") + T(L"msg.updNotes") + L"\n" + BulletList(info.notes) + L"\n";
    message += std::wstring(L"\n") + T(confirmKey);
    if (Dialogs::Show(message, MB_ICONQUESTION | MB_YESNO) != IDYES) return;

    ApplyUpdate(info);
}

void DoRepairIfNeeded() {
    // Cheap local check first: if the payload descriptor is present, the payload is
    // intact enough to run, so skip the network entirely. This is the common case —
    // a normal install.
    if (Bootstrap::PayloadPresent()) return;

    // Claim the update gate so a concurrent manual check cannot race us. The dialog
    // and the manifest fetch then run on a worker, exactly like a normal update.
    if (!Claim(g_updateBusy)) return;
    if (!RunDetached("Repair", [] {
            BusyGuard guard(g_updateBusy);
            LOGW(L"Repair: meta.ini missing; prompting the user.");
            if (Dialogs::Show(T(L"msg.repairNeeded"), MB_ICONWARNING | MB_YESNO) != IDYES) {
                LOGI(L"Repair: user declined; running without payload.");
                return;
            }
            // User said yes. Fetch the manifest and apply — exactly like an update, but
            // the confirmation dialog is already behind us, so ApplyUpdate skips
            // straight to the download.
            const Update::Info info = Update::FetchManifest();
            if (!info.ok) {
                const std::wstring why =
                    info.error.empty() ? std::wstring(T(L"msg.bootstrapFail")) : info.error;
                Dialogs::Show(why, MB_ICONERROR);
                return;
            }
            ApplyUpdate(info);
        })) {
        g_updateBusy.store(false);
    }
}

void DoCheckUpdate() {
    // Refuse a second check while one is running. Claiming the gate here, before the
    // thread starts, closes the double-click race.
    if (!Claim(g_updateBusy)) return;
    if (!RunDetached("Check for updates", [] {
            BusyGuard guard(g_updateBusy);
            const Update::Info info = Update::FetchManifest();
            if (!info.ok) {
                // FetchManifest sets a specific reason (signature, schema, policy,
                // network); show that rather than a generic failure.
                const std::wstring why =
                    info.error.empty() ? std::wstring(T(L"msg.updFail")) : info.error;
                Dialogs::Show(why, info.reinstallRequired ? MB_ICONWARNING : MB_ICONERROR);
                return;
            }
            std::wstring summary;
            if (!Update::UpdateAvailable(info, summary)) {
                Dialogs::Show(T(L"msg.upToDate"), MB_ICONINFORMATION);
                return;
            }
            PromptAndApply(info, summary);
        })) {
        // No thread means the guard will never run, so the gate has to be released
        // here or every later check would find it held and do nothing.
        g_updateBusy.store(false);
        Dialogs::Show(T(L"msg.updFail"), MB_ICONERROR);
    }
}

void DoUninstall() {
    // The confirmation stays on the calling thread — it is a modal question and
    // nothing else may happen until it is answered. An uninstall that lands while an
    // update is applying, or a cleanup is mid-restart, would delete the tree out from
    // under it, so the same gates that guard Start and Stop guard this too.
    if (g_updateBusy.load() || g_cleanupBusy.load()) return;
    if (Dialogs::Show(T(L"msg.uninstallConfirm"), MB_ICONWARNING | MB_YESNO) != IDYES) return;

    // The work itself runs off the UI thread. It stops the stack, which waits on the
    // same lock every command uses, and a Start already under way holds that lock for
    // as long as a child takes to bind its port — up to ten seconds of a frozen tray
    // if this ran here.
    RunDetached("Uninstall", [] {
        Services::Uninstall();
        // Not a direct teardown: the caller is a worker, and the window belongs to
        // another thread. The presenter posts the close, which is what lets the UI
        // thread run wWinMain's teardown. See the Presenter contract in controller.h.
        Quit();
    });
}

void DoCleanCache() {
    // Refuse a second cleanup while one is running.
    if (!Claim(g_cleanupBusy)) return;
    if (!RunDetached("Clean cache", [] {
            BusyGuard guard(g_cleanupBusy);
            SetTip(std::wstring(APP_NAME) + T(L"punct.colon") + T(L"msg.cleaningCache"));
            const Services::CacheCleanResult result = Services::CleanCache();
            ResetTip();
            if (!result.ok) {
                Dialogs::Show(T(L"msg.restartFailed"), MB_ICONERROR);
                return;
            }
            std::wstring message = T(L"msg.cacheClean");
            message += std::wstring(L"\n") + std::to_wstring(result.deleted) + L" " +
                       T(L"msg.itemsDeleted");
            Balloon(APP_NAME, message);
        })) {
        g_cleanupBusy.store(false);
        Dialogs::Show(T(L"msg.restartFailed"), MB_ICONERROR);
    }
}

}  // namespace

void Attach(Presenter presenter) {
    std::lock_guard<std::mutex> lock(g_presenterMx);
    g_presenter = std::move(presenter);
}

void Detach() {
    std::lock_guard<std::mutex> lock(g_presenterMx);
    g_presenter = Presenter{};
}

bool Busy() {
    return g_updateBusy.load() || g_cleanupBusy.load();
}

void Start() {
    DoStart();
}

void Stop() {
    DoStop();
}

void ToggleAutostart() {
    DoToggleAutostart();
}

void SetLanguage(Lang lang) {
    DoSetLang(lang);
}

void CheckForUpdates() {
    DoCheckUpdate();
}

// Any failure — network, signature, policy — is swallowed to a log line: an automatic
// check must never nag with an error dialog. If an update is available, the same
// prompt/confirm/apply path as the manual check is reused.
void StartSilentUpdateCheck() {
    if (!Claim(g_updateBusy)) return;
    if (!RunDetached("Silent update check", [] {
            BusyGuard guard(g_updateBusy);
            const Update::Info info = Update::FetchManifest();
            if (!info.ok) {
                LOGW(L"Auto-update check failed: " +
                     (info.error.empty() ? std::wstring(L"unknown error") : info.error));
                return;
            }
            std::wstring summary;
            if (!Update::UpdateAvailable(info, summary)) {
                LOGI(L"Auto-update check: already up to date (" + info.version + L").");
                return;
            }
            PromptAndApply(info, summary);
        })) {
        g_updateBusy.store(false);
    }
}

void RepairIfNeeded() {
    DoRepairIfNeeded();
}

void Uninstall() {
    DoUninstall();
}

void CleanCache() {
    DoCleanCache();
}

}  // namespace Controller
