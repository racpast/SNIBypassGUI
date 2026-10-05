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

#include "dns/dns_proxy.h"

#include <winsock2.h>
#include <ws2tcpip.h>

#include <windows.h>

#include <algorithm>
#include <condition_variable>
#include <cstring>
#include <deque>
#include <functional>
#include <memory>
#include <system_error>
#include <utility>
#include <vector>

#include "app/logging.h"
#include "app/text.h"
#include "dns/cancel.h"
#include "dns/dnscrypt_cert_cache.h"
#include "dns/dnscrypt_client.h"
#include "dns/doh_client.h"
#include "dns/dot_client.h"
#include "dns/message.h"
#include "dns/plain_client.h"
#include "dns/socket_utils.h"
#include "dns/tcp_session.h"

namespace Dns {
namespace {

using SocketUtils::CloseSocket;
using SocketUtils::kMaxMessage;
using SocketUtils::kMaxTcpSessions;
using SocketUtils::kTcpIdleTimeoutMs;
using SocketUtils::kTickMs;
using SocketUtils::Now;
using SocketUtils::PrepareSessionSocket;
using SocketUtils::SendDatagram;
using SocketUtils::SetNonBlocking;
using SocketUtils::SocketHandle;
using SocketUtils::WaitSet;
using SocketUtils::WritePending;
using SocketUtils::WriteResult;

// Queries allowed to be raced at once. Each one holds a task per enabled
// upstream, so this is the backpressure that keeps a client burst from queueing
// work faster than the pool can drain it. A query past the cap is answered
// SERVFAIL rather than dropped: a resolver that goes silent makes its clients
// retry, which turns one overload into an avalanche.
constexpr size_t kMaxPendingQueries = 512;

// Enough workers for several independent queries to race every configured
// upstream at once. The queue remains bounded below, so a burst cannot turn this
// multiplier into unbounded delayed work.
constexpr size_t kParallelQueryCapacity = 4;
constexpr size_t kMaxWorkerThreads = 256;
constexpr size_t kWorkerQueueBatches = 8;

// Every socket the loop watches has to fit in one fd_set: two listeners, the
// wake pair, and one client connection per TCP session.
//
// One per session rather than the resolver's two: a query here runs on a pool
// thread that owns its upstream socket, so the loop itself holds only the
// client's half of a connection.
static_assert(kMaxTcpSessions + 4 <= FD_SETSIZE,
              "select() cannot watch that many sockets at once");

// ---- Racing state shared between the loop and the pool -----------------------

// Everything one query's tasks share.
//
// Reference counted because the loop can finish a query — and drop it — long
// before the workers still racing on its behalf have noticed. The last one out
// releases this, so a worker can never touch state the loop has freed.
struct RaceState {
    // Cancelled the moment a winner is chosen, or the query is given up on.
    // Closing the sockets registered with it is what wakes the workers still
    // blocked on their upstreams — the difference between abandoning a query and
    // merely ignoring its answer.
    CancelToken cancel;

    // The query handed to every task, and how long each may take.
    std::vector<uint8_t> message;
    uint32_t timeoutMs = 0;

    // Set once the loop has taken an answer, so a task that finishes late can see
    // its result is no longer wanted and skip posting it.
    std::atomic<bool> won{false};

    // Tasks finished and tasks started, so the loop can tell a query that is
    // still partly in flight from one that has been fully collected.
    std::atomic<size_t> done{0};
    size_t total = 0;
};

// ---- Worker pool -------------------------------------------------------------

// A fixed set of threads that run upstream queries. Fixed rather than one thread
// per query because the work is entirely blocking network I/O: a pool bounds how
// many threads and sockets exist at once, and a task whose query no longer
// matters frees its worker the instant cancellation closes its socket.
class WorkerPool {
public:
    WorkerPool() = default;
    ~WorkerPool() { Shutdown(); }

    WorkerPool(const WorkerPool&) = delete;
    WorkerPool& operator=(const WorkerPool&) = delete;

    // Start `count` threads. Returns false if any thread could not be created, in
    // which case whatever started is joined again before returning.
    bool Start(size_t count) {
        std::unique_lock<std::mutex> lock(m_mx);
        if (!m_threads.empty()) return false;
        m_stopping = false;
        // One client query fans out to every configured upstream. nginx sends a
        // little over twenty distinct lookups while loading the bundled config,
        // before the first cold TLS race has necessarily completed. Keep enough
        // bounded backlog for that normal startup burst so later names are not
        // rejected with SERVFAIL merely because earlier races filled two waves.
        m_maxQueued = count * kWorkerQueueBatches;

        try {
            m_threads.reserve(count);
            for (size_t i = 0; i < count; ++i) {
                m_threads.emplace_back([this] { Worker(); });
            }
        } catch (...) {
            // Whatever started has to be stopped before the exception leaves, or
            // the threads would outlive the object that owns them.
            m_stopping = true;
            lock.unlock();
            m_cv.notify_all();
            for (std::thread& t : m_threads) {
                if (t.joinable()) t.join();
            }
            lock.lock();
            m_threads.clear();
            m_stopping = false;
            return false;
        }
        return true;
    }

