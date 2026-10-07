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

#include "app/services.h"

#include <windows.h>

#include <wincrypt.h>

#include <algorithm>
#include <atomic>
#include <chrono>
#include <cstdint>
#include <cwchar>
#include <functional>
#include <mutex>
#include <optional>
#include <system_error>
#include <thread>
#include <utility>
#include <vector>

#include "app/filesystem.h"
#include "app/i18n.h"
#include "app/logging.h"
#include "app/paths.h"
#include "app/payload.h"
#include "app/text.h"
#include "app/version.h"
#include "dns/dns_proxy.h"
#include "dns/nrpt.h"
#include "dns/redirector.h"
#include "platform/autostart.h"
#include "platform/command.h"
#include "platform/dialogs.h"
#include "platform/ini.h"
#include "platform/ports.h"
#include "platform/process.h"
#include "platform/shortcut.h"
#include "updater/module.h"
#include "updater/plan.h"

namespace Services {

namespace {

// Stack components: any failure stops all (prevents unresponsive DNS).
enum class Component : std::uint8_t { Nginx, SniGate, DnsRedirection, DnsProxy };

// For the log: stable, ASCII identifier.
const wchar_t* ComponentLogName(Component c) {
    switch (c) {
        case Component::Nginx: return L"nginx";
        case Component::SniGate: return L"sni-gate";
        case Component::DnsRedirection: return L"DNS redirection";
        case Component::DnsProxy: return L"DNS proxy";
    }
    return L"";
}

// For UI: localized name key (keeps dialogs and menu in sync).
const wchar_t* ComponentNameKey(Component c) {
    switch (c) {
        case Component::Nginx: return L"status.nginx";
        case Component::SniGate: return L"status.route";
        case Component::DnsRedirection: return L"status.dns";
        case Component::DnsProxy: return L"status.proxy";
    }
    return L"";
}

// Watches running stack for unexpected component failure.
// Event-driven wait on kernel objects (process handles, DNS component signals).
// Observes failures instantly, no polling. Handles already-down components at
// startup. Arm/Disarm not thread-safe (callers hold operation mutex).
class StackSupervisor {
public:
    ~StackSupervisor() { Disarm(); }

    StackSupervisor() = default;
    StackSupervisor(const StackSupervisor&) = delete;
    StackSupervisor& operator=(const StackSupervisor&) = delete;

    // One component, and the handle that is signalled when it is no longer running.
    struct Watched {
        Component component;
        HANDLE signal;
    };

    // Watch each entry until one signals or Disarm is called, then call onDown.
    // Handles must outlive the armed supervisor (callers guarantee this).
    void Arm(std::vector<Watched> watched, std::function<void(Component)> onDown);

    // Stop watching and return once nothing is waiting on handles. Idempotent.
    void Disarm();

private:
    void Run();

    // Manual-reset: once set, stays set (no interleaving can miss it).
    HANDLE m_cancel = nullptr;    // "stop watching"
    HANDLE m_finished = nullptr;  // "no longer waiting on anything, and no member of
                                  //  this object will be touched again"
    bool m_armed = false;
    std::vector<Watched> m_watched;
    std::function<void(Component)> m_onDown;
};

void StackSupervisor::Arm(std::vector<Watched> watched, std::function<void(Component)> onDown) {
    Disarm();

    // Remove components with null handles (prevents WaitForMultipleObjects failure).
    watched.erase(std::remove_if(watched.begin(), watched.end(),
                                 [](const Watched& w) {
                                     if (w.signal) return false;
                                     LOGE(std::wstring(ComponentLogName(w.component)) +
                                          L" cannot be watched; if it stops on its own "
                                          L"it will go unnoticed.");
                                     return true;
                                 }),
                  watched.end());
    if (watched.empty() || !onDown) return;

    m_cancel = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    m_finished = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    if (!m_cancel || !m_finished) {
        LOGE(L"Could not create the supervisor's events (err " +
             std::to_wstring(GetLastError()) +
             L"); a service that stops on its own will go unnoticed.");
        if (m_cancel) CloseHandle(m_cancel);
        if (m_finished) CloseHandle(m_finished);
        m_cancel = nullptr;
        m_finished = nullptr;
        return;
    }

    m_watched = std::move(watched);
    m_onDown = std::move(onDown);

    // Create supervisor thread here (not on failure). Exception here is reportable;
    // exception during failure reaction kills process with DNS policy still installed.
    try {
        std::thread(&StackSupervisor::Run, this).detach();
    } catch (const std::system_error& e) {
        LOGE(L"Could not create the supervisor thread (" + Utf8ToWide(e.what()) +
             L"); a service that stops on its own will go unnoticed.");
        CloseHandle(m_cancel);
        CloseHandle(m_finished);
        m_cancel = nullptr;
        m_finished = nullptr;
        m_watched.clear();
        m_onDown = nullptr;
        return;
    }
    m_armed = true;
}

void StackSupervisor::Run() {
    // Cancellation goes first so its index is known without arithmetic.
    std::vector<HANDLE> waits;
    waits.reserve(m_watched.size() + 1);
    waits.push_back(m_cancel);
    for (const Watched& w : m_watched) waits.push_back(w.signal);

    const DWORD result =
        WaitForMultipleObjects(static_cast<DWORD>(waits.size()), waits.data(), FALSE, INFINITE);
    const DWORD index = result - WAIT_OBJECT_0;

    // Move handler out (makes Disarm safe: won't destroy callable mid-execution).
    std::optional<Component> down;
    std::function<void(Component)> reaction;
    if (result != WAIT_OBJECT_0) {
        if (index >= waits.size()) {
            // WAIT_FAILED or abandoned: supervisor stopped. Log it (silent failure looks like health).
            LOGE(L"The service supervisor stopped waiting unexpectedly (err " +
                 std::to_wstring(GetLastError()) +
                 L"); a service that stops on its own will go unnoticed until the "
                 L"next start.");
        } else {
            down = m_watched[index - 1].component;
            reaction = std::move(m_onDown);
        }
    }

    // Published before the reaction runs, and this is the whole reason the reaction
    // Signal finished: supervisor may be destroyed while reaction runs (works on local copies only).
    SetEvent(m_finished);
    if (down) reaction(*down);
}

void StackSupervisor::Disarm() {
    if (!m_armed) return;
    SetEvent(m_cancel);
    WaitForSingleObject(m_finished, INFINITE);
    CloseHandle(m_cancel);
    CloseHandle(m_finished);
    m_cancel = nullptr;
    m_finished = nullptr;
    m_watched.clear();
    m_onDown = nullptr;  // already empty when a reaction is under way
    m_armed = false;
}

}  // namespace

struct Runtime::State {
    // Teardown flag: prevents detached worker from restarting stack during shutdown.
    std::atomic<bool> shuttingDown{false};

