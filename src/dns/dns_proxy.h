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
// DNS Proxy: local DNS servers that forward every query to upstream resolvers.
//
// This server binds every listener its configuration declares and races, for each
// query, the upstream pool that listener names — returning whichever answer arrives
// first. Unlike LocalResolver it has no rules and never synthesizes answers: every
// query is forwarded to configured upstreams, which may use encrypted protocols
// (DoH, DoT, DNSCrypt) or plain DNS.
//
// It exists to let Nginx's resolver use DNS protocols that Nginx cannot speak
// directly: Nginx points its `resolver` directive at one of these listeners, which
// translates on its behalf. Several listeners exist so that different consumers can
// be given different transports — an endpoint racing encrypted upstreams beside one
// racing plain ones — which is a property of the file, not of this class.
//
// Shape of the thing: one event loop owns every client socket on every listener and
// never blocks. The loop parses a query, records it in a pending table under a
// freshly drawn transaction id, hands one task per upstream to a worker pool, and
// goes back to select(). Workers own their own upstream sockets, so nothing they do
// can stall the loop. Each finished task posts a completion back, which wakes the
// loop through a socket pair; the first valid answer is returned to the client and
// the token shared by that query's tasks is cancelled, closing the sockets of
// the ones still in flight.
//
// This mirrors LocalResolver's model — remember the query, match the reply when
// it arrives — with the forwarding itself moved off the loop, because unlike
// LocalResolver's upstreams these are remote servers that take a quarter of a
// second to answer.
#include <atomic>
#include <cstddef>
#include <cstdint>
#include <memory>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

#include "dns/dns_proxy_config.h"

namespace Dns {

// Sockets, in-flight state and the worker pool, defined in the implementation.
struct DnsProxyState;

class DnsProxy {
public:
    DnsProxy();
    ~DnsProxy();
    DnsProxy(const DnsProxy&) = delete;
    DnsProxy& operator=(const DnsProxy&) = delete;

    // Load configuration from `path`. Returns false if the file cannot be read or
    // declares no usable listener — a proxy with nothing to forward to, or with
    // nowhere to listen, is refused before it starts.
    bool LoadConfig(const std::wstring& path);

    // Bind every configured listener and run the event loop on a worker thread. Any
    // previous session is torn down first. Returns false — having bound nothing — if
    // any endpoint is unavailable or no upstreams are configured.
    //
    // All-or-nothing on purpose: a proxy that came up on some of its listeners would
    // answer for the names pointed at the ones it managed, and leave the rest of the
    // configuration silently unserved. Nginx resolving an upstream name through this
    // proxy cannot tell the two states apart, so a partial start is refused and the
    // caller rolls the whole stack back.
    bool Start();

    // Stop the loop, cancel everything in flight, and join every worker. Safe to
    // call when not running, and safe to call twice.
    void Stop();

    bool Running() const { return m_running.load(); }

    // The endpoints this proxy last started with, for the log and for a caller that
    // has to say where it tried to listen. Empty until a successful Start.
    std::vector<std::wstring> BoundEndpoints() const;

    // Signalled while the loop is NOT running, cleared by a successful Start().
    // Typed as void* rather than HANDLE for the same reason as LocalResolver.
    void* stoppedHandle() const { return m_stopped; }

private:
    // The event loop, on its own thread. It is handed the state rather than
    // reaching for it through m_state, so that Stop() can take ownership of that
    // state — and let the loop finish before destroying it — without the loop
    // ever dereferencing a pointer another thread has moved away.
    //
    // Everything the loop does once it is running is a free function in the
    // implementation, so that no type the implementation keeps to itself has to
    // appear here.
    void Loop(DnsProxyState& state);

    // The body of Stop(), without taking m_lifecycle. Split out because Start()
    // has to tear down the previous session and already holds that lock; calling
    // Stop() there would deadlock on a non-recursive mutex.
    void StopLocked();

    // Guards m_state and m_thread together. They are moved out as a pair by
    // Stop(), and read as a pair by Start(), so one lock has to cover both: a
    // state without its thread, or a thread without its state, is the state of
    // affairs this exists to make impossible.
    //
    // m_running is deliberately not covered by it — the loop reads that flag on
    // every pass and must never wait on a mutex a caller is holding.
    mutable std::mutex m_mx;

    // Serializes Start() and Stop() against each other, and is held across the
    // whole of each. m_mx alone cannot do this: it is released for the join, so
    // two callers could both pass the state hand-off and then race to install a
    // second session over the first's remains. A Stop() that arrives during a
    // Start() waits for it and then tears down what it built, which is exactly
    // what calling Stop() first inside Start() assumes cannot happen meanwhile.
    //
    // Never held by the loop, so a Stop() waiting here cannot deadlock against it.
    std::mutex m_lifecycle;

    DnsProxyConfig m_config;
    std::unique_ptr<DnsProxyState> m_state;
    std::thread m_thread;
    std::atomic<bool> m_running{false};

    // Manual-reset, created already signalled.
    void* m_stopped = nullptr;
};

}  // namespace Dns