    // Join every worker, discarding queued work.
    void Shutdown() {
        {
            std::lock_guard<std::mutex> lock(m_mx);
            m_stopping = true;
            m_jobs.clear();
        }
        m_cv.notify_all();
        for (std::thread& t : m_threads) {
            if (t.joinable()) t.join();
        }
        m_threads.clear();
        m_stopping = false;
        m_maxQueued = 0;
    }

    // Queue one unit of work. False means the pool is stopped or at capacity.
    bool Submit(std::function<void()> job) {
        {
            std::lock_guard<std::mutex> lock(m_mx);
            if (m_stopping || m_threads.empty() || m_jobs.size() >= m_maxQueued) return false;
            m_jobs.push_back(std::move(job));
        }
        m_cv.notify_one();
        return true;
    }

private:
    void Worker() {
        for (;;) {
            std::function<void()> job;
            {
                std::unique_lock<std::mutex> lock(m_mx);
                m_cv.wait(lock, [this] { return m_stopping || !m_jobs.empty(); });
                if (m_jobs.empty()) {
                    if (m_stopping) return;
                    continue;
                }
                job = std::move(m_jobs.front());
                m_jobs.pop_front();
            }
            job();
        }
    }

    mutable std::mutex m_mx;
    std::condition_variable m_cv;
    std::deque<std::function<void()>> m_jobs;
    std::vector<std::thread> m_threads;
    size_t m_maxQueued = 0;
    bool m_stopping = false;
};

// ---- Wake socket pair --------------------------------------------------------

// A connected UDP socket pair on loopback, used to wake the loop's select() the
// instant a worker posts a completion.
//
// A self-pipe is the portable answer, and on Windows a connected UDP pair is the
// cheapest one: sending a byte to the peer makes the reading end readable, which
// select() reports like any other socket. A TCP pair would need a listener and a
// connection to set up; two UDP sockets need nothing but a bind and a connect.
class WakePair {
public:
    bool Open() {
        sockaddr_in addr = {};
        addr.sin_family = AF_INET;
        addr.sin_port = 0;  // ephemeral
        InetPtonW(AF_INET, kDnsProxyAddress, &addr.sin_addr);

        m_read.reset(socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP));
        if (!m_read.IsValid()) return false;

        if (bind(m_read.Get(), reinterpret_cast<const sockaddr*>(&addr), sizeof(addr)) ==
            SOCKET_ERROR) {
            m_read.Close();
            return false;
        }

        // The bound port is what the write end targets, so the address from bind
        // is re-read rather than assumed: port 0 asked the kernel to pick one.
        int len = sizeof(addr);
        if (getsockname(m_read.Get(), reinterpret_cast<sockaddr*>(&addr), &len) ==
            SOCKET_ERROR) {
            m_read.Close();
            return false;
        }

        m_write.reset(socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP));
        if (!m_write.IsValid()) {
            m_read.Close();
            return false;
        }
        if (connect(m_write.Get(), reinterpret_cast<const sockaddr*>(&addr), sizeof(addr)) ==
            SOCKET_ERROR) {
            m_write.Close();
            m_read.Close();
            return false;
        }

        SetNonBlocking(m_read.Get());
        SetNonBlocking(m_write.Get());
        return true;
    }

    SOCKET Read() const { return m_read.Get(); }

    // Ask the loop to come back around. Never blocks, and a lost wake-up only
    // delays a completion by one tick.
    void Wake() { send(m_write.Get(), "", 1, 0); }

    // Drain whatever wake-ups are queued.
    void Drain() {
        char buffer[64];
        while (recv(m_read.Get(), buffer, sizeof(buffer), 0) > 0) {
        }
    }

private:
    SocketHandle m_read;
    SocketHandle m_write;
};

// ---- Client-side connection state --------------------------------------------

// One TCP client connection. The upstream side is not a socket this loop owns:
// the query leaves through the same worker pool as a UDP one and the answer
// comes back as a completion. What is kept here is only what the client's own
// connection needs.
//
// The client-facing half — the socket, its reader, the response owed to it and
// the write cursor into that response — is the shared TcpClientConnection. What
// is added here is the state of the query this connection is waiting on, none of
// which the resolver has an analogue of: the resolver keeps the upstream
// connection in its own loop, while this one names the raced query instead.
struct TcpSession : TcpClientConnection {
    // A query has been dispatched for this client and a response is owed. Kept as
    // a flag rather than inferred from `race`, so the connection's state cannot
    // depend on a weak pointer that a racing task may already have outlived.
    bool busy = false;
    Query query;

    // The query being raced on this connection's behalf, so closing the
    // connection can cancel work that no longer has anyone to answer.
    std::weak_ptr<RaceState> race;

    // The loop's handle on this session, so a dispatched query can name its
    // connection without the loop searching for it. Set once, when the session
    // is created, and never after — the session cannot outlive the loop's own
    // shared_ptr, so this can never dangle where the weak_ptr can be locked.
    std::weak_ptr<TcpSession> self;
};

// One answer from one upstream, posted back to the loop.
struct Completion {
    // The query being answered. The loop finds the client to reply to by this, so
    // a completion for a query already answered or abandoned is dropped.
    std::shared_ptr<RaceState> race;