    // Serializes Start/Stop/CleanCache so overlapping tray actions cannot interleave
    // process launches and DNS redirection state against each other.
    std::mutex operationMutex;

    // Generation counter: prevents stale component-down reports after restart.
    // Incremented under operationMutex when arming/stopping.
    uint64_t generation = 0;

    // Guards the two child slots on their own, so the tray can ask what is running
    // without waiting behind a start that is still bringing the stack up.
    // Always taken after operationMutex when both are held.
    std::mutex childMutex;
    Process::Child nginx;
    Process::Child sniGate;

    // DNS redirection: a policy-table rule routes every listed namespace to a local
    // DNS server, which answers the redirected names with loopback and forwards
    // everything else to the machine's real resolvers.
    Dns::Redirector redirector;

    // DNS forwarder: forwards queries to DoH/DoT/DNSCrypt upstreams with racing.
    Dns::DnsProxy proxy;

    // Declared last: destroyed first (disarms before handles it waits on are destroyed).
    StackSupervisor supervisor;
};

namespace {

// ---- Payload-declared locations ---------------------------------------------

// Cached payload descriptor (meta.ini). Invalidated on file change (size/mtime).
// Thread-safe: returns by value. Caches entire descriptor, not just [Paths].
struct PayloadStamp {
    long long size = -1;
    long long mtime = -1;
    bool known = false;

