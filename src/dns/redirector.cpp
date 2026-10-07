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

#include "dns/redirector.h"

#include <windows.h>

#include <cstdint>
#include <system_error>
#include <utility>
#include <vector>

#include "app/logging.h"
#include "app/text.h"
#include "dns/file_watcher.h"
#include "dns/nrpt.h"

namespace Dns {

bool RepairBudget::Allow(uint64_t nowMs) {
    if (!started || nowMs - windowStart >= kWindowMs) {
        started = true;
        windowStart = nowMs;
        used = 0;
    }
    return ++used <= kMaxRepairs;
}

Redirector::Redirector() : m_rules(std::make_shared<const RuleSet>()) {}

Redirector::~Redirector() {
    Stop();
    if (m_guardCancel) CloseHandle(m_guardCancel);
    if (m_failed) CloseHandle(m_failed);
}

std::shared_ptr<const RuleSet> Redirector::Rules() const {
    std::lock_guard<std::mutex> lock(m_mx);
    return m_rules;
}

size_t Redirector::RuleCount() const {
    return Rules()->RuleCount();
}

size_t Redirector::LoadRules(const std::wstring& path) {
    size_t loaded = 0;
    std::shared_ptr<const RuleSet> rules = LoadRuleFile(path, L"hosts", loaded);
    if (!rules) {
        LOGE(L"Cannot read the DNS rule file: " + path);
        return 0;
    }
    Apply(std::move(rules));
    return loaded;
}

void Redirector::SetEndpoint(const BindEndpoint& endpoint) {
    // The resolver is told first and the rule is brought in line under the table
    // lock, in that order, because the rule must never name an endpoint the server
    // has not been told about.
    //
    // A running redirector is re-pointed rather than restarted: the resolver's
    // SetEndpoint takes effect on its next Start, so the rule keeps naming the
    // endpoint the server is actually bound to until one happens. Writing the new
    // address into the table now would route every listed name to a port nothing is
    // listening on — the exact state this class exists to avoid.
    std::lock_guard<std::mutex> table(m_tableMx);
    m_endpoint = endpoint;
    m_resolver.SetEndpoint(endpoint);

    if (m_resolver.Running()) {
        LOGW(L"DNS redirection: the endpoint changed to " + endpoint.Text() +
             L" while the stack is running; it takes effect on the next start.");
    }
}

void Redirector::Apply(std::shared_ptr<const RuleSet> rules) {
    const std::vector<std::string> namespaces = rules->Namespaces();

    // Held across the whole adoption, so the guardian cannot catch the table
    // mid-rewrite and mistake it for interference.
    std::lock_guard<std::mutex> table(m_tableMx);
    {
        std::lock_guard<std::mutex> lock(m_mx);
        m_rules = rules;
    }

    // The resolver learns the rules before the policy table is told to send names
    // to it. The other order has a window in which Windows routes a name here and
    // the answer comes from the rule set being replaced.
    m_resolver.Publish(std::move(rules));

    // Reinstalled unconditionally rather than only when the namespace list has
    // changed. Writing what the table should say — instead of trusting a memory of
    // what it was last told — costs a registry write on a file the user just saved,
    // and keeps a reload and the guardian's repair as the same operation.
    if (m_resolver.Running()) Nrpt::InstallRule(namespaces, Nrpt::ServerField(m_endpoint));
}

bool Redirector::Start() {
    // A previous session may have ended without anyone tearing it down. Stopping the
    // guardian first means the resolver restart below cannot be mistaken by a
    // leftover watcher for the server dying on its own.
    StopGuardian();

    // A rule set that names nothing is not a failure. The file may simply have no
    // rules in it yet, and nothing about the machine is wrong — so this is something
    // to say out loud, not a reason to refuse.
    //
    // The server is started regardless, and that is the point. With no namespace
    // installed nothing is routed to it, so it sits idle — but it IS running, which
    // is what lets a later edit to the rule file bring redirection up through the
    // ordinary hot-reload path. Refusing to start would leave a reload with nothing
    // to publish to, and the user would have to restart the stack by hand to be heard.
    const std::vector<std::string> namespaces = Rules()->Namespaces();
    if (namespaces.empty())
        LOGW(
            L"No DNS rules are loaded, so no domain is redirected yet; the local DNS "
            L"server is starting anyway and will pick the file up when it changes.");

    // Nothing is routed anywhere until there is something listening to route to.
    if (!m_resolver.Start()) return false;

    // An empty list installs no rule and removes any earlier one, which is exactly
    // right: there is nothing to route.
    {
        std::lock_guard<std::mutex> table(m_tableMx);
        if (!Nrpt::InstallRule(namespaces, Nrpt::ServerField(m_endpoint))) {
            RollBackStart();
            return false;
        }
    }

    // Last, and only once both halves are in place: the guardian's first act is to
    // compare the table against what it should say, and starting it any earlier
    // would have it race the install it is meant to be checking.
    //
    // A redirection that cannot be watched is refused outright. The header's whole
    // account of what is safe to leave running rests on the guardian noticing a
    // server that has stopped answering while the rule still points at it — without
    // it, that state is not merely unwatched, it is unobservable, and every names
    // the rule covers hangs until the client gives up. Saying so beats returning
    // true over it.
    if (!StartGuardian()) {
        LOGE(L"DNS redirection could not be started: there is no guardian to watch it.");
        RollBackStart();
        return false;
    }
    return true;
}

// Start has given up. Take back the two halves in the order Stop takes them —
// names stop being sent here before the server that answers them goes away, so no
// query is ever routed to a port that has just closed — and clear the failure that
// the attempt recorded, because a Start that returned false has no outstanding
// failure to report: the caller was told by the return value, and Stop's contract
// is that a stopped object leaves nothing in a failed state to describe.
void Redirector::RollBackStart() {
    StopGuardian();
    {
        std::lock_guard<std::mutex> table(m_tableMx);
        Nrpt::RemoveRule();
    }
    m_resolver.Stop();
    m_failure.store(RedirectFailure::None);
    if (m_failed) ResetEvent(m_failed);
}

void Redirector::Stop() {
    // First. Everything below is this program taking redirection down on purpose,
    // and the guardian exists to react to redirection going down on its own — left
    // running, it would see our own RemoveRule and dutifully put the rule back.
    StopGuardian();

    // The watcher belongs to the session, not to this object's lifetime: left for
    // the destructor, its thread would outlive every caller's idea of "stopped".
    DisableHotReload();

    // Names stop being sent here before the server that answers them goes away, so
    // no query is ever routed to a port that has just closed.
    {
        std::lock_guard<std::mutex> table(m_tableMx);
        Nrpt::RemoveRule();
    }
    m_resolver.Stop();

    // Last, once there is nothing left in a failed state to describe. Clearing it
    // here rather than at the next start is what keeps Active() derived from what is
    // actually outstanding instead of remembered: after this returns, this object
    // holds no thread, no socket and no registry key, and says so.
    m_failure.store(RedirectFailure::None);
    if (m_failed) ResetEvent(m_failed);
}

void Redirector::EnableHotReload(const std::wstring& path, unsigned debounceMs) {
    if (m_watcher && m_rulesPath == path) return;
    DisableHotReload();
    m_rulesPath = path;

    m_watcher = std::make_unique<FileWatcher>(
        path,
        [this, path] {
            LOGI(L"Hot-reload: reloading DNS rules from " + path);
            size_t loaded = 0;
            std::shared_ptr<const RuleSet> rules = LoadRuleFile(path, L"Hot-reload", loaded);
            if (!rules) {
                LOGE(L"Hot-reload: cannot open " + path);
                return;
            }
            Apply(std::move(rules));
            LOGI(L"Hot-reload: loaded " + std::to_wstring(loaded) + L" rule(s).");
        },
        debounceMs);

    m_watcher->Start();
}

void Redirector::DisableHotReload() {
    if (m_watcher) {
        m_watcher->Stop();
        m_watcher.reset();
    }
    m_rulesPath.clear();
}

// ---- The guardian ------------------------------------------------------------

void Redirector::Fail(RedirectFailure cause, const wchar_t* detail) {
    LOGE(std::wstring(L"DNS redirection has stopped: ") + detail);
    m_failure.store(cause);
    SetEvent(m_failed);
}

bool Redirector::StartGuardian() {
    StopGuardian();

    // A previous session's failure is not this one's, and Stop has already cleared
    // it on every path that goes through one. Cleared again here, before anything
    // below can fail, so that a Start reached without a Stop still begins from a
    // state that describes this incarnation and not the last.
    m_failure.store(RedirectFailure::None);
    m_repairs = RepairBudget();

    if (!m_guardCancel) m_guardCancel = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    if (!m_failed) m_failed = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    if (!m_guardCancel || !m_failed) {
        LOGE(L"DNS redirection: cannot create the guardian's events (err " +
             std::to_wstring(GetLastError()) + L").");
        return false;
    }
    ResetEvent(m_guardCancel);
    ResetEvent(m_failed);

    // The handles above outlive a failed attempt on purpose: they are opened once
    // and reused, and handing them back to the kernel would only mean opening them
    // again on the next Start.
    try {
        m_guard = std::thread([this] { Guard(); });
    } catch (const std::system_error& e) {
        LOGE(L"DNS redirection: cannot create the guardian thread (" + Utf8ToWide(e.what()) +
             L").");
        return false;
    }
    return true;
}

void Redirector::StopGuardian() {
    if (m_guardCancel) SetEvent(m_guardCancel);
    if (m_guard.joinable()) m_guard.join();
}

// Bring the policy table back in line with the rules, if it has drifted.
//
// Reading before writing is what stops this from notifying itself forever — our own
// install is a change to the table, so an unconditional rewrite would wake the
// guardian, which would rewrite, which would wake it again. Comparing converges: the
// repair fires a notification, the notification finds the table already correct, and
// there it stops.
Redirector::RuleGuard Redirector::RepairRuleIfNeeded() {
    // Under the table lock, so what is read is a finished state and never one of our
    // own installs half done.
    std::lock_guard<std::mutex> table(m_tableMx);
    const std::vector<std::string> namespaces = Rules()->Namespaces();
    if (Nrpt::RuleMatches(namespaces, Nrpt::ServerField(m_endpoint))) return RuleGuard::Held;

    if (!m_repairs.Allow(GetTickCount64())) {
        Fail(RedirectFailure::RuleUnholdable,
             L"the DNS policy rule was removed again as fast as it could be restored. "
             L"Something else on this machine is removing it.");
        return RuleGuard::Lost;
    }

    LOGW(L"NRPT: the policy rule no longer says what it should; restoring it.");
    if (!Nrpt::InstallRule(namespaces, Nrpt::ServerField(m_endpoint))) {
        Fail(RedirectFailure::RuleUnholdable,
             L"the DNS policy rule was removed and could not be written back.");
        return RuleGuard::Lost;
    }

    // Confirm our own write, and treat failing to recognise it as a different thing
    // from losing the rule.
    //
    // If the table does not read back as what was just successfully written to it,
    // the rule is installed and redirection is working — what has failed is this
    // code's ability to tell "correct" from "tampered with". Repairing again would
    // find the same disagreement and repair again, and the budget would run out in
    // milliseconds and take down a stack that is doing its job. So the watch is given
    // up and the server keeps being watched, which is the honest description of what
    // is left.
    if (!Nrpt::RuleMatches(namespaces, Nrpt::ServerField(m_endpoint))) {
        LOGE(
            L"NRPT: the policy rule was written but does not read back as what was "
            L"written. Redirection is up and the rule is installed; it will no longer "
            L"be checked, because a check that cannot recognise its own writing would "
            L"rewrite the rule without end.");
        return RuleGuard::Unwatchable;
    }
    return RuleGuard::Held;
}

// One thread, three things it can be woken by, and not one interval among them.
//
// Called on its own thread and never on any other: the policy-table subscription is
// bound to whichever thread registered it (see dns/nrpt.h), so opening the watch,
// re-arming it and waiting on it all happen here or the subscription silently stops
// arriving.
void Redirector::Guard() {
    Nrpt::RuleWatch watch;
    bool watchingRule = watch.Open();
    if (!watchingRule)
        LOGW(
            L"NRPT: the policy table cannot be watched on this machine; a rule removed "
            L"by other software will not be repaired. The local DNS server is still "
            L"watched.");

    // Once before waiting on anything. A subscription only covers changes made after
    // it was taken, so a rule deleted between the install that Start just did and the
    // Open above would otherwise never be noticed at all.
    if (watchingRule) {
        const RuleGuard verdict = RepairRuleIfNeeded();
        if (verdict == RuleGuard::Lost) return;
        if (verdict == RuleGuard::Unwatchable) watchingRule = false;
    }

    // Cancellation first, so it wins a tie: WaitForMultipleObjects reports the
    // lowest signalled index, and a stop arriving at the same instant as a failure
    // is a stop.
    enum : std::uint8_t { kCancel = 0, kServerStopped, kRuleChanged, kWaitCount };
    HANDLE waits[kWaitCount] = {};
    waits[kCancel] = m_guardCancel;
    waits[kServerStopped] = m_resolver.stoppedHandle();
    waits[kRuleChanged] = watch.handle();

    for (;;) {
        const DWORD count = watchingRule ? kWaitCount : kRuleChanged;
        const DWORD result = WaitForMultipleObjects(count, waits, FALSE, INFINITE);
        const DWORD index = result - WAIT_OBJECT_0;
        if (result == WAIT_FAILED || index >= count) {
            // A watchdog that stopped watching is worth more in the log than a
            // silence that looks like health. Not treated as a redirection failure:
            // nothing about redirection has changed, only our ability to see it.
            LOGE(L"DNS redirection: the guardian stopped waiting unexpectedly (err " +
                 std::to_wstring(GetLastError()) +
                 L"); redirection that stops on its own will go unnoticed.");
            return;
        }

        if (index == kCancel) return;

        if (index == kServerStopped) {
            // Not repairable from here. select() failing is the machine's networking
            // being wrong, and restarting into it would be a loop; what the sockets
            // are left in — bound, unread, still routed to — is the one state worth
            // escaping at any cost.
            Fail(RedirectFailure::ServerStopped,
                 L"the local DNS server's loop ended on its own.");
            return;
        }

        // Re-arm BEFORE reading, so the window the read occupies is already covered
        // by the next subscription rather than falling outside both.
        //
        // A subscription that cannot be renewed costs the rule its watch and nothing
        // else: the wait narrows to the two handles that still work rather than
        // ending, because losing the ability to repair a deleted rule is no reason to
        // also stop noticing a server that has stopped answering.
        if (!watch.Rearm()) {
            LOGW(
                L"NRPT: the policy table can no longer be watched; a rule removed by "
                L"other software will not be repaired. The local DNS server is still "
                L"watched.");
            watchingRule = false;
            continue;
        }

        const RuleGuard verdict = RepairRuleIfNeeded();
        if (verdict == RuleGuard::Lost) return;
        if (verdict == RuleGuard::Unwatchable) watchingRule = false;
    }
}

}  // namespace Dns