    std::vector<uint8_t> response;  // empty means this upstream failed
};

// One query the loop has dispatched and is waiting on.
struct PendingQuery {
    enum class Client : std::uint8_t { Udp, Tcp };

    std::shared_ptr<RaceState> race;
    Query query;
    uint64_t deadline = 0;
    Client origin = Client::Udp;

    // Where the answer goes. A UDP query carries the client's address; a TCP one
    // carries the session. The session is weak because the loop owns it and may
    // close the connection first, and because a strong reference here would keep
    // a dead connection alive for as long as its query is still running.
    sockaddr_storage address = {};
    int addressLen = 0;
    std::weak_ptr<TcpSession> session;

    // The size a UDP client can receive, from its own EDNS0 OPT, capped at what
    // this proxy is willing to send.
    //
    // Recorded when the query is registered because the answer arrives much later
    // from a worker that was handed the query's bytes and nothing else, and
    // because the client's advertisement is the one thing about its buffer that
    // the upstreams will never see: they speak TCP, so nothing in the path above
    // them truncates on this client's behalf. Unused for a TCP client, which has
    // no size limit to negotiate.
    size_t udpPayload = kMinUdpPayload;
};

}  // namespace

// Everything the loop owns. Its destructor is the only place sockets are
// released, which is what lets a failed Start() unwind by letting the object go.
//
// At namespace scope rather than in the anonymous namespace above, because
// DnsProxy holds one through unique_ptr and the header forward-declares
// Dns::DnsProxyState. Defined anywhere else it would be a different type, and
// the header's unique_ptr would be instantiated against an incomplete
// Dns::DnsProxyState — which is a hard error, not a warning.
struct DnsProxyState {
    SOCKET udp = INVALID_SOCKET;
    SOCKET tcp = INVALID_SOCKET;

    WakePair wake;
    WorkerPool pool;

    // Snapshotted at Start(), so the loop never takes the config lock and a
    // reload cannot change the upstream set underneath a query already in flight.
    std::vector<DnsProxyEndpoint> upstreams;
    uint32_t timeoutMs = 0;

    std::vector<std::shared_ptr<TcpSession>> sessions;
    std::vector<PendingQuery> pending;

    // DNSCrypt provider certificates, for as long as this session lasts. It is a
    // member rather than a global so its lifetime is the session's: the workers
    // that use it are shut down by ~DnsProxyState before this is destroyed, and
    // no query can outlive the state that owns it.
    CertCache certCache;

    // Completions posted by workers, drained once per loop pass. Guarded by its
    // own mutex, taken only for the push and the swap — never while a task runs.
    std::mutex completionsMx;
    std::vector<Completion> completions;

    std::vector<uint8_t> scratch;  // one datagram at a time

    // The descriptor sets for one pass, kept here rather than in the loop body
    // because they are several hundred bytes and the loop is the hot path this
    // whole design exists to keep cheap.
    WaitSet waits;

    ~DnsProxyState() {
        pool.Shutdown();
        for (std::shared_ptr<TcpSession>& session : sessions) {
            CloseSocket(session->socket);
        }
        CloseSocket(udp);
        CloseSocket(tcp);
    }

    // Post a finished task's answer and wake the loop.
    void Post(const std::shared_ptr<RaceState>& race, std::vector<uint8_t> response) {
        {
            std::lock_guard<std::mutex> lock(completionsMx);
            completions.push_back(Completion{race, std::move(response)});
        }
        wake.Wake();
    }
};

