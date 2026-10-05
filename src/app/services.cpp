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

// The four things that have to be up for this program to do anything.
//
// Named as one type because the stack does not distinguish between them: any one of
// them going down takes the others with it, and for the same reason in each case
// — what is left is not a reduced service, it is a machine whose DNS points at
// something that cannot answer. The children happen to be processes and the DNS
// components happen to be threads and a registry key, but that is an implementation
// detail of each, not a difference in how the stack treats them.
enum class Component : std::uint8_t { Nginx, SniGate, DnsRedirection, DnsProxy };

// For the log: stable, ASCII, and the name the thing calls itself.
const wchar_t* ComponentLogName(Component c) {
    switch (c) {
        case Component::Nginx: return L"nginx";
        case Component::SniGate: return L"sni-gate";
        case Component::DnsRedirection: return L"DNS redirection";
        case Component::DnsProxy: return L"DNS proxy";
    }
    return L"";
}

// For anything a person reads: the same key the tray labels the component with, so
// a dialog about a component and the menu entry for it cannot drift apart, and a
// Chinese dialog does not suddenly contain an untranslated English noun.
const wchar_t* ComponentNameKey(Component c) {
    switch (c) {
        case Component::Nginx: return L"status.nginx";
        case Component::SniGate: return L"status.route";
        case Component::DnsRedirection: return L"status.dns";
        case Component::DnsProxy: return L"status.proxy";
    }
    return L"";
}

// Watches the running stack for a component going down without being asked to.
//
// Coming up is not a promise about the next second. A child can be killed from Task
// Manager, shot by security software minutes in, or hit a fatal condition of its own
// long after it started healthy; the local DNS server's loop can end on a failed
// select; the policy rule can be deleted by something else on the machine. Until
// something notices, the stack stays "started" with the DNS policy rule installed,
// so every redirected name resolves to a loopback address with nothing behind it.
// Every supported site breaks at once and the program does not say a word.
//
// The wait is on kernel objects — the children's process handles and events the DNS
// components signal when they stop in a way they could not repair — so a
// component going down is observed the instant it happens, thirty milliseconds in or
// thirty minutes in, indistinguishably, and costs nothing at all until it does. That
// is the point: any interval here would be a guess about how long a service is
// allowed to take, and a guess is precisely what this exists to avoid.
//
// A component that is already down by the time the wait is set up signals
// immediately, so this covers a failure during the start exactly as it covers one
// hours later. There is no separate startup check, and no window in which one goes
// unnoticed.
//
// Arm and Disarm are not thread-safe against each other and do not need to be: every
// caller holds Runtime::State's operation mutex, or is that state's own destruction,
// which happens once every such call has returned.
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

    // Watch each entry until one of them signals or Disarm is called, then call
    // `onDown` with the component it belonged to.
    //
    // The handles must outlive the armed supervisor. Every caller satisfies that by
    // construction — arming happens with the stack up, and the only two paths that
    // tear it down, a stop and the state's own destruction, both disarm first, and
    // Disarm does not return until this has stopped waiting on them.
    void Arm(std::vector<Watched> watched, std::function<void(Component)> onDown);

    // Stop watching, and return only once nothing is waiting on the handles any
    // more. Idempotent, and safe to call from the teardown that a signal triggered —
    // see Run for why that is not a thread waiting on itself.
    void Disarm();

private:
    void Run();

    // Manual-reset, both: once either fact is true it stays true, so no interleaving
    // of the threads can miss it.
    HANDLE m_cancel = nullptr;    // "stop watching"
    HANDLE m_finished = nullptr;  // "no longer waiting on anything, and no member of
                                  //  this object will be touched again"
    bool m_armed = false;
    std::vector<Watched> m_watched;
    std::function<void(Component)> m_onDown;
};

