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
// The DNS server the policy table points at.
//
// It answers for the names the rule set claims and forwards everything else to
// the machine's real DNS servers, because a policy-table namespace is routed here
// WHOLE: every query type for every name under it, not just the address lookups
// worth redirecting. A resolver that only knew how to answer would therefore turn
// a redirected domain's MX, TXT and SRV records into silence. Forwarding is what
// keeps the redirect confined to what it is meant to change.
//
// UDP and TCP are both served on port 53. TCP is not optional: a forwarded reply
// can come back truncated, and the client's only recourse then is to ask the same
// server again over TCP.
//
// One thread runs one event loop. DNS on loopback is a trickle, and forwarding is
// the only thing here that ever waits on the network — so it is done without
// waiting, by remembering the query and matching the reply when it arrives.
#include <atomic>
#include <cstdint>
#include <memory>
#include <mutex>
#include <string>
#include <thread>

#include "dns/rules.h"
#include "dns/socket_utils.h"

namespace Dns {

// The loopback endpoint this server binds by default.
//
// Deliberately not 127.0.0.1. All of 127.0.0.0/8 reaches loopback, and port 53 on
// 127.0.0.1 is the conventional home of every other local DNS server a machine
// might already run — Docker's, an ICS host's, a filtering proxy's. Taking an
// address nobody picks by convention means coexisting with them instead of
// fighting for a port.
//
// A default rather than the endpoint: where this server actually binds comes from
// the payload (see app/payload.h), because the NRPT rule that routes names here
// has to name the same address and both must move together.
inline constexpr wchar_t kDefaultResolverAddress[] = L"127.11.45.14";
inline constexpr uint16_t kDefaultResolverPort = 53;

// Sockets and in-flight state, defined in the implementation so that no consumer
// of this header has to see winsock.
struct ResolverState;

class LocalResolver {
public:
    LocalResolver();
    ~LocalResolver();
    LocalResolver(const LocalResolver&) = delete;
    LocalResolver& operator=(const LocalResolver&) = delete;

    // Publish the rules to answer from. The set is never mutated after publishing,
    // so the loop reads its snapshot without locking anything and a hot-reload only
    // changes which set the next query sees. Safe before or after Start().
    void Publish(std::shared_ptr<const RuleSet> rules);

    // Where to bind, for the next Start(). Safe before or after one; a change does
    // not move a listener that is already bound, so it takes effect on the next
    // Start.
    //
    // Not a Start() parameter because the caller that knows the endpoint — the
    // Redirector, which also has to write it into the policy table — must be able to
    // state it before the server exists, and because the two have to be the same
    // value at the moment the rule is installed rather than merely related.
    void SetEndpoint(const BindEndpoint& endpoint);

    // The endpoint in force, whether it came from SetEndpoint or the default.
    BindEndpoint Endpoint() const;

    // Bind both listeners and run the event loop on a worker thread. Any previous
    // session is torn down first, so this always starts from a clean state.
    // Returns false — having bound nothing — if the endpoint is unavailable.
    //
    // Never throws. A thread that cannot be created is reported the same way a port
    // that cannot be bound is, because the caller can do nothing different about it
    // and an exception here would escape into a detached tray worker.
    bool Start();

    // Stop the loop and close every socket. Each step is driven by the resource it
    // releases rather than by a "running" flag, because the loop can end on its
    // own, and an early return on that flag would leave a joinable std::thread to
    // be destroyed, which terminates the process.
    void Stop();

    bool Running() const { return m_running.load(); }

    // Signalled while the loop is NOT running, cleared by a successful Start().
    //
    // The loop can end without anyone asking: a failed select() is the only way, but
    // it is a way, and what it leaves behind is the worst state this program has —
    // the sockets are still bound, so queries are accepted into a receive buffer and
    // never answered, while the policy table keeps sending every listed name here.
    // Nothing about that is visible until someone thinks to ask.
    //
    // The thread's exit IS the event, so it is published as one. Waiting on this
    // costs nothing until it happens, needs no interval to be guessed at, and drops
    // straight into the same WaitForMultipleObjects that watches the child processes
    // — which is what lets one wait cover all three components of the stack.
    //
    // It is signalled by an asked-for stop as well; distinguishing the two is the
    // waiter's job, and Redirector does it by disarming before it stops anything.
    //
    // Typed as void* rather than HANDLE for the same reason the socket state is
    // hidden below: nothing that includes this header should have to pull in
    // windows.h to use a DNS server. HANDLE is void*, so a waiter passes it straight
    // to WaitForMultipleObjects with no cast.
    //
    // Valid for this object's whole lifetime, except on a machine that could not
    // create an event at all — in which case it is null and Start() fails outright,
    // so nobody is ever handed a null handle to wait on.
    void* stoppedHandle() const { return m_stopped; }

private:
    void Loop();
    std::shared_ptr<const RuleSet> ActiveRules() const;

    mutable std::mutex m_mx;

    // Where the next Start() binds, and where the policy table sends names. Guarded
    // by m_mx because the tray can set it while a status query reads it; a change
    // takes effect on the next Start rather than moving a bound listener.
    BindEndpoint m_endpoint;

    std::shared_ptr<const RuleSet> m_activeRules;
    std::unique_ptr<ResolverState> m_state;
    std::thread m_thread;
    std::atomic<bool> m_running{false};

    // Manual-reset, and created already signalled: at construction the loop is
    // genuinely not running, and a waiter armed before the first Start deserves that
    // answer rather than a wait that never returns.
    void* m_stopped = nullptr;
};

}  // namespace Dns