namespace {
// ---- Query helpers -----------------------------------------------------------

// The size this proxy may reply to a UDP client with, from the client's own
// EDNS0 OPT and capped by what we are willing to send.
//
// The proxy faces encrypted upstreams, all of which speak TCP, so nothing between
// here and the answer ever truncated on this client's behalf: an upstream will
// happily return a megabyte of TXT records, and without this the proxy would
// `sendto` it whole. On Windows that does not become a dropped datagram the
// client retries — it becomes WSAEMSGSIZE out of the client's own recv, which
// ends that lookup with a hard error.
size_t NegotiateUdpPayload(const uint8_t* message, size_t len) {
    return std::min<size_t>(QueryUdpPayloadSize(message, len), kMaxUdpPayload);
}

// The response this proxy gives for a query when no upstream answered: the header
// and question echoed back with SERVFAIL, so the client is told the lookup failed
// rather than left waiting for a reply that is not coming.
std::vector<uint8_t> Failure(const RaceState& race, const Query& query) {
    return BuildStatusResponse(race.message.data(), race.message.size(), query, kRcodeServFail);
}

// Whether `response` is a well-formed answer to the query in `race`.
//
// The transports here are authenticated, but the response still has to belong to
// this particular request: the transaction id and question must both match. It
// must also be a response rather than a query or UPDATE, with the standard opcode.
// A malformed or mismatched message is discarded rather than handed to the client.
//
// A truncated response (TC=1) is rejected, and by the time it reaches here that
// is a failure of the escalation rather than a size negotiation.
//
// The encrypted transports cannot truncate at all: DoH, DoT and DNSCrypt speak
// TCP, where there is no datagram size to exceed. Plain DNS can, and its client
// answers TC=1 by asking the same server again over TCP — so a truncated message
// only gets this far if that retry also came back truncated or could not be made,
// in which case the answer is incomplete and there is nothing here to complete
// it with. Discarding lets another racing upstream win instead of handing the
// client a half answer it would have to notice and retry itself.
bool ValidAnswer(const std::vector<uint8_t>& response, const RaceState& race,
                 const Query& query) {
    if (response.size() < 12) return false;
    if (query.questionEnd < 12 || query.questionEnd > race.message.size()) return false;
    if (response.size() < query.questionEnd) return false;
    if (response[0] != race.message[0] || response[1] != race.message[1]) return false;

    const uint16_t flags =
        static_cast<uint16_t>((static_cast<unsigned>(response[2]) << 8u) | response[3]);
    if ((flags & 0x8000u) == 0) return false;  // QR: must be a response
    if (((static_cast<unsigned>(flags) >> 11u) & 0xFu) != 0)
        return false;  // opcode: must be QUERY
    if (IsTruncated(response.data(), response.size())) return false;

    // The same question has to come back, byte for byte, from the QNAME to the
    // end of QCLASS. A response that answers something else is not an answer.
    return std::memcmp(response.data() + 12, race.message.data() + 12,
                       query.questionEnd - 12) == 0;
}

// Query one upstream endpoint once.
//
// This is the only place the loop's work touches the network, and it runs on a
// pool thread. `race.cancel` is what makes it abandonable: the clients register
// every socket they open with it, so cancelling closes them and the blocking read
// underneath returns failure at once rather than at its own deadline.
std::vector<uint8_t> QueryUpstream(const DnsProxyEndpoint& upstream, const RaceState& race,
                                   CertCache& certCache) {
    try {
        switch (upstream.protocol) {
            case DnsProxyProtocol::PlainDNS:
                return QueryPlainDns(race.message, upstream.address, race.timeoutMs,
                                     &race.cancel);
            case DnsProxyProtocol::DoH:
                return QueryDoH(race.message, upstream.address, upstream.hostname,
                                upstream.path, upstream.certificateHashes, race.timeoutMs,
                                &race.cancel);
            case DnsProxyProtocol::DoT:
                return QueryDoT(race.message, upstream.address, upstream.hostname,
                                upstream.certificateHashes, race.timeoutMs, &race.cancel);
            case DnsProxyProtocol::DNSCrypt:
                return QueryDNSCrypt(race.message, upstream.address, upstream.providerName,
                                     upstream.publicKey, certCache, race.timeoutMs,
                                     &race.cancel);
            default:
                // Unreachable: the enum's only other value is Unknown, and the
                // config loader discards a stamp it cannot classify. Spelled out
                // rather than left off the switch so that adding a transport is a
                // compile error here rather than a silent empty answer at runtime.
                return {};
        }
    } catch (...) {
        // A client that throws is a client that failed; the race carries on
        // without it, which is the reason several are asked at once.
        return {};
    }
}

// Hand one query to the pool: one task per enabled upstream, all sharing `race`.
// Called from the loop, which is the only thread that touches `pending` and
// `sessions`; the tasks themselves only ever touch `race` and the network.
void DispatchQuery(DnsProxyState& s, const std::shared_ptr<RaceState>& race) {
    race->total = 0;
    race->done.store(0, std::memory_order_relaxed);

    for (const DnsProxyEndpoint& upstream : s.upstreams) {
        DnsProxyEndpoint endpoint = upstream;  // copied into the task, not referenced
        const bool submitted = s.pool.Submit([&s, endpoint, race] {
            std::vector<uint8_t> response;
            if (!race->won.load(std::memory_order_acquire)) {
                response = QueryUpstream(endpoint, *race, s.certCache);
            }
            // Counted even when the query was already won or abandoned: the loop
            // uses this to know when the query can be dropped for good.
            race->done.fetch_add(1, std::memory_order_acq_rel);
            // Empty failures are posted too. Without that notification an all-
            // failed race sits pending until its deadline despite every task
            // already having finished.
            s.Post(race, std::move(response));
        });
        if (submitted) ++race->total;
    }

    // A saturated pool refuses this query immediately instead of putting work in
    // an unbounded deque that cannot start before the query's deadline.
    if (race->total == 0) s.Post(race, {});
}

// ---- Delivery ----------------------------------------------------------------

// The client's own datagram socket, through the shared helper, so the empty
// check and the cast live beside the other one.
void SendUdp(DnsProxyState& s, const std::vector<uint8_t>& message, const sockaddr_storage& to,
             int toLen) {
    SendDatagram(s.udp, message, to, toLen);
}

// Hand a response to the client that asked for it. A TCP client that has since
// gone away is simply dropped: the connection is already being torn down.
void Deliver(DnsProxyState& s, PendingQuery& pending, const std::vector<uint8_t>& response) {
    if (pending.origin == PendingQuery::Client::Udp) {
        // The last chance to notice the answer does not fit. A UDP client's
        // buffer size is a property of the client, not of the query, so it cannot
        // be inferred by any upstream — and every upstream here speaks TCP, where
        // nothing truncates. Cutting to the client's size and setting TC is what
        // converts an oversized answer into one the client can act on: it asks
        // again over TCP, and that path has no size limit at all.
        std::vector<uint8_t> payload = response;
        const UdpBudget budget =
            ApplyUdpBudget(pending.race->message.data(), pending.race->message.size(), payload,
                           pending.udpPayload);
        if (budget == UdpBudget::CannotFit) {
            SendUdp(s, Failure(*pending.race, pending.query), pending.address,
                    pending.addressLen);
            return;
        }
        SendUdp(s, payload, pending.address, pending.addressLen);
        return;
    }
    const std::shared_ptr<TcpSession> session = pending.session.lock();
    if (!session) return;
    session->busy = false;
    session->race.reset();
    // The shared member, which frames, resets the cursor, and arms the idle
    // deadline in one step. False means the answer is too long for a 16-bit
    // prefix, so it cannot be sent at all and the connection goes.
    if (!session->QueueResponse(response)) {
        CloseSocket(session->socket);
        return;
    }
}

}  // namespace