    bool operator==(const PayloadStamp& o) const {
        return size == o.size && mtime == o.mtime && known == o.known;
    }
};

std::mutex g_payloadMx;
PayloadStamp g_payloadStamp;
std::vector<Ini::Section> g_payloadSections;

PayloadStamp StampOfPayloadFile() {
    PayloadStamp stamp;
    WIN32_FILE_ATTRIBUTE_DATA data = {};
    if (!GetFileAttributesExW(Payload::ConfigPath().c_str(), GetFileExInfoStandard, &data))
        return stamp;
    stamp.size =
        static_cast<long long>((static_cast<unsigned long long>(data.nFileSizeHigh) << 32u) |
                               static_cast<unsigned long long>(data.nFileSizeLow));
    stamp.mtime = static_cast<long long>(
        (static_cast<unsigned long long>(data.ftLastWriteTime.dwHighDateTime) << 32u) |
        static_cast<unsigned long long>(data.ftLastWriteTime.dwLowDateTime));
    stamp.known = true;
    return stamp;
}

std::vector<Ini::Section> PayloadSections() {
    const std::wstring path = Payload::ConfigPath();
    const PayloadStamp stamp = StampOfPayloadFile();

    std::lock_guard<std::mutex> lock(g_payloadMx);
    if (g_payloadStamp == stamp && g_payloadStamp.known) return g_payloadSections;

    g_payloadSections = Ini::Read(path);
    g_payloadStamp = stamp;
    return g_payloadSections;
}

// Read one string value out of the payload descriptor.
//
// This goes through Ini::Value rather than GetPrivateProfileStringW, because the
// profile API cannot serve a value longer than 32767 characters: at exactly 32768 it
// returns 0 and a character later it returns a wrapped fragment, so a caller that
// checks only "did I get something" receives a short value that looks real. That is
// not a cosmetic loss for these entries — they are glob patterns the uninstall and
// cache-clean paths delete by, and a pattern cut in the middle ("data\nginx*" from
// "data\nginx*something") is still a valid wildcard that CompilePattern accepts and
// DeleteByPatterns then acts on. The reader in platform/ini parses the file directly,
// so the value arrives whole or not at all.
std::wstring ResolvedPath(const wchar_t* key, const std::wstring& fallback) {
    // An unreadable or oversized value falls back to the built-in default rather than
    // resolving a truncated path to a directory that is not the one intended.
    std::wstring rel = Ini::Value(PayloadSections(), L"Paths", key);
    if (rel.empty()) rel = fallback;
    return PathUnder(rel);
}

// Redirect-rule source: hosts-style "ACTION domain..." lines (see dns/rules.h).
std::wstring DnsRulesPath() {
    return ResolvedPath(L"Hosts", L"data\\dns_hosts.txt");
}

// Directory containing exe, with trailing backslash. No separator = own directory.
std::wstring DirOf(const std::wstring& exe) {
    const std::wstring dir = DirPart(exe);
    return dir.empty() ? ExeDir() : dir;
}

// ---- Timing constants --------------------------------------------------------

// How long to wait for a stopped service's port to disappear from the TCP table.
constexpr DWORD kPortReleaseTimeoutMs = 5000;

// Poll interval for port release (no kernel event for socket close).
constexpr DWORD kPortPollMs = 50;

// Wait for service ports to leave TCP table (ensures next bind succeeds).
void AwaitPortsReleased(DWORD timeoutMs) {
    const ULONGLONG deadline = GetTickCount64() + timeoutMs;
    for (;;) {
        // Test the same condition the start will check (bindable, not just table-absent).
        //
        // The whole set at once, because availability is not a property of any single
        // claim: two of ours that could not coexist would each answer "free" and the
        // set would still be unbindable.
        if (Ports::CanBindAll(RequiredPorts())) return;
        if (GetTickCount64() >= deadline) {
            LOGW(
                L"A required endpoint is still unavailable after stopping; a restart may "
                L"fail to bind.");
            return;
        }
        Sleep(kPortPollMs);
    }
}

// Drop the OS resolver cache. Synthesized answers carry a short TTL, so without
// this a start would be shadowed by cached real addresses, and a stop would keep
// sending traffic to a loopback that no longer listens until the TTL expired.
//
// Bounded well below Command::RunHidden's default. This runs inside StopLocked, which
// the exit path waits on, so its timeout is part of how long closing the program can
// take — and ipconfig answers locally in milliseconds or not at all. A flush that has
// not completed in a few seconds is not going to, and the cache expiring on its own
// TTL is a far smaller cost than a program that will not close.
constexpr DWORD kFlushTimeoutMs = 5000;

void FlushResolverCache() {
    Command::RunHidden(L"ipconfig /flushdns", kFlushTimeoutMs);
}

// Published state and lock for thread-safe access.
// Callers get shared_ptr (state survives in-flight calls after Runtime destroyed).
// SRWLOCK trivially destructible; shared_ptr in leaked heap object (safe for
// detached workers accessing after main() returns).
SRWLOCK g_stateLock = SRWLOCK_INIT;

std::shared_ptr<Runtime::State>& StateSlot() {
    static auto* slot = new std::shared_ptr<Runtime::State>();
    return *slot;
}

std::shared_ptr<Runtime::State> Ctx() {
    AcquireSRWLockShared(&g_stateLock);
    std::shared_ptr<Runtime::State> state = StateSlot();
    ReleaseSRWLockShared(&g_stateLock);
    return state;
}

void PublishState(std::shared_ptr<Runtime::State> state) {
    AcquireSRWLockExclusive(&g_stateLock);
    StateSlot() = std::move(state);
    ReleaseSRWLockExclusive(&g_stateLock);
}

// Is this service running? Checks:
// 1. Our launched child (via handle, PID-reuse safe)
// 2. Same executable running externally (prevents "Stopped" when ports are held)
bool ChildRunning(const std::shared_ptr<Runtime::State>& state,
                  Process::Child Runtime::State::* slot, const std::wstring& exe) {
    if (state) {
        std::lock_guard<std::mutex> lock(state->childMutex);
        if (((*state).*slot).Running()) return true;
    }
    return !Process::FindByImagePath(exe).empty();
}

// Whether any part of the stack we launched still needs teardown (threads/sockets/registry).
// Not about health: a failed component still holds resources that must be cleaned up.
bool AnythingRunning(Runtime::State& state) {
    if (state.redirector.Active() || state.proxy.Running()) return true;
    std::lock_guard<std::mutex> lock(state.childMutex);
    return static_cast<bool>(state.nginx) || static_cast<bool>(state.sniGate);
}

// ---- Start / stop, assuming operationMutex is held ---------------------------

// Launch child. False means launch failed (logged). Health checked by supervisor, not here.
bool StartChild(Runtime::State& state, Process::Child Runtime::State::* slot,
                Component component, const std::wstring& exe) {
    const std::wstring name = ComponentLogName(component);
    LOGI(L"Starting " + name + L": " + exe);

    // Capture child output to log (both streams: stderr vs stdout is payload-dependent).
    const std::wstring prefix = L"[" + name + L"] ";
    const Process::OutputSink sink = [prefix](Process::OutputLine&& line) {
        LogLine(L"CHILD",
                prefix +
                    (line.channel == Process::Channel::Stdout ? L"[stdout] " : L"[stderr] ") +
                    line.text);
    };

    // nginx derives prefix from module path (wide API), no args needed.
    // Working directory passed as UTF-16 by kernel (survives non-ASCII paths).
    Process::Child child = Process::LaunchChild(exe, L"", {DirOf(exe), sink});
    if (!child) return false;

    LOGI(name + L" started (pid " + std::to_wstring(child.pid()) + L").");

    // Move old child out under lock, destroy after (prevents tray freeze).
    Process::Child replaced;
    {
        std::lock_guard<std::mutex> lock(state.childMutex);
        replaced = std::move(state.*slot);
        state.*slot = std::move(child);
    }
    return true;
}

void StopChildren(Runtime::State& state) {
    // Move children out under lock, terminate outside (keeps tray responsive).
    Process::Child nginx;
    Process::Child sniGate;
    {
        std::lock_guard<std::mutex> lock(state.childMutex);
        nginx = std::move(state.nginx);
        sniGate = std::move(state.sniGate);
    }
    if (nginx) {
        LOGI(L"Stopping nginx (pid " + std::to_wstring(nginx.pid()) + L")");
        nginx.Terminate();
    }
    if (sniGate) {
        LOGI(L"Stopping sni-gate (pid " + std::to_wstring(sniGate.pid()) + L")");
        sniGate.Terminate();
    }

    // Also stop externally launched copies (opened from data folder by hand).
    // Full path matching prevents affecting unrelated programs.
    Process::TerminateByImagePath(NginxExe());
    Process::TerminateByImagePath(SniGateExe());
}

void StopLocked(Runtime::State& state) {
    // Disarm supervisor first (every death after this is intentional).
    state.supervisor.Disarm();

    // Bump generation (invalidates in-flight reports from old stack).
    ++state.generation;

    // Remove policy rule + flush DNS cache (clears loopback answers, resolves real immediately).
    state.redirector.Stop();
    state.proxy.Stop();
    FlushResolverCache();

    StopChildren(state);
    AwaitPortsReleased(kPortReleaseTimeoutMs);
    LOGI(L"Services stopped.");
}

// Build service error dialog: translated name + reason + consequence.
std::wstring ServiceProblem(Component component, const wchar_t* reasonKey) {
    return std::wstring(T(ComponentNameKey(component))) + T(L"punct.colon") + T(reasonKey) +
           L"\n" + T(L"msg.serviceFailed");
}

// Start failure carried out of critical section (prevents dialog inside operationMutex).
// Set() gates on canShow (logon path never shows dialogs during sign-in).
struct StartFailure {
    std::wstring message;

    bool pending() const { return !message.empty(); }