void StackSupervisor::Arm(std::vector<Watched> watched, std::function<void(Component)> onDown) {
    Disarm();

    // A component with no handle to wait on is one whose event could not be created,
    // and the reason is already in its own log line. Dropping it here is what keeps
    // that from costing the others their supervision too: a null in the array fails
    // the whole WaitForMultipleObjects, so one component that cannot be watched would
    // otherwise mean none of them are.
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

    // The only thread this supervisor ever creates, and it is created here rather
    // than when a component goes down. That is deliberate: the reaction to a failure
    // must not itself need a thread, because the moment it would need one is the
    // moment the machine is least able to provide one — and a std::system_error
    // thrown inside a thread function is an unhandled exception, which is a process
    // killed with the policy rule still installed and every listed name pointing at
    // it. Here it is an ordinary failure, with a caller to report it to.
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

    // What to do, decided while the members are still ours to read. The handler is
    // MOVED out rather than copied, and that is what makes the Disarm inside it safe:
    // it leaves the member empty, so clearing the member cannot destroy the callable
    // this thread is in the middle of executing.
    std::optional<Component> down;
    std::function<void(Component)> reaction;
    if (result != WAIT_OBJECT_0) {
        if (index >= waits.size()) {
            // Anything outside the signalled range — WAIT_FAILED on a handle that
            // went bad, an abandoned wait — means this supervisor has stopped
            // supervising. Say so rather than returning quietly: a watchdog that died
            // is worth more in the log than a silence that looks like health.
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
    // can run here rather than on a thread of its own.
    //
    // The reaction takes the stack down, and that teardown disarms this supervisor.
    // If Disarm meant "join this thread", that would be this thread joining itself,
    // and the only escape would be to create another thread on the failure path —
    // exactly where a machine is least able to give one. So Disarm waits for this
    // event instead, and this event states the one thing Disarm actually needs: that
    // nothing is waiting on the watched handles any more and no member of this object
    // will be touched again.
    //
    // Everything after this line is a local or a string with static storage, so the
    // supervisor may be destroyed while the reaction is still running — including
    // while it is blocked on a message box in front of a person, which is why the
    // reaction must not be something anyone waits for.
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
    // Set once the Runtime has begun tearing down.
    //
    // A detached tray worker can already be blocked on operationMutex when that
    // happens — the user clicking Start and then Exit is enough. Without this it
    // would acquire the mutex the moment teardown releases it and bring the whole
    // stack back up while the process is on its way out, leaving a local DNS
    // server and a policy rule alive with nothing left to own them.
    std::atomic<bool> shuttingDown{false};

    // Serializes Start/Stop/CleanCache so overlapping tray actions cannot interleave
    // process launches and DNS redirection state against each other.
    std::mutex operationMutex;

    // Which incarnation of the stack is currently up.
    //
    // A component going down is reported on a thread of its own, which then queues
    // behind this mutex — and by the time it gets in, the stack it was watching may
    // no longer be the stack that is running. CleanCache stops, deletes and starts
    // inside one hold of the mutex, so a component that went down a moment before it
    // began produces a report that arrives to find a brand new, healthy stack; acting
    // on it would tear that stack down and show the user an error about a service
    // that is running perfectly well.
    //
    // "Is anything running" cannot answer this — it is a different question, and it
    // happens to give the right answer only when nothing was restarted in between.
    // The question actually being asked is "is the stack I was watching still the
    // stack that is up", and a counter bumped by every arm and every stop answers
    // exactly that, whatever the timing.
    //
    // Read and written only under operationMutex, which is also the only place the
    // stack changes incarnation.
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

    // Watches every component for going down without this program asking.
    //
    // Declared last deliberately: members are destroyed in reverse order, so this one
    // is torn down — its wait cancelled and confirmed ended — while the children and
    // DNS components whose handles it is waiting on are still alive. Reversed, the
    // wait would outlive the handles it waits on for as long as the disarm takes.
    //
    // Armed and disarmed only under operationMutex, or from this state's own
    // destruction, which happens after every call holding that mutex has returned.
    StackSupervisor supervisor;
};

namespace {

// ---- Payload-declared locations ---------------------------------------------

// The service-location interface lives at a fixed path next to the executable, so
// the layout it points at can change across releases without breaking startup.
std::wstring PathsConfigFile() {
    return ExeDir() + L"paths.ini";
}

// A parsed paths.ini, kept until the file changes underneath it.
//
// Every lookup below used to parse the file from scratch, and the ones that matter
// run often: NginxRunning and SniGateRunning resolve an executable path each time the
// tray menu is built, so opening the menu meant reading and decoding this file twice,
// before either of them had even looked at a process. The payload declares this file
// and it is small, but nothing about the declaration requires re-reading it per call.
//
// Invalidated on size and write time rather than read once per process, because the
// file is NOT immutable while the program runs: it ships in the payload, so a data-only
// update can replace it under a running instance. An edit is noticed; an unchanged file
// is parsed once. Same reasoning, and the same key, as the config.ini cache in
// app/settings.cpp.
//
// Guarded because these are called from the tray's worker threads as well as the UI
// thread. The vector is returned by value rather than by reference so a caller cannot
// hold a reference into it while another thread replaces it.
struct PathsStamp {
    long long size = -1;
    long long mtime = -1;
    bool known = false;

    bool operator==(const PathsStamp& o) const {
        return size == o.size && mtime == o.mtime && known == o.known;
    }
};

std::mutex g_pathsMx;
PathsStamp g_pathsStamp;
std::vector<Ini::Section> g_pathsSections;

PathsStamp StampOfPathsFile() {
    PathsStamp stamp;
    WIN32_FILE_ATTRIBUTE_DATA data = {};
    if (!GetFileAttributesExW(PathsConfigFile().c_str(), GetFileExInfoStandard, &data))
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

std::vector<Ini::Section> PathsSections() {
    const std::wstring path = PathsConfigFile();
    const PathsStamp stamp = StampOfPathsFile();

    std::lock_guard<std::mutex> lock(g_pathsMx);
    if (g_pathsStamp == stamp && g_pathsStamp.known) return g_pathsSections;

    g_pathsSections = Ini::Read(path);
    g_pathsStamp = stamp;
    return g_pathsSections;
}

// Read one string value out of paths.ini.
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
    std::wstring rel = Ini::Value(PathsSections(), L"Paths", key);
    if (rel.empty()) rel = fallback;
    return PathUnder(rel);
}

// Redirect-rule source: hosts-style "ACTION domain..." lines (see dns/rules.h).
std::wstring DnsRulesPath() {
    return ResolvedPath(L"Hosts", L"data\\dns_hosts.txt");
}

// Directory containing `exe`, with a trailing backslash. A path with no separator is
// taken to mean a name in our own directory, which is where such a service would have
// been launched from.
std::wstring DirOf(const std::wstring& exe) {
    const std::wstring dir = DirPart(exe);
    return dir.empty() ? ExeDir() : dir;
}

// ---- Timing constants --------------------------------------------------------

// How long to wait for a stopped service's port to disappear from the TCP table.
constexpr DWORD kPortReleaseTimeoutMs = 5000;

// How often the TCP table is re-read while waiting for a port to be released. There
// is no kernel event for "a socket closed", so this poll cannot be avoided.
constexpr DWORD kPortPollMs = 50;

// Wait for our service ports to leave the TCP table.
//
// Terminating a process closes its sockets as part of teardown, and we already
// waited for that teardown to finish — but the update path stops the stack and
// starts it again straight away, so "stopped" has to mean the next bind will
// succeed, not merely that the process is gone.
void AwaitPortsReleased(DWORD timeoutMs) {
    const ULONGLONG deadline = GetTickCount64() + timeoutMs;
    for (;;) {
        bool anyHeld = false;
        for (int port : Ports::kServicePorts)
            if (Ports::IsOccupied(port)) anyHeld = true;
        if (!anyHeld) return;
        if (GetTickCount64() >= deadline) {
            LOGW(L"A service port is still held after stopping; a restart may fail to bind.");
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
    Command::RunHidden(L"ipconfig /flushdns", nullptr, kFlushTimeoutMs);
}

// The published state, and the lock that makes publishing it and taking a reference
// to it one step each.
//
// A tray command runs on a detached thread, so one of them — an update applying
// while the user picks Exit — can still be inside a public function below when the
// Runtime on wWinMain's stack is destroyed. Callers therefore get a shared_ptr, not
// a raw pointer: the state stays alive for exactly the duration of a call that is
// already under way, and is released as soon as the last such call returns. For a
// caller that arrives afterwards there is no state, which reads as "there is nothing
// running to act on" — exactly right.
//
// Neither global has a destructor that can run at exit. The lock is an SRWLOCK
// initialized by a constant, and the shared_ptr lives in a heap object that is
// deliberately never deleted: a detached worker can reach this slot after main() has
// returned, and a namespace-scope object would by then have been destroyed under it.
// What leaks is one empty shared_ptr — sixteen bytes holding nothing. The state it
// used to point at is destroyed properly, with every thread, process and handle it
// owns released in order.
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

// Is this service running?
//
// Two different things both mean yes, and the tray has to report either one:
//   * the child this program launched is alive — answered through its handle, so it
//     cannot drift from reality and cannot be fooled by a reused PID;
//   * or a copy of the very same executable is alive that we did not launch, because
//     someone opened it by hand in the data folder.
//
// The second case used to report "Stopped" while that process sat there holding
// ports 80 and 22222. That is the one answer that is certainly wrong: the menu then
// offers to Start a stack whose ports are already taken, and the start walks into
// the port-conflict prompt for a program the user started themselves a moment ago.
bool ChildRunning(const std::shared_ptr<Runtime::State>& state,
                  Process::Child Runtime::State::* slot, const std::wstring& exe) {
    if (state) {
        std::lock_guard<std::mutex> lock(state->childMutex);
        if (((*state).*slot).Running()) return true;
    }
    return !Process::FindByImagePath(exe).empty();
}

// Whether any part of the stack this Runtime owns still has to be taken back. Only
// about what we launched: a copy started by hand is not something a teardown of this
// object is responsible for, and Stop reaches those separately.
//
// The question is what a Stop would have work to do about, NOT what is healthy — a
// component that has failed still holds a thread, a socket or a registry key, and
// answering with its health would walk past exactly the state that most needs
// clearing up.
bool AnythingRunning(Runtime::State& state) {
    if (state.redirector.Active() || state.proxy.Running()) return true;
    std::lock_guard<std::mutex> lock(state.childMutex);
    return static_cast<bool>(state.nginx) || static_cast<bool>(state.sniGate);
}

// ---- Start / stop, assuming operationMutex is held ---------------------------

// Launch one child into its slot. False means the launch itself failed — the process
// was never created, and the reason is already in the log.
//
// Nothing is waited for and nothing is checked afterwards. A child that starts slowly
// is not a child that failed, and there is no window short enough to tell those apart
// or long enough to be sure. Whether the children are still there is not asked at a
// moment of this code's choosing at all; it is watched continuously, by the
// supervisor, from the end of the start until the next stop.
bool StartChild(Runtime::State& state, Process::Child Runtime::State::* slot,
                Component component, const std::wstring& exe) {
    const std::wstring name = ComponentLogName(component);
    LOGI(L"Starting " + name + L": " + exe);

    // nginx derives its prefix from its own module path with the wide API, and it
    // already lives in the directory that prefix has to be, so it needs no
    // arguments at all. It used to be given -p "<dir>", which travels through the
    // child's ANSI argv: on a non-ASCII install path those bytes do not survive the
    // round trip back to UTF-16, and nginx would exit before it could even open its
    // error log. The working directory carries the same information and is passed
    // as UTF-16 by the kernel.
    Process::Child child = Process::LaunchChild(exe, L"", DirOf(exe));
    if (!child) return false;

    LOGI(name + L" started (pid " + std::to_wstring(child.pid()) + L").");
    std::lock_guard<std::mutex> lock(state.childMutex);
    state.*slot = std::move(child);
    return true;
}

void StopChildren(Runtime::State& state) {
    // Move the children out under the lock and terminate them outside it: the tray
    // must stay able to ask what is running while a stop is in progress.
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

    // A copy nobody here launched — opened by hand from the data folder — reports as
    // running and is therefore offered a Stop, so Stop has to be able to stop it.
    // Otherwise pressing it would leave the status exactly as it was, which is the
    // one thing a command must never do. Matching on the full path is what keeps this
    // to our own executables and away from a program that merely shares their name.
    Process::TerminateByImagePath(NginxExe());
    Process::TerminateByImagePath(SniGateExe());
}

void StopLocked(Runtime::State& state) {
    // First, so that terminating the children below is not mistaken for the thing the
    // supervisor exists to catch. Every death from here on is one this program asked
    // for, and this returns only once nothing is waiting on their handles.
    state.supervisor.Disarm();

    // The stack that was up is no longer the stack that is up, whatever comes next.
    // Bumped here rather than only on the way back up, so that a report already in
    // flight is invalidated by the stop itself and not left to be filtered by
    // whatever happens to be running when it arrives.
    ++state.generation;

    // Removing the policy rule is what returns DNS to normal; flushing afterwards
    // drops the loopback answers we synthesized, so names resolve for real again
    // straight away instead of after their TTL runs out.
    state.redirector.Stop();
    state.proxy.Stop();
    FlushResolverCache();

    StopChildren(state);
    AwaitPortsReleased(kPortReleaseTimeoutMs);
    LOGI(L"Services stopped.");
}

// Compose a service dialog: the service and what happened to it, then what that cost.
//
// One line, one consequence — and both built here rather than at the call sites,
// which is what keeps the dialogs from drifting into looking like messages from two
// different programs. The name is a translation key, not a literal, so the dialog
// calls a component exactly what the tray menu calls it. The separator between name
// and reason comes from the translation table too, where a fullwidth colon in Chinese
// is a translator's decision rather than a literal buried in code.
std::wstring ServiceProblem(Component component, const wchar_t* reasonKey) {
    return std::wstring(T(ComponentNameKey(component))) + T(L"punct.colon") + T(reasonKey) +
           L"\n" + T(L"msg.serviceFailed");
}

// A start's failure to report, carried out of the critical section that detected it.
//
// Every failure below used to be shown where it was found, and that place is inside
// operationMutex. A dialog is a wait on a person, and Start, Stop, the cache clean and
// the exit path all queue behind that lock — so an unread message box froze every one
// of them. The message therefore travels out with the result and is shown once the
// lock is released; see Start().
//
// The logon path must never put a dialog in front of someone who is still signing in,
// so Set() takes whether this start is allowed to talk to the user at all and records
// nothing when it is not. Absent a message there is nothing to show, and the caller
// needs no second flag.
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

// Why the component stopped, in the words the user is shown.
//
// Only DNS redirection has more than one answer, and the two are worth telling apart
// because the thing to do about them differs: a server that stopped answering is this
// machine's networking, while a policy rule that keeps being deleted is another
// program on the machine, and no amount of restarting this one will help.
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

// A component stopped without being asked to. Take the rest of the stack down with it.
//
// Half a stack is the one state this program refuses to sit in. nginx gone leaves the
// DNS policy rule pointing every listed name at a loopback port nothing answers,
// which breaks those sites far more thoroughly than not running at all would — and
// leaves the user with no way to tell why, since everything still reports as started.
// DNS redirection gone is the same picture from the other side. Rolling back restores
// the machine to what it was before the start; the dialog is what turns a silent
// breakage into something the user can act on.
//
// That dialog is shown however the stack was started, including from the logon path
// that suppresses its own failure dialogs. The reasoning there does not carry: that
// suppression exists so nothing is put in front of someone still signing in, but a
// component going down has no such moment attached to it — it can land three hours
// into a session, and tying the notification to how the stack came up would silence it
// for the whole of that session.
//
// `generation` is the incarnation of the stack this report was armed against. It runs
// on the supervisor's own thread, which has already stopped supervising by the time
// this is entered, so blocking here — including on the message box — delays nothing.
void OnComponentDown(Component component, uint64_t generation) {
    const std::shared_ptr<Runtime::State> state = Ctx();
    if (!state) return;  // the runtime is gone; there is nothing left to take down

    // What the user is told, composed while the lock is still held. The reason
    // belongs to the incarnation being torn down, so it is read before the teardown
    // rather than after it.
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

    // Outside the lock on purpose: this dialog waits for a person, and holding the
    // operation mutex across it would freeze Start, Stop and the tray behind it for
    // as long as the message box goes unread.
    //
    // This is the supervisory thread, which owns no window, so the dialog is
    // marshalled to the UI thread and owned by the tray window. The block here still
    // delays only this thread — it has already stopped supervising, as the note above
    // says — but it no longer depends on being lucky about which thread that is.
    Dialogs::Show(message, MB_ICONERROR);
}

// Begin watching every component. Called at the end of a start, which is also the
// point from which the stack is expected to stay up.
//
// The generation the report will carry is decided here, in the same step that arms
// the watch, so a report can only ever describe the incarnation it was armed for.
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

// Refuse to start when the service that enforces the DNS policy table is down.
//
// Nothing is repaired here. Windows does not permit the service to be stopped, so
// the only way it is found down is that its start type was set to Disabled and the
// machine restarted — and undoing that needs another restart no matter which route
// is taken (see dns/nrpt.h for the two that were measured and rejected). Since the
// user has to restart either way, the useful thing to hand them is that
// instruction, delivered before a stack comes up that could not have worked.
//
// The message is composed here and shown by the caller, for the reason given on
// StartFailure: this runs under operationMutex.
bool EnsureDnsClientRunning(StartFailure& failure, bool interactive) {
    const Dns::Nrpt::DnsClient state = Dns::Nrpt::QueryDnsClient();
    if (state == Dns::Nrpt::DnsClient::Running) return true;

    // Being unable to ask is not evidence of a problem. Blocking a start on a
    // question we could not put would be worse than proceeding and letting the
    // ordinary failure paths report whatever actually goes wrong.
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

    // A start on a stack that is already up is not a second start.
    //
    // Two callers can ask independently — the logon thread brings the stack up while
    // the tray still shows "Start Services", because at the moment that menu was
    // built nothing was running yet. The mutex orders them but does not make the
    // second one a no-op, and without this it is not one: the ports are occupied by
    // then, so the second start would offer to free ports held by the services the
    // first one just launched, terminate them, and start them over.
    //
    // A remnant — one child gone on its own, or DNS redirection down with a child
    // still up — is not left to the port-conflict path either. This stack is
    // all-or-nothing by design, so the remnant is cleared and the start proceeds from
    // a known state rather than from whatever happened to survive.
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

    if (AnyPortOccupied()) {
        // The question itself was asked by the caller, outside this lock. All that is
        // decided here is what to do with the answer, and the answer to "free them"
        // for a non-interactive start is not a question at all: the logon path and the
        // cache clean have nobody to ask and must not put a dialog in front of anyone.
        if (!portsApproved && !interactive) {
            LOGW(L"Ports are held and this start cannot ask; aborting.");
            return false;
        }
        if (!portsApproved) {
            LOGW(L"User declined port cleanup; aborting start.");
            return false;
        }
        if (!KillPortHolders()) {
            LOGE(L"Cannot free ports held by system-critical processes; aborting start.");
            failure.Set(interactive, T(L"msg.portsCritical"));
            return false;
        }
        if (AnyPortOccupied()) {
            LOGE(L"Ports still occupied after cleanup; aborting start.");
            failure.Set(interactive, T(L"msg.portsStillInUse"));
            return false;
        }
    }

    // From here on the start is all-or-nothing. Nginx resolves dynamic upstreams
    // through this proxy, so it must be listening before nginx reads its config.
    const std::wstring proxyConfig = ResolvedPath(L"DnsProxyConfig", L"data\\dns_proxy.ini");
    if (!state.proxy.LoadConfig(proxyConfig) || !state.proxy.Start()) {
        LOGE(L"Failed to start DNS proxy from " + proxyConfig + L".");
        StopLocked(state);
        failure.Set(interactive, T(L"msg.dnsProxyStartFail"));
        return false;
    }
    LOGI(L"DNS proxy started.");

    // A stack with a child up but DNS redirection down proxies nothing, yet holds
    // the ports and reads as partly running; rolling back restores the known state.
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

    state.redirector.LoadRules(DnsRulesPath());
    if (!state.redirector.Start()) {
        LOGE(L"Failed to start DNS redirection.");
        StopLocked(state);
        if (interactive) Dialogs::Show(T(L"msg.dnsStartFail"), MB_ICONERROR);
        return false;
    }

    // Reload rules automatically when the file changes.
    state.redirector.EnableHotReload(DnsRulesPath());

    // Evict any real addresses cached for redirected names so the redirect takes
    // effect immediately rather than after the cached TTL runs out.
    FlushResolverCache();

    // Everything is up. Coming up and staying up are different questions, and this is
    // where responsibility passes from one to the other — including for a child that
    // is already gone by now, whose handle the supervisor finds signalled the moment
    // it starts waiting.
    ArmSupervisor(state);

    LOGI(L"DNS redirection started with " + std::to_wstring(state.redirector.RuleCount()) +
         L" rules.");
    return true;
}

// ---- Uninstall helpers ------------------------------------------------------

// Split a '|'-separated list, trimming each item and dropping empties.
std::vector<std::wstring> SplitList(const std::wstring& s) {
    std::vector<std::wstring> out;
    size_t start = 0;
    for (;;) {
        const size_t bar = s.find(L'|', start);
        std::wstring token =
            TrimW(bar == std::wstring::npos ? s.substr(start) : s.substr(start, bar - start));
        if (!token.empty()) out.push_back(std::move(token));
        if (bar == std::wstring::npos) break;
        start = bar + 1;
    }
    return out;
}

// Read one '|'-separated value from a section in paths.ini.
//
// An oversized value comes back empty from the reader, so a truncated list can never be
// produced: either every entry is present in full or the list reads as absent. That
// matters because these lists drive deletion — the last entry, cut mid-word, becomes a
// prefix glob that matches more than it should, and a cleanup that silently does
// nothing is the recoverable direction while one that deletes by a pattern nobody wrote
// is not.
std::vector<std::wstring> ReadPathsList(const wchar_t* section, const wchar_t* key) {
    return SplitList(Ini::Value(PathsConfigFile(), section, key));
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

// Remove every certificate whose subject OR issuer common name matches one of
// `names` from a Trusted Root store. sni-gate generates a local CA and installs it
// there to terminate TLS; leaving it behind after an uninstall would keep a trusted
// signer on the machine with no software left to justify it.
//
// Deleting invalidates the enumeration, so each pass restarts from the top and the
// loop repeats until a full sweep finds nothing to remove — which is also why the
// restart must be conditional on the delete having happened. Repeating because a
// delete succeeded is progress; repeating because one did not is the same sweep
// forever, and a root store is writable only by an administrator (enterprise
// policy locks it down outright), so a delete that fails once fails every time.
size_t RemoveRootCertificates(DWORD storeLocation, const std::vector<std::wstring>& names) {
    size_t removed = 0;
    HCERTSTORE store = CertOpenStore(CERT_STORE_PROV_SYSTEM_W, 0, 0,
                                     storeLocation | CERT_STORE_OPEN_EXISTING_FLAG, L"ROOT");
    if (!store) return 0;

    // Each pass removes at most one certificate, so no store can need more passes
    // than it can hold. The bound is a second line of defence for a delete that
    // reports success without the certificate leaving: the enumeration is finite
    // either way, and a loop that cannot end is the one outcome worth ruling out
    // twice.
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

            // CertDeleteCertificateFromStore frees the context either way, so the
            // enumeration cannot continue from it — duplicate, delete, restart.
            PCCERT_CONTEXT dup = CertDuplicateCertificateContext(ctx);
            CertFreeCertificateContext(ctx);

            // Only a delete that actually happened earns another pass. When it does
            // not — or when the duplicate itself could not be made — the certificate
            // is left where it is, said out loud, and this sweep carries on to the
            // next one rather than restarting on the one that would not go.
            if (dup && CertDeleteCertificateFromStore(dup)) {
                ++removed;
                removedThisSweep = true;
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

// The executable cannot delete itself while running, so the updater waits for this
// process to exit, removes it, and then tries a NON-recursive rmdir of the program
// directory. That call succeeds only if nothing else is left — if the user keeps
// unrelated files there, it fails harmlessly and their files remain.
void ScheduleSelfRemoval() {
    std::wstring dir = ExeDir();
    if (!dir.empty() && dir.back() == L'\\') dir.pop_back();

    // The same work-order mechanism the update path uses: a data file under
    // %ProgramData% plus an explicit CreateProcessW, with no shell anywhere in between.
    // The former script wrote its paths into batch, where a directory name containing
    // & would have been re-read as command syntax by an elevated cmd.exe.
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
    // Unpublish first: from here on a new call from a detached worker finds no
    // runtime and does nothing, instead of racing this teardown.
    PublishState(nullptr);

    // Announce the teardown BEFORE waiting for the lock. A worker already blocked on
    // it will acquire it the instant we let go, and this is what stops that worker
    // from starting the stack again on the way out.
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

bool AnyPortOccupied() {
    for (int port : Ports::kServicePorts)
        if (Ports::IsOccupied(port)) return true;
    return false;
}

bool KillPortHolders() {
    // http.sys (System, pid 4) commonly parks 80/443 via IIS/W3SVC/BranchCache.
    // Stopping the HTTP service releases those reservations.
    Command::RunHidden(L"net stop http /y", nullptr, 60000);
    Command::RunHidden(L"net stop w3svc /y", nullptr, 30000);
    Command::RunHidden(L"net stop was /y", nullptr, 30000);

    bool allFreed = true;
    for (int port : Ports::kServicePorts) {
        for (DWORD pid : Ports::ListenersOn(port)) {
            if (pid == 0) continue;

            std::wstring image;
            const bool identified = Process::TryImagePath(pid, image);

            if (Ports::IsSystemCritical(pid)) {
                LOGE(L"Port " + std::to_wstring(port) +
                     L" held by a system-critical process (pid " + std::to_wstring(pid) +
                     L": " + (identified ? image : std::wstring(L"<unknown>")) + L")");
                allFreed = false;
                continue;
            }

            LOGW(L"Freeing port " + std::to_wstring(port) + L": terminating pid " +
                 std::to_wstring(pid) + L" (" +
                 (identified ? image : std::wstring(L"<unknown>")) + L")");
            if (!Process::KillTree(pid)) allFreed = false;
        }
    }
    return allFreed;
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
    for (int port : Ports::kServicePorts) {
        for (DWORD pid : Ports::ListenersOn(port)) {
            std::wstring image;
            if (pid == 0 || !Process::TryImagePath(pid, image)) continue;
            image = LowerW(image);
            for (const std::wstring& mine : oursLower) {
                if (image != mine) continue;
                LOGW(L"Terminating a leftover " + image + L" (pid " + std::to_wstring(pid) +
                     L") still holding port " + std::to_wstring(port) + L".");
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

    // The port-conflict question is asked HERE, before the operation lock is taken.
    //
    // It used to be asked inside, and that was the one place in the program where a
    // message box was held under operationMutex — which Stop, the cache clean and the
    // exit path all queue behind. An unread question therefore froze every one of
    // them, including the answer to "why will this not exit". The question is a wait
    // on a person, so it belongs outside everything that waits on the lock.
    //
    // Asked only when nothing of ours is already up: while our own stack is running
    // the ports belong to it, and offering to free them would be offering to kill the
    // services the user is using. The full check is repeated under the lock below,
    // where the decision is actually made.
    bool portsApproved = true;
    if (interactive && !AnythingRunning(*state) && AnyPortOccupied()) {
        portsApproved = Dialogs::Show(T(L"msg.portsInUse"), MB_ICONWARNING | MB_YESNO) == IDYES;
        if (!portsApproved) {
            LOGW(L"User declined port cleanup; aborting start.");
            return false;
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
// WHAT we own is declared by the payload ([Uninstall] in paths.ini), not baked into
// this executable — the same reason the service locations live there. A payload that
// grows a new folder ships an updated paths.ini through the ordinary signed update.
// This code is the executor of that declaration, never the author of it.
void Uninstall() {
    LOGI(L"Uninstalling.");
    Stop();
    Autostart::Disable();

    // Drop the desktop shortcut, if the one there is ours.
    Shortcut::RemoveIfOurs();

    // Pull sni-gate's locally generated CA out of the Trusted Root stores. Done
    // BEFORE the files go away, since the names come from paths.ini, which is itself
    // on the removal list. Both the machine store (where an elevated install lands)
    // and the current user's store are swept.
    const std::vector<std::wstring> certNames =
        ReadPathsList(L"Uninstall", L"RootCertificates");
    if (!certNames.empty()) {
        size_t n = RemoveRootCertificates(CERT_SYSTEM_STORE_LOCAL_MACHINE, certNames);
        n += RemoveRootCertificates(CERT_SYSTEM_STORE_CURRENT_USER, certNames);
        LOGI(L"Uninstall: removed " + std::to_wstring(n) + L" root certificate(s).");
    }

    // Remove exactly what the payload declares as ours; nothing else in the program
    // directory is touched.
    const std::vector<std::wstring> patterns = ReadPathsList(L"Uninstall", L"Remove");
    if (patterns.empty()) {
        LOGW(
            L"Uninstall: paths.ini declares no [Uninstall] Remove list; "
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
    const std::vector<std::wstring> patterns = ReadPathsList(L"Cache", L"Clean");
    if (patterns.empty()) {
        LOGW(L"Cache: paths.ini declares no [Cache] Clean patterns.");
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
    const std::vector<std::wstring> dirs = ReadPathsList(L"Directories", L"Required");
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