// ---- DnsProxy implementation -------------------------------------------------

DnsProxy::DnsProxy() {
    m_stopped =
        CreateEventW(nullptr, TRUE, TRUE, nullptr);  // manual-reset, initially signalled
}

DnsProxy::~DnsProxy() {
    Stop();
    if (m_stopped) CloseHandle(m_stopped);
}

bool DnsProxy::LoadConfig(const std::wstring& path) {
    std::lock_guard<std::mutex> lock(m_mx);
    m_config = DnsProxyConfig::Load(path);

    if (m_config.EnabledCount() == 0) {
        LOGE(L"DNS forwarder: no enabled upstreams in config");
        return false;
    }

    return true;
}

bool DnsProxy::Start() {
    // Held for the whole of Start, so a Stop() arriving midway waits rather than
    // tearing down half a session, and so two Starts cannot both get past the
    // "no session yet" read below and install one over the other.
    std::lock_guard<std::mutex> lifecycle(m_lifecycle);

    // A previous session may have ended on its own, leaving a joinable thread and
    // closed sockets behind. Tearing that down first means Start always begins
    // from a clean state and can never stack a second loop on the first.
    StopLocked();

    if (!m_stopped) {
        LOGE(L"DNS forwarder: no state event; refusing to start a server nobody can watch.");
        return false;
    }

    if (!SocketUtils::EnsureWinsock()) {
        LOGE(L"DNS forwarder: cannot initialize Winsock");
        return false;
    }

    // Read once, here, so a reload while the loop runs cannot change the pool
    // size or the upstream set underneath queries already in flight.
    std::vector<DnsProxyEndpoint> upstreams;
    uint32_t timeoutMs = 0;
    size_t poolSize = 0;
    {
        std::lock_guard<std::mutex> lock(m_mx);
        upstreams = m_config.EnabledUpstreams();
        timeoutMs = m_config.timeoutMs;
        poolSize = m_config.threadPoolSize;
    }
    if (upstreams.empty()) {
        LOGE(L"DNS forwarder: no enabled upstreams, cannot start");
        return false;
    }

    const size_t scaledPool = upstreams.size() * kParallelQueryCapacity;
    poolSize = std::min(kMaxWorkerThreads, std::max(poolSize, scaledPool));

    // Held locally until everything has succeeded: on any failure the state
    // object goes out of scope and its destructor closes what was already opened.
    auto state = std::make_unique<DnsProxyState>();
    state->upstreams = std::move(upstreams);
    state->timeoutMs = timeoutMs;
    state->scratch.resize(kMaxMessage);

    state->udp =
        SocketUtils::BindListener(kDnsProxyAddress, kDnsProxyPort, SOCK_DGRAM, IPPROTO_UDP);
    if (state->udp == INVALID_SOCKET) {
        LOGE(L"DNS forwarder: cannot bind UDP listener (err " +
             std::to_wstring(WSAGetLastError()) + L")");
        return false;
    }
    // Without this a single dead upstream's ICMP port-unreachable fails every
    // later read on this socket, which is the socket every client shares.
    SocketUtils::DisableUdpConnReset(state->udp);

    state->tcp =
        SocketUtils::BindListener(kDnsProxyAddress, kDnsProxyPort, SOCK_STREAM, IPPROTO_TCP);
    if (state->tcp == INVALID_SOCKET || listen(state->tcp, SOMAXCONN) == SOCKET_ERROR) {
        LOGE(L"DNS forwarder: cannot listen on TCP (err " + std::to_wstring(WSAGetLastError()) +
             L")");
        return false;
    }

    if (!state->wake.Open()) {
        LOGE(L"DNS forwarder: cannot open the wake socket pair (err " +
             std::to_wstring(WSAGetLastError()) + L")");
        return false;
    }

    if (!state->pool.Start(poolSize)) {
        LOGE(L"DNS forwarder: cannot start the worker pool");
        return false;
    }

    // Cleared before the thread exists, so the loop can only ever set it again —
    // there is no ordering in which a real "the loop ended" signal is cleared by
    // the start that came before it.
    ResetEvent(m_stopped);

    // m_state, m_thread and m_running are written under the lock as one step, so
    // a Stop() running concurrently either sees this whole session (and tears it
    // down) or sees none of it (and leaves it alone). Seeing half of it — the
    // thread without the state, or the state without the thread — is the failure
    // this guards against, and it is the one that crashes on shutdown.
    std::lock_guard<std::mutex> lock(m_mx);
    m_state = std::move(state);
    m_running.store(true);

    try {
        // The state is passed by reference rather than looked up on each pass:
        // Stop() moves m_state out from under this thread, and a loop that read
        // it through the member would dereference an empty unique_ptr the moment
        // that happened. The reference stays valid until Stop() joins this
        // thread, which is the contract the whole teardown rests on.
        DnsProxyState& loopState = *m_state;
        m_thread = std::thread([this, &loopState] { Loop(loopState); });
    } catch (const std::system_error& e) {
        LOGE(L"DNS forwarder: cannot create thread (" + Utf8ToWide(e.what()) + L")");
        m_running.store(false);
        SetEvent(m_stopped);
        m_state.reset();
        return false;
    }

    LOGI(L"DNS forwarder started on " + std::wstring(kDnsProxyAddress) + L":" +
         std::to_wstring(kDnsProxyPort) + L" with " + std::to_wstring(poolSize) +
         L" worker(s) and " + std::to_wstring(m_state->upstreams.size()) + L" upstream(s)");
    return true;
}