    void Set(bool canShow, std::wstring text) {
        if (!canShow || text.empty()) return;
        message = std::move(text);
    }
};

// Report a failed start through `failure`, which decides whether the user is told.
void ReportStartFailure(StartFailure& failure, bool interactive, Component component,
                        const wchar_t* reasonKey) {
    failure.Set(interactive, ServiceProblem(component, reasonKey));
}

// Translation key for why component stopped (DNS redirection distinguishes rule-deleted vs server-stopped).
const wchar_t* ReasonKeyFor(Runtime::State& state, Component component) {
    if (component == Component::DnsProxy) return L"reason.dnsProxyStopped";
    if (component != Component::DnsRedirection) return L"reason.exitedWhileRunning";
    return state.redirector.failure() == Dns::RedirectFailure::RuleUnholdable
               ? L"reason.dnsRuleRemoved"
               : L"reason.dnsServerStopped";
}

// The exit code, for the log, when the component that stopped is one that has one.
std::wstring ExitDetail(Runtime::State& state, Component component) {
    std::lock_guard<std::mutex> lock(state.childMutex);
    const Process::Child* child = nullptr;
    if (component == Component::Nginx) child = &state.nginx;
    if (component == Component::SniGate) child = &state.sniGate;

    DWORD code = 0;
    if (child && child->ExitCode(code)) return L" (exit code " + std::to_wstring(code) + L")";
    return L"";
}

// Component stopped: tear down rest, show dialog. `generation` prevents stale reports.
void OnComponentDown(Component component, uint64_t generation) {
    const std::shared_ptr<Runtime::State> state = Ctx();
    if (!state) return;  // the runtime is gone; there is nothing left to take down

    // Compose message under lock (reason belongs to this incarnation, read before teardown).
    std::wstring message;
    {
        std::lock_guard<std::mutex> lock(state->operationMutex);

        // Two ways this turns out to have been expected after all, just not by the
        // supervisor: the program is on its way out, or the stack changed while this
        // report was queued on the mutex — a Stop got in first, or a cache clean has
        // already stopped and restarted everything and what is up now is a stack this
        // report has nothing to say about.
        if (state->shuttingDown.load(std::memory_order_acquire)) return;
        if (state->generation != generation) {
            LOGI(std::wstring(ComponentLogName(component)) +
                 L" stopped, but the stack has been restarted since; ignoring it.");
            return;
        }

        LOGE(std::wstring(ComponentLogName(component)) + L" stopped without being asked to" +
             ExitDetail(*state, component) + L"; the stack is half up, taking the rest down.");
        message = ServiceProblem(component, ReasonKeyFor(*state, component));
        StopLocked(*state);
    }

    // Outside lock: dialog blocks but doesn't freeze Start/Stop (supervisor thread already stopped).
    Dialogs::Show(message, MB_ICONERROR);
}

// Arm supervisor after successful start. Generation captured here prevents stale reports.
void ArmSupervisor(Runtime::State& state) {
    const uint64_t generation = ++state.generation;
    std::lock_guard<std::mutex> lock(state.childMutex);
    state.supervisor.Arm(
        {{Component::Nginx, state.nginx.waitHandle()},
         {Component::SniGate, state.sniGate.waitHandle()},
         {Component::DnsRedirection, state.redirector.failureHandle()},
         {Component::DnsProxy, state.proxy.stoppedHandle()}},
        [generation](Component component) { OnComponentDown(component, generation); });
}

// Format port claims for log: "127.0.0.1:53/both/exclusive, ..."
std::wstring JoinClaims(const std::vector<Ports::PortClaim>& claims) {
    std::wstring text;
    for (const Ports::PortClaim& claim : claims) {
        if (!text.empty()) text += L", ";
        text += claim.Text();
    }
    return text;
}

// Abort start if DNS Client service disabled (needs restart to fix, instruct user).
bool EnsureDnsClientRunning(StartFailure& failure, bool interactive) {
    const Dns::Nrpt::DnsClient state = Dns::Nrpt::QueryDnsClient();
    if (state == Dns::Nrpt::DnsClient::Running) return true;

    // Query failed: proceed anyway (blocking start on unknown state is worse than trying).
    if (state == Dns::Nrpt::DnsClient::Unavailable) {
        LOGW(
            L"Could not determine whether the DNS Client service is running; "
            L"continuing anyway.");
        return true;
    }

    LOGE(state == Dns::Nrpt::DnsClient::Disabled
             ? L"The DNS Client service is disabled; DNS redirection cannot work. "
               L"Aborting start."
             : L"The DNS Client service is not running; DNS redirection cannot work. "
               L"Aborting start.");
    failure.Set(interactive, T(L"msg.dnsClientOff"));
    return false;
}

// `portsApproved` is the caller's answer to the port-conflict question, asked before
// this lock was taken. It has no effect unless a port actually turns out to be held,
// so the common case (nothing running on 80/443/22222) never consults it.
bool StartLocked(Runtime::State& state, bool interactive, StartFailure& failure,
                 bool portsApproved) {
    // Teardown has the last word: once it has started, nothing may bring the stack
    // back up behind it.
    if (state.shuttingDown.load(std::memory_order_acquire)) {
        LOGW(L"Start requested while shutting down; ignoring it.");
        return false;
    }

    // Ignore start if already running (prevents double-start race between logon thread and tray).
    // Also clear remnants (partial stack) before proceeding.
    if (state.redirector.Running() && state.proxy.Running() && state.nginx.Running() &&
        state.sniGate.Running()) {
        LOGI(L"Start requested while everything is already running; nothing to do.");
        return true;
    }
    if (state.redirector.Active() || state.proxy.Running() || state.nginx || state.sniGate) {
        LOGW(L"Start requested with part of the stack still up; stopping the remnant first.");
        StopLocked(state);
    }

    EnsureRequiredDirectories();

    // Checked before anything is launched, because it is the one precondition whose
    // absence fails silently: without the DNS Client service the policy rule is
    // inert, every listed name resolves the ordinary way, and the user sees a stack
    // that reports itself running while not one site works.
    if (!EnsureDnsClientRunning(failure, interactive)) return false;

    // Read all endpoint declarations once (prevents inconsistency between checks).
    const Ports::ClaimAudit audit = AuditEndpoints();

    // Configuration faults: reject immediately (user can't fix payload).
    if (audit.HasConfigurationFault()) {
        LOGE(L"Endpoint(s) this program cannot bring up as declared (" +
             JoinClaims(audit.ConfigurationFaults()) +
             L"); the payload's endpoint declarations disagree with each other or are not "
             L"bindable as written. Aborting start.");
        failure.Set(interactive, T(L"msg.configMaybeWrong"));
        return false;
    }

    // Port conflict check: test all endpoints before launching anything.
    // CanBindAll judges the full set (one claim can block another).
    if (!audit.unavailable.empty()) {
        // Non-interactive start (logon/cache-clean): cannot ask user, abort silently.
        if (!portsApproved && !interactive) {
            LOGW(L"Required endpoints are unavailable and this start cannot ask; aborting.");
            return false;
        }
        if (!portsApproved) {
            LOGW(L"User declined endpoint cleanup; aborting start.");
            return false;
        }
        // KillPortHolders judges full set (one claim can block another); use that verdict.
        if (!KillPortHolders()) {
            // Name endpoints individually (tells user which specific port to free).
            std::vector<std::wstring> still;
            for (const Ports::PortClaim& claim : AuditEndpoints().unavailable)
                still.push_back(claim.Describe());
            LOGE(L"Endpoint(s) could not be freed; aborting start.");
            failure.Set(interactive, TFmt(L"msg.portsCritical", BulletList(still)));
            return false;
        }
    }

    // DNS proxy must start before nginx (nginx resolves upstreams through it).
    const std::wstring proxyConfig = ResolvedPath(L"DnsProxyConfig", L"data\\dns_proxy.ini");
    if (!state.proxy.LoadConfig(proxyConfig) || !state.proxy.Start()) {
        LOGE(L"Failed to start DNS proxy from " + proxyConfig + L".");
        // Report which listener failed to bind, or config path if none bound.
        std::wstring endpoints;
        for (const std::wstring& endpoint : state.proxy.BoundEndpoints()) {
            if (!endpoints.empty()) endpoints += L", ";
            endpoints += endpoint;
        }
        if (endpoints.empty()) endpoints = proxyConfig;
        StopLocked(state);
        failure.Set(interactive, TFmt(L"msg.dnsProxyStartFail", endpoints));
        return false;
    }
    LOGI(L"DNS proxy started.");

    // Roll back partial stack on child start failure (prevents holding ports with no DNS routing).
    if (!StartChild(state, &Runtime::State::nginx, Component::Nginx, NginxExe())) {
        StopLocked(state);
        ReportStartFailure(failure, interactive, Component::Nginx, L"reason.launchFailed");
        return false;
    }

    if (!StartChild(state, &Runtime::State::sniGate, Component::SniGate, SniGateExe())) {
        StopLocked(state);
        ReportStartFailure(failure, interactive, Component::SniGate, L"reason.launchFailed");
        return false;
    }

    const Dns::BindEndpoint resolverEndpoint =
        Payload::LoadResolverEndpoint(Payload::ConfigPath());
    state.redirector.SetEndpoint(resolverEndpoint);
    state.redirector.LoadRules(DnsRulesPath());
    if (!state.redirector.Start()) {
        LOGE(L"Failed to start DNS redirection.");
        StopLocked(state);
        if (interactive) {
            Dialogs::Show(TFmt(L"msg.dnsStartFail", resolverEndpoint.Text()).c_str(),
                          MB_ICONERROR);
        }
        return false;
    }

    // Reload rules automatically when the file changes.
    state.redirector.EnableHotReload(DnsRulesPath());

    // Flush DNS cache so redirects take effect immediately (not after TTL expires).
    FlushResolverCache();

    // Supervisor takes over. Handles children that exit immediately or later.
    ArmSupervisor(state);

    LOGI(L"DNS redirection started with " + std::to_wstring(state.redirector.RuleCount()) +
         L" rules.");
    return true;
}

// ---- Uninstall helpers ------------------------------------------------------

std::vector<std::wstring> ReadPayloadList(const wchar_t* section, const wchar_t* key) {
    return Ini::List(PayloadSections(), section, key);
}

// Read a certificate's subject or issuer common name.
std::wstring CertName(PCCERT_CONTEXT ctx, DWORD which) {
    const DWORD n = CertGetNameStringW(
        ctx, CERT_NAME_ATTR_TYPE, which,
        const_cast<void*>(static_cast<const void*>(szOID_COMMON_NAME)), nullptr, 0);
    if (n <= 1) return L"";
    std::wstring name(n, L'\0');
    CertGetNameStringW(ctx, CERT_NAME_ATTR_TYPE, which,
                       const_cast<void*>(static_cast<const void*>(szOID_COMMON_NAME)),
                       name.data(), n);
    name.resize(std::wcslen(name.c_str()));
    return name;
}

// Remove certificates matching names from Trusted Root store.
// Restart enumeration after each delete (deletion invalidates iterator).
size_t RemoveRootCertificates(DWORD storeLocation, const std::vector<std::wstring>& names) {
    size_t removed = 0;
    HCERTSTORE store = CertOpenStore(CERT_STORE_PROV_SYSTEM_W, 0, 0,
                                     storeLocation | CERT_STORE_OPEN_EXISTING_FLAG, L"ROOT");
    if (!store) return 0;

    // Bound prevents infinite loop if delete reports success without removing cert.
    constexpr int kMaxSweeps = 4096;
    for (int sweep = 0; sweep < kMaxSweeps; ++sweep) {
        bool removedThisSweep = false;
        PCCERT_CONTEXT ctx = nullptr;
        while ((ctx = CertEnumCertificatesInStore(store, ctx)) != nullptr) {
            const std::wstring subject = LowerW(CertName(ctx, 0));
            const std::wstring issuer = LowerW(CertName(ctx, CERT_NAME_ISSUER_FLAG));
            bool match = false;
            for (const std::wstring& want : names) {
                const std::wstring lowered = LowerW(want);
                if (subject == lowered || issuer == lowered) {
                    match = true;
                    break;
                }
            }
            if (!match) continue;

            // CertDeleteCertificateFromStore frees its argument, so it gets a duplicate;
            // the cursor is freed here only when abandoned, else by the next enumeration.
            PCCERT_CONTEXT dup = CertDuplicateCertificateContext(ctx);
            if (dup && CertDeleteCertificateFromStore(dup)) {
                ++removed;
                removedThisSweep = true;
                CertFreeCertificateContext(ctx);
                break;  // restart the sweep
            }
            if (dup) {
                LOGW(L"Uninstall: failed to remove a root certificate (err " +
                     std::to_wstring(GetLastError()) + L"); it will be left in the store.");
                CertFreeCertificateContext(dup);
            } else {
                LOGW(L"Uninstall: could not duplicate a root certificate context (err " +
                     std::to_wstring(GetLastError()) + L"); it will be left in the store.");
            }
        }
        if (!removedThisSweep) break;
    }
    CertCloseStore(store, 0);
    return removed;
}

// Updater waits for exit, removes exe, then non-recursively rmdir program directory
// (succeeds only if empty, preserving any user files).
void ScheduleSelfRemoval() {
    std::wstring dir = ExeDir();
    if (!dir.empty() && dir.back() == L'\\') dir.pop_back();

    // Data file under %ProgramData% + explicit CreateProcessW (no shell parsing).
    const std::wstring planDir = Updater::PlanDirectory();
    if (planDir.empty()) {
        LOGE(
            L"Uninstall: the updater's plan directory could not be secured; the "
            L"executable will have to be deleted by hand.");
        return;
    }

    Updater::Plan plan;
    plan.op = Updater::Op::Remove;
    plan.target = ExePath();
    plan.dir = dir;
    plan.parentPid = GetCurrentProcessId();

    const std::wstring planPath = planDir + L"\\" + Updater::RandomPlanFileName();
    if (!Updater::WritePlan(planPath, plan)) {
        LOGE(
            L"Uninstall: cannot write the updater plan; the executable will have to be "
            L"deleted by hand.");
        return;
    }

    if (!UpdaterModule::Launch({L"--apply-plan", planPath})) {
        LOGE(
            L"Uninstall: the updater module could not be started; the executable will "
            L"have to be deleted by hand.");
        Updater::DeletePlanFile(planPath);
    }
}

}  // namespace

// ---- Runtime ----------------------------------------------------------------

Runtime::Runtime() : m_state(std::make_shared<State>()) {
    PublishState(m_state);
}

Runtime::~Runtime() {
    // Unpublish first: detached workers find no runtime and do nothing (no race).
    PublishState(nullptr);

    // Set shutdown flag before acquiring lock (prevents worker from restarting stack).
    m_state->shuttingDown.store(true, std::memory_order_release);

    {
        std::lock_guard<std::mutex> lock(m_state->operationMutex);
        if (AnythingRunning(*m_state)) StopLocked(*m_state);
    }

    // Wait out every call that was already under way when we unpublished, so that the
    // final release — and with it DnsProxy::Stop() and Redirector::Stop(), and every
    // thread, child and handle they own — happens HERE, on wWinMain's thread, rather
    // than on whichever detached worker happens to drop the last copy.
    //
    // The unpublish above is what makes waiting safe and bounded rather than a race:
    // a caller reaches the slot only through Ctx(), which takes the same lock, so once
    // PublishState(nullptr) has returned no new reference can appear. Every remaining
    // one is held by a thread that acquired it before the unpublish and will release
    // it when its call returns. Workers also take the state lock before waiting on
    // operationMutex, so holding no lock here cannot deadlock against them.
    //
    // A holder runs a finite operation, and this destructor is what makes it finite
    // even when the operation is a Stop: shuttingDown is already set, so a worker
    // arriving on the mutex returns rather than starting the stack again.
    while (m_state.use_count() > 1) std::this_thread::sleep_for(std::chrono::milliseconds(1));

    // This is now provably the last reference. Everything the stack owns is stopped,
    // and releasing it destroys the state here, before wWinMain returns — the
    // guarantee services.h states, and the one main.cpp relies on.
}

// ---- Resolved locations ------------------------------------------------------

std::wstring NginxExe() {
    return ResolvedPath(L"Nginx", L"data\\nginx.exe");
}

std::wstring SniGateExe() {
    return ResolvedPath(L"SniGate", L"data\\sni-gate.exe");
}

std::wstring SupportedSitesFile() {
    return ResolvedPath(L"SupportedSites", L"data\\supported_sites.dat");
}

// ---- Status ------------------------------------------------------------------

bool DnsRedirectRunning() {
    const std::shared_ptr<Runtime::State> state = Ctx();
    return state && state->redirector.Running();
}

bool DnsProxyRunning() {
    const std::shared_ptr<Runtime::State> state = Ctx();
    return state && state->proxy.Running();
}

bool NginxRunning() {
    return ChildRunning(Ctx(), &Runtime::State::nginx, NginxExe());
}

bool SniGateRunning() {
    return ChildRunning(Ctx(), &Runtime::State::sniGate, SniGateExe());
}

bool AnyRunning() {
    return DnsRedirectRunning() || DnsProxyRunning() || NginxRunning() || SniGateRunning();
}

// ---- Ports -------------------------------------------------------------------

std::vector<Ports::PortClaim> RequiredPorts() {
    std::vector<Ports::PortClaim> claims;

    // The payload's own declarations first, in the order the file lists them, so a
    // conflict is reported in the order the person who wrote the file will read it.
    for (const Ports::PortClaim& claim : Payload::LoadPortClaims(Payload::ConfigPath()))
        claims.push_back(claim);

    // Then the DNS endpoints, which are declared in their own files rather than in
    // meta.ini — the proxy's listeners in dns_proxy.ini, the resolver's address in
    // [Resolver] — so this is the one place they all meet.
    //
    // Both are UDP and TCP and both are exclusive, which is not a guess: every one of
    // these sockets is opened through SocketUtils::BindListener, which sets
    // SO_EXCLUSIVEADDRUSE. The claim has to describe the bind that will actually be
    // attempted, or the check answers a different question than the start will ask.
    {
        const std::wstring proxyConfig =
            ResolvedPath(L"DnsProxyConfig", L"data\\dns_proxy.ini");
        const Dns::DnsProxyConfig proxy = Dns::DnsProxyConfig::Load(proxyConfig);
        for (const Dns::DnsProxyListener& listener : proxy.listeners) {
            claims.push_back(Ports::PortClaim{listener.address, listener.port,
                                              Ports::Transport::Both,
                                              Ports::BindMode::Exclusive});
        }
    }
    {
        const Dns::BindEndpoint resolver = Payload::LoadResolverEndpoint(Payload::ConfigPath());
        claims.push_back(Ports::PortClaim{resolver.address, resolver.port,
                                          Ports::Transport::Both, Ports::BindMode::Exclusive});
    }

    return claims;
}

std::vector<DWORD> HoldersOf(const Ports::PortClaim& claim) {
    // The processes actually in the way of THIS endpoint, not everyone on the port.
    //
    // The difference is not cosmetic: this list is walked to decide what to
    // terminate. A port is a namespace per address — a holder on 127.0.0.2 does not
    // stop a bind of 127.0.0.3 — so a listing by port number alone names processes
    // holding nothing this claim wants, and terminating them would be killing an
    // innocent program over a conflict it is not part of.
    return Ports::BlockersOf(claim);
}

Ports::ClaimAudit AuditEndpoints() {
    return Ports::AuditClaims(RequiredPorts());
}

bool KillPortHolders() {
    // http.sys (System, pid 4) commonly parks 80/443 via IIS/W3SVC/BranchCache.
    // Stopping the HTTP service releases those reservations.
    Command::RunHidden(L"net stop http /y", 60000);
    Command::RunHidden(L"net stop w3svc /y", 30000);
    Command::RunHidden(L"net stop was /y", 30000);

    // Only the claims that are actually blocked, and each is tested again by a bind
    // rather than assumed to be blocked because it was earlier: stopping HTTP.sys
    // above frees 80 and 443 without any process being terminated, and the calls
    // below would otherwise report a conflict the cleanup has already resolved.
    const std::vector<Ports::PortClaim> claims = RequiredPorts();
    for (const Ports::PortClaim& claim : claims) {
        if (Ports::CanBind(claim)) continue;

        for (DWORD pid : HoldersOf(claim)) {
            if (pid == 0) continue;

            std::wstring image;
            const bool identified = Process::TryImagePath(pid, image);

            if (Ports::IsSystemCritical(pid)) {
                LOGE(L"Endpoint " + claim.Text() + L" held by a system-critical process (pid " +
                     std::to_wstring(pid) + L": " +
                     (identified ? image : std::wstring(L"<unknown>")) + L")");
                continue;
            }

            LOGW(L"Freeing " + claim.Text() + L": terminating pid " + std::to_wstring(pid) +
                 L" (" + (identified ? image : std::wstring(L"<unknown>")) + L")");
            Process::KillTree(pid);
        }
    }

    // The bind is the verdict, not the kill — and it is the verdict for the whole set,
    // because that is the state the start needs. A process that ignored the
    // termination, or a holder the table did not name, leaves the set unbindable and
    // the caller has to be told; testing each claim on its own would report a set free
    // that the start is about to fail on.
    //
    // The per-claim test above is only a filter for which holders to go after. It is
    // deliberately not the verdict: a claim that binds alone but not beside its
    // siblings is the payload's own contradiction, and killing whoever holds its port
    // would not help while claiming it had.
    return Ports::CanBindAll(claims);
}

// ---- Lifecycle ---------------------------------------------------------------

void EnforceCleanSlate() {
    const std::wstring ours[] = {NginxExe(), SniGateExe()};

    // Sweep by image path first: every process whose executable is OUR file at OUR
    // resolved location, whether or not it holds a port.
    //
    // The port-based pass below cannot see a child that started but never bound —
    // nginx killed between launch and its listen, or a copy that failed to bind and is
    // sitting there retrying. Such an orphan is still a live process holding a job
    // object, a config file and a log handle open, and nothing else in the program
    // will ever notice it. Matching the full resolved path is what keeps this from
    // reaching a user's own nginx.exe, which lives somewhere else entirely; a process
    // whose image path cannot be read is not returned by FindByImagePath at all, so
    // "could not identify" stays handled as "not ours".
    //
    // This runs before anything is launched (see the call in wWinMain), so anything
    // matching here is an orphan of a previous run by construction — there is no
    // child of this run for it to mistake for one.
    for (const std::wstring& mine : ours) {
        for (DWORD pid : Process::FindByImagePath(mine)) {
            if (pid == GetCurrentProcessId()) continue;
            LOGW(L"Terminating a leftover " + mine + L" (pid " + std::to_wstring(pid) +
                 L") that a previous run did not clean up.");
            Process::KillTree(pid);
        }
    }

    // Then the port intersection, which covers the case the path sweep cannot: one of
    // our binaries that is running from a location the current configuration no longer
    // names, still holding port 80. Both halves are required, and each rules out a
    // mistake the other would let through — matching the port alone would terminate
    // whatever happens to hold 80, which is not this program's call to make silently.
    // A foreign program holding one of our ports is not dealt with here at all: that
    // is a conflict the user is asked about, in KillPortHolders, on the way into a
    // start.
    const std::wstring oursLower[] = {LowerW(ours[0]), LowerW(ours[1])};
    for (const Ports::PortClaim& claim : RequiredPorts()) {
        for (DWORD pid : HoldersOf(claim)) {
            std::wstring image;
            if (pid == 0 || !Process::TryImagePath(pid, image)) continue;
            image = LowerW(image);
            for (const std::wstring& mine : oursLower) {
                if (image != mine) continue;
                LOGW(L"Terminating a leftover " + image + L" (pid " + std::to_wstring(pid) +
                     L") still holding " + claim.Text() + L".");
                Process::KillTree(pid);
                break;
            }
        }
    }

    // The policy rule is the one thing this program can leave behind that outlives
    // the process holding it: a machine killed mid-run keeps sending the redirected
    // names to a local server that is no longer listening. Removing it here is what
    // makes that recoverable by simply starting the program again. The rule has a
    // fixed key of our own, so this can never reach one the user installed.
    if (Dns::Nrpt::RemoveRule())
        LOGW(L"Removed a DNS policy rule left behind by a previous run.");

    // The updater deletes the copy of itself it was started from, so a leftover means
    // one was killed before it could. Swept here because this is the one place that runs
    // before anything could be launched from that directory, and safe to call
    // unconditionally: a copy still executing is skipped rather than reported.
    UpdaterModule::SweepStaleCopies();
}

bool Start(bool interactive) {
    const std::shared_ptr<Runtime::State> state = Ctx();
    if (!state) return false;

    // The endpoint question is asked HERE, before the operation lock is taken.
    //
    // It used to be asked inside, and that was the one place in the program where a
    // message box was held under operationMutex — which Stop, the cache clean and the
    // exit path all queue behind. An unread question therefore froze every one of
    // them, including the answer to "why will this not exit". The question is a wait
    // on a person, so it belongs outside everything that waits on the lock.
    //
    // A payload that declares endpoints it cannot bring up is refused here instead,
    // and refused FIRST. It has to be decided in this same step: such a declaration
    // also shows up as an endpoint that could not be bound, so asking only the
    // "unavailable" question here would put "these endpoints are being used by
    // another program" in front of the user — with an offer to terminate whoever
    // holds them, which is nobody — when the real cause is a declaration in a file
    // that has to be corrected. The lock-free path is the only place a refusal can be
    // shown without holding up everything else, so both answers are read here and the
    // configuration fault wins.
    //
    // Asked only when nothing of ours is already up: while our own stack is running
    // the ports belong to it, and offering to free them would be offering to kill the
    // services the user is using. The full check is repeated under the lock below,
    // where the decision is actually made.
    bool portsApproved = true;
    if (interactive && !AnythingRunning(*state)) {
        const Ports::ClaimAudit audit = AuditEndpoints();
        if (audit.HasConfigurationFault()) {
            LOGE(L"Endpoint(s) this program cannot bring up as declared (" +
                 JoinClaims(audit.ConfigurationFaults()) +
                 L"); the payload's endpoint declarations disagree with each other or are "
                 L"not bindable as written. Aborting start.");
            Dialogs::Show(T(L"msg.configMaybeWrong"), MB_ICONERROR);
            return false;
        }
        if (!audit.unavailable.empty()) {
            std::vector<std::wstring> held;
            held.reserve(audit.unavailable.size());
            for (const Ports::PortClaim& claim : audit.unavailable)
                held.push_back(claim.Describe());
            portsApproved = Dialogs::Show(TFmt(L"msg.portsInUse", BulletList(held)).c_str(),
                                          MB_ICONWARNING | MB_YESNO) == IDYES;
            if (!portsApproved) {
                LOGW(L"User declined endpoint cleanup; aborting start.");
                return false;
            }
        }
    }

    StartFailure failure;
    bool started = false;
    {
        std::lock_guard<std::mutex> lock(state->operationMutex);
        started = StartLocked(*state, interactive, failure, portsApproved);
    }

    // Shown with the lock released, for the same reason the question is asked there.
    if (failure.pending()) Dialogs::Show(failure.message, MB_ICONERROR);
    return started;
}

void Stop() {
    const std::shared_ptr<Runtime::State> state = Ctx();
    if (!state) return;
    std::lock_guard<std::mutex> lock(state->operationMutex);
    StopLocked(*state);
}

void RunAutostartMode() {
    Start(false);
}

// The program directory is NOT assumed to be ours alone. Users are told to extract
// the archive into a fixed folder, but nothing stops that folder from also holding
// unrelated files. So uninstall removes only what this program owns, and removes the
// directory itself ONLY if that left it empty.
//
// WHAT we own is declared by the payload ([Uninstall] in meta.ini), not baked into
// this executable — the same reason the service locations live there. A payload that
// grows a new folder ships an updated meta.ini through the ordinary signed update.
// This code is the executor of that declaration, never the author of it.
void Uninstall() {
    LOGI(L"Uninstalling.");
    Stop();
    Autostart::Disable();

    // Drop the desktop shortcut, if the one there is ours.
    Shortcut::RemoveIfOurs();

    // Pull sni-gate's locally generated CA out of the Trusted Root stores. Done
    // BEFORE the files go away, since the names come from meta.ini, which is itself
    // on the removal list. Both the machine store (where an elevated install lands)
    // and the current user's store are swept.
    const std::vector<std::wstring> certNames =
        ReadPayloadList(L"Uninstall", L"RootCertificates");
    if (!certNames.empty()) {
        size_t n = RemoveRootCertificates(CERT_SYSTEM_STORE_LOCAL_MACHINE, certNames);
        n += RemoveRootCertificates(CERT_SYSTEM_STORE_CURRENT_USER, certNames);
        LOGI(L"Uninstall: removed " + std::to_wstring(n) + L" root certificate(s).");
    }

    // Remove exactly what the payload declares as ours; nothing else in the program
    // directory is touched.
    const std::vector<std::wstring> patterns = ReadPayloadList(L"Uninstall", L"Remove");
    if (patterns.empty()) {
        LOGW(
            L"Uninstall: meta.ini declares no [Uninstall] Remove list; "
            L"only the executable will be removed.");
    } else {
        const size_t deleted = FileSystem::DeleteByPatterns(ExeDir(), patterns);
        LOGI(L"Uninstall: removed " + std::to_wstring(deleted) + L" item(s).");
    }

    ScheduleSelfRemoval();
}

CacheCleanResult CleanCache() {
    CacheCleanResult result;

    const std::shared_ptr<Runtime::State> state = Ctx();
    if (!state) return result;
    std::lock_guard<std::mutex> lock(state->operationMutex);

    LOGI(L"Cleaning cache.");
    const std::vector<std::wstring> patterns = ReadPayloadList(L"Cache", L"Clean");
    if (patterns.empty()) {
        LOGW(L"Cache: meta.ini declares no [Cache] Clean patterns.");
        result.ok = true;
        return result;
    }

    // The stack is stopped and restarted as a whole, mirroring what Start and Stop
    // already guarantee, rather than tracking each service separately.
    const bool wasRunning = AnythingRunning(*state);
    if (wasRunning) {
        LOGI(L"Cache: stopping services before cleanup.");
        // No pause is needed before deleting: a process's file handles are closed by
        // the kernel as part of its teardown, and StopLocked does not return until
        // that teardown is confirmed complete.
        StopLocked(*state);
    }

    result.deleted = FileSystem::DeleteByPatterns(ExeDir(), patterns);
    LOGI(L"Cache: cleaned " + std::to_wstring(result.deleted) + L" item(s).");

    // Directories that were empty may have matched a pattern and gone with it.
    EnsureRequiredDirectories();

    // Bringing the stack back up is not an interactive start: it is restoring a state
    // this function took down a moment ago, and nothing may be put in front of the
    // user for it. The ports it is about to hold were freed by the StopLocked above,
    // so what is left is whether it comes up — not whether it may ask.
    StartFailure failure;
    result.ok = !wasRunning || StartLocked(*state, /*interactive=*/false, failure,
                                           /*portsApproved=*/true);
    if (!result.ok) LOGE(L"Cache: the services did not come back up after cleanup.");
    return result;
}

size_t EnsureRequiredDirectories() {
    const std::vector<std::wstring> dirs = ReadPayloadList(L"Directories", L"Required");
    if (dirs.empty()) return 0;

    std::vector<std::wstring> fullPaths;
    fullPaths.reserve(dirs.size());
    for (const std::wstring& rel : dirs) {
        if (!FileSystem::IsSafePath(rel)) {
            LOGW(L"Directories: rejecting unsafe path: " + rel);
            continue;
        }
        fullPaths.push_back(PathUnder(rel));
    }

    const size_t created = FileSystem::EnsureDirectories(fullPaths);
    if (created > 0) {
        LOGI(L"Directories: ensured " + std::to_wstring(created) + L" required director(ies).");
    }
    return created;
}

}  // namespace Services