void DnsProxy::Stop() {
    // Serialized against Start() so the two can never interleave. A Stop() that
    // arrives while a Start() is running waits for it and then tears down what it
    // built; a Stop() that arrives while another Stop() is running waits and then
    // finds nothing left to do, which is what makes this reentrant.
    std::lock_guard<std::mutex> lifecycle(m_lifecycle);
    StopLocked();
}

void DnsProxy::StopLocked() {
    // Idempotent: a second caller finds the flag already clear and nothing left
    // to take, because the state and the thread were moved out as a pair.
    m_running.store(false);

    // The thread and the state are taken together, under the one lock that covers
    // both, so two concurrent Stops cannot both claim them and the pair can never
    // be split. Whichever caller does not get them finds them gone and returns.
    std::unique_ptr<DnsProxyState> state;
    std::thread thread;
    {
        std::lock_guard<std::mutex> lock(m_mx);
        thread = std::move(m_thread);
        state = std::move(m_state);
    }

    // Join before touching the sockets: the loop is the only other thing that
    // reads them, and it must be gone before they are closed under it.
    if (thread.joinable()) thread.join();

    if (state) {
        // The loop is no longer running, so nothing else will touch these. Cancel
        // everything still in flight: that unblocks the workers, and Shutdown()
        // then joins them. Only after this is it safe for the sockets and the pool
        // to be destroyed.
        for (PendingQuery& pending : state->pending) {
            pending.race->cancel.Cancel();
        }
        for (std::shared_ptr<TcpSession>& session : state->sessions) {
            if (const std::shared_ptr<RaceState> race = session->race.lock()) {
                race->cancel.Cancel();
            }
        }
        state->pool.Shutdown();
        state.reset();
    }

    SetEvent(m_stopped);
}

namespace {
// ---- Query intake ------------------------------------------------------------

// Register a client's query and race it against every upstream. Called only from
// the loop thread, which is the only thread that touches `pending`.
void HandleQuery(DnsProxyState& s, const uint8_t* message, size_t len,
                 const sockaddr_storage& client, int clientLen) {
    Query query;
    // A message that cannot be read has no question to echo back, so there is
    // nothing to answer and nothing to forward.
    if (!ParseQuery(message, len, query)) {
        // Logged with the length and the source: a malformed query is either a
        // broken client or a stray datagram, and which one it is decides whether
        // anything is worth doing about it.
        LOGW(L"DNS forwarder: malformed query of " + std::to_wstring(len) + L" byte(s) from " +
             SocketUtils::AddressText(client, clientLen, true));
        return;
    }

    auto race = std::make_shared<RaceState>();
    race->message.assign(message, message + len);
    race->timeoutMs = s.timeoutMs;

    if (s.pending.size() >= kMaxPendingQueries) {
        // Refused, but answered: a client told the lookup failed will retry
        // against whatever else it has, while one that hears nothing just waits.
        SendUdp(s, Failure(*race, query), client, clientLen);
        return;
    }

    PendingQuery pending;
    pending.race = race;
    pending.query = std::move(query);
    pending.deadline = Now() + s.timeoutMs + kTickMs;
    pending.origin = PendingQuery::Client::Udp;
    pending.address = client;
    pending.addressLen = clientLen;
    // Read now, from the client's own bytes: the answer comes back long after
    // this datagram is gone, and nothing downstream of here knows how big a
    // buffer the client has.
    pending.udpPayload = NegotiateUdpPayload(message, len);
    s.pending.push_back(std::move(pending));

    DispatchQuery(s, race);
}

// ---- TCP intake --------------------------------------------------------------

// Take an accepted client into the session set.
//
// At the cap, replace the longest-idle session. Busy sessions are never evicted:
// they already have an answer in flight. If all sessions are busy, reject the
// newcomer so the vector can never outgrow the fd_set sized for it.
//
// The resolver's acceptor refuses the newcomer instead, and both are right for
// themselves. The reason this one can afford to evict: a query here is being
// served by a worker thread holding its own upstream socket, so a session costs
// the loop one descriptor and nothing else, and a client whose connection is
// dropped is free to open another. The resolver holds two descriptors per
// session inside this same loop, and dropping one would abandon a client already
// waiting on a reply from a real server.
void AcceptTcpClient(DnsProxyState& s, SOCKET client) {
    if (s.sessions.size() >= kMaxTcpSessions) {
        // The longest-idle session that is not mid-answer. Erasing a busy one
        // would drop a client waiting on an answer it has already been promised.
        size_t victim = s.sessions.size();
        uint64_t oldest = UINT64_MAX;
        for (size_t i = 0; i < s.sessions.size(); ++i) {
            if (s.sessions[i]->busy) continue;
            if (s.sessions[i]->deadline < oldest) {
                oldest = s.sessions[i]->deadline;
                victim = i;
            }
        }

        if (victim == s.sessions.size()) {
            closesocket(client);
            return;
        }
        if (const std::shared_ptr<RaceState> race = s.sessions[victim]->race.lock()) {
            race->cancel.Cancel();
        }
        CloseSocket(s.sessions[victim]->socket);
        s.sessions.erase(s.sessions.begin() + static_cast<ptrdiff_t>(victim));
    }

    if (!PrepareSessionSocket(client)) return;
    auto session = std::make_shared<TcpSession>();
    session->socket = client;
    session->deadline = Now() + kTcpIdleTimeoutMs;
    // The session's own handle on itself, so a query it dispatches can name this
    // connection without the loop walking the list to find it.
    session->self = session;
    s.sessions.push_back(std::move(session));
}

void ServiceTcpSession(DnsProxyState& s, TcpSession& session) {
    // One query owns a connection at a time: a second one that arrives early
    // waits in the reader until this one is answered.
    if (session.busy) return;

    while (session.reader.HasMessage()) {
        const std::vector<uint8_t> message = session.reader.TakeMessage();
        Query query;
        // A malformed message leaves the framing intact but has no question to
        // answer, so the connection stays open for the next one.
        if (!ParseQuery(message.data(), message.size(), query)) {
            LOGW(L"DNS forwarder: malformed TCP query of " + std::to_wstring(message.size()) +
                 L" byte(s)");
            continue;
        }

        auto race = std::make_shared<RaceState>();
        race->message = message;
        race->timeoutMs = s.timeoutMs;

        session.busy = true;
        session.query = query;
        session.race = race;
        session.deadline = Now() + s.timeoutMs + kTickMs;

        PendingQuery pending;
        pending.race = race;
        pending.query = std::move(query);
        pending.deadline = session.deadline;
        pending.origin = PendingQuery::Client::Tcp;
        // The session's own weak pointer, so the query refers back to the
        // connection without keeping it alive and without searching the list.
        pending.session = session.self;
        // udpPayload is left at its default: a TCP client has no datagram to fit
        // an answer into, and the length prefix carries the size it does have.
        s.pending.push_back(std::move(pending));

        DispatchQuery(s, race);
        return;
    }
}

// ---- Harvesting --------------------------------------------------------------

void DrainCompletions(DnsProxyState& s) {
    std::vector<Completion> finished;
    {
        std::lock_guard<std::mutex> lock(s.completionsMx);
        finished.swap(s.completions);
    }
    if (finished.empty()) return;

    for (Completion& completion : finished) {
        const std::shared_ptr<RaceState>& race = completion.race;

        // The query this answers, if it is still open. One already answered or
        // expired has been erased, and this is dropped.
        const auto it =
            std::find_if(s.pending.begin(), s.pending.end(),
                         [&race](const PendingQuery& pending) { return pending.race == race; });
        if (it == s.pending.end()) continue;

        const bool decided = race->won.load(std::memory_order_acquire);

        if (!decided && !completion.response.empty() &&
            ValidAnswer(completion.response, *race, it->query)) {
            race->won.store(true, std::memory_order_release);
            race->cancel.Cancel();  // stop the others still in flight
            Deliver(s, *it, completion.response);
            s.pending.erase(it);
            continue;
        }

        // No usable answer from this upstream. Only when it was the last one out
        // is the client told the lookup failed.
        const bool last = race->done.load(std::memory_order_acquire) >= race->total;
        if (!decided && last) {
            race->won.store(true, std::memory_order_release);
            Deliver(s, *it, Failure(*race, it->query));
            s.pending.erase(it);
        }
    }
}

void ExpireQueries(DnsProxyState& s) {
    const uint64_t now = Now();

    for (auto it = s.pending.begin(); it != s.pending.end();) {
        if (now < it->deadline) {
            ++it;
            continue;
        }
        // Past its deadline. Cancel first so the workers stop, then answer the
        // client if no answer has been taken already.
        it->race->cancel.Cancel();
        if (!it->race->won.exchange(true, std::memory_order_acq_rel)) {
            Deliver(s, *it, Failure(*it->race, it->query));
        }
        // A TCP client whose query ran out is free to send the next one; the
        // connection itself is not at fault and stays open.
        if (it->origin == PendingQuery::Client::Tcp) {
            if (const std::shared_ptr<TcpSession> session = it->session.lock()) {
                session->busy = false;
                session->race.reset();
                session->deadline = now + kTcpIdleTimeoutMs;
            }
        }
        it = s.pending.erase(it);
    }
}

void ExpireTcpSessions(DnsProxyState& s) {
    const uint64_t now = Now();
    for (size_t i = 0; i < s.sessions.size();) {
        TcpSession& session = *s.sessions[i];
        if (now < session.deadline) {
            ++i;
            continue;
        }

        if (const std::shared_ptr<RaceState> race = session.race.lock()) {
            race->cancel.Cancel();
        }
        CloseSocket(session.socket);
        s.sessions.erase(s.sessions.begin() + static_cast<ptrdiff_t>(i));
    }
}

}  // namespace

// ---- The loop ----------------------------------------------------------------

void DnsProxy::Loop(DnsProxyState& s) {
    while (m_running.load()) {
        // The same select skeleton the resolver runs. The wake socket is what
        // makes this one differ in kind rather than in shape: a completion
        // posted by a worker arrives without anything else happening, so this
        // loop has something to be woken by and the resolver does not.
        s.waits.Reset();
        s.waits.WatchRead(s.udp);
        s.waits.WatchRead(s.tcp);
        s.waits.WatchRead(s.wake.Read());

        for (const std::shared_ptr<TcpSession>& session : s.sessions) {
            if (session->out.empty()) {
                s.waits.WatchRead(session->socket);
            } else {
                s.waits.WatchWrite(session->socket);
            }
        }

        // Bounded so deadlines are still checked when nothing arrives; a
        // completion wakes this immediately rather than waiting the tick out.
        if (!s.waits.Wait()) {
            LOGE(L"DNS forwarder: select failed (err " + std::to_wstring(WSAGetLastError()) +
                 L"); the forwarder is stopping.");
            break;
        }

        if (!s.waits.TimedOut()) {
            if (s.waits.Readable(s.wake.Read())) s.wake.Drain();

            if (s.waits.Readable(s.udp)) {
                sockaddr_storage from = {};
                int fromLen = sizeof(from);
                const int received = recvfrom(s.udp, reinterpret_cast<char*>(s.scratch.data()),
                                              static_cast<int>(s.scratch.size()), 0,
                                              reinterpret_cast<sockaddr*>(&from), &fromLen);
                if (received > 0) {
                    HandleQuery(s, s.scratch.data(), static_cast<size_t>(received), from,
                                fromLen);
                }
            }

            if (s.waits.Readable(s.tcp)) {
                sockaddr_storage from = {};
                int fromLen = sizeof(from);
                const SOCKET client =
                    accept(s.tcp, reinterpret_cast<sockaddr*>(&from), &fromLen);
                if (client != INVALID_SOCKET) AcceptTcpClient(s, client);
            }

            for (size_t i = 0; i < s.sessions.size();) {
                TcpSession& session = *s.sessions[i];
                bool keep = true;

                if (s.waits.Writable(session.socket)) {
                    const WriteResult written =
                        WritePending(session.socket, session.out, session.outSent);
                    if (written == WriteResult::Failed) {
                        keep = false;
                    } else if (written == WriteResult::Done) {
                        // Keep the connection for the next query, so a client does
                        // not pay for a fresh TCP handshake on every lookup.
                        session.out.clear();
                        session.outSent = 0;
                        session.deadline = Now() + kTcpIdleTimeoutMs;
                        ServiceTcpSession(s, session);
                    }
                } else if (s.waits.Readable(session.socket)) {
                    char buffer[4096];
                    const int received = recv(session.socket, buffer, sizeof(buffer), 0);
                    if (received < 0) {
                        if (WSAGetLastError() != WSAEWOULDBLOCK) keep = false;
                    } else if (received == 0 ||
                               session.reader.Append(reinterpret_cast<const uint8_t*>(buffer),
                                                     static_cast<size_t>(received)) ==
                                   TcpSessionReader::State::Broken) {
                        keep = false;  // the client closed its half
                    } else {
                        if (session.out.empty()) session.deadline = Now() + kTcpIdleTimeoutMs;
                        ServiceTcpSession(s, session);
                    }
                }

                if (keep) {
                    ++i;
                    continue;
                }
                // Cancel what the connection was waiting on before dropping it:
                // otherwise those tasks keep racing for an answer nobody will read.
                if (const std::shared_ptr<RaceState> race = session.race.lock()) {
                    race->cancel.Cancel();
                }
                CloseSocket(session.socket);
                s.sessions.erase(s.sessions.begin() + static_cast<ptrdiff_t>(i));
            }
        }

        // Completions are drained every pass, whether or not select() reported
        // anything: a query that finished between the wake-up and here still has
        // to be collected.
        DrainCompletions(s);
        ExpireQueries(s);
        ExpireTcpSessions(s);
    }

    // The loop can end without anyone asking — a failed select is the only way,
    // but it is a way. Clearing the flag here keeps "is the forwarder running" an
    // honest answer, and signalling the event is what tells a waiter rather than
    // leaving it to poll.
    m_running.store(false);
    SetEvent(m_stopped);
    LOGI(L"DNS forwarder loop ended");
}

}  // namespace Dns
