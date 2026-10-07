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

#include "dns/resolver.h"

#include <mswsock.h>
#include <winsock2.h>
#include <ws2tcpip.h>

#include <windows.h>

#include <iphlpapi.h>

#include <cstring>
#include <system_error>
#include <utility>
#include <vector>

#include "app/logging.h"
#include "app/text.h"
#include "dns/answer.h"
#include "dns/message.h"
#include "dns/socket_utils.h"
#include "dns/tcp_session.h"
#include "platform/socket_runtime.h"

namespace Dns {
namespace {

using SocketUtils::CloseSocket;
using SocketUtils::kMaxMessage;
using SocketUtils::kMaxTcpSessions;
using SocketUtils::kTcpIdleTimeoutMs;
using SocketUtils::Now;
using SocketUtils::PrepareSessionSocket;
using SocketUtils::SameHost;
using SocketUtils::SendDatagram;
using SocketUtils::SetNonBlocking;
using SocketUtils::WaitSet;
using SocketUtils::WritePending;
using SocketUtils::WriteResult;

// Outstanding forwards, and how long one waits before the client is told the
// lookup failed rather than left waiting for a reply that is not coming.
constexpr size_t kMaxPendingUdp = 256;
constexpr uint64_t kForwardTimeoutMs = 4000;

// A query is sent to this many of the machine's DNS servers at once. Asking two
// costs one extra datagram and removes the need to notice that the first one is
// dead and try the next: whichever answers first is the answer.
constexpr size_t kUpstreamsQueried = 2;

// How long a discovered server list is trusted before it is read again. Joining a
// VPN or renewing a lease replaces the machine's resolvers underneath us.
constexpr uint64_t kUpstreamRefreshMs = 10000;

// Every socket the loop can watch has to fit in one fd_set: two listeners, two
// upstream sockets, and a client plus an upstream connection per TCP session.
//
// Two per session rather than one, which is what sets this limit apart from the
// forwarder's: a resolver serves a client by opening a connection of its own to
// the upstream and holding both sockets in this same loop, so neither of them
// can be buried in a worker.
static_assert(kMaxTcpSessions * 2 + 4 <= FD_SETSIZE,
              "select() cannot watch that many sockets at once");

// Bind the resolver's UDP listener on `endpoint`.
SOCKET BindUdpListener(const BindEndpoint& endpoint) {
    return SocketUtils::BindListener(endpoint.address.c_str(), endpoint.port, SOCK_DGRAM,
                                     IPPROTO_UDP);
}

// Bind and listen on the resolver's TCP listener.
SOCKET BindTcpListener(const BindEndpoint& endpoint) {
    const SOCKET s = SocketUtils::BindListener(endpoint.address.c_str(), endpoint.port,
                                               SOCK_STREAM, IPPROTO_TCP);
    if (s == INVALID_SOCKET) return INVALID_SOCKET;
    if (listen(s, SOMAXCONN) == SOCKET_ERROR) {
        const int err = WSAGetLastError();
        SOCKET failed = s;
        CloseSocket(failed);
        WSASetLastError(err);
        return INVALID_SOCKET;
    }
    return s;
}

// An ephemeral socket for talking to real DNS servers of one family.
SOCKET MakeUpstreamSocket(int family) {
    SOCKET s = socket(family, SOCK_DGRAM, IPPROTO_UDP);
    if (s == INVALID_SOCKET) return INVALID_SOCKET;
    SocketUtils::DisableUdpConnReset(s);
    SetNonBlocking(s);
    return s;
}

// ---- Upstream discovery ------------------------------------------------------

struct Upstream {
    sockaddr_storage addr = {};
    int len = 0;
};

// True if `sa` is a server worth sending a query to.
//
// `self` is this resolver's own bound address. Forwarding to it would be a loop with
// itself: the query would come back here, match a rule or be forwarded again, and
// the client would get either no answer or the same one it just sent. Passed in
// rather than read from a constant because the endpoint is configuration, and a
// check against the wrong address would only fail the machine where the two differ.
bool UsableUpstream(const sockaddr* sa, const BindEndpoint& self) {
    if (sa->sa_family == AF_INET) {
        const in_addr& v4 = reinterpret_cast<const sockaddr_in*>(sa)->sin_addr;
        if (v4.s_addr == 0) return false;  // "no server configured"

        // Our own listener, when it is an IPv4 address. A v6 endpoint cannot match
        // a v4 upstream, and InetPtonW simply fails on one, which leaves this check
        // as the "not ours" it should be.
        in_addr own = {};
        if (InetPtonW(AF_INET, self.address.c_str(), &own) == 1) return v4.s_addr != own.s_addr;
        return true;
    }
    if (sa->sa_family == AF_INET6) {
        const in6_addr& v6 = reinterpret_cast<const sockaddr_in6*>(sa)->sin6_addr;
        const uint8_t* b = v6.s6_addr;

        bool allZero = true;
        for (int i = 0; i < 16; ++i)
            if (b[i] != 0) allZero = false;
        if (allZero) return false;

        // fec0:0:0:ffff::1 through ::3 are the placeholders Windows lists when a
        // machine has no IPv6 resolver of its own. Nothing answers on them.
        static const uint8_t kPlaceholderPrefix[14] = {0xFE, 0xC0, 0, 0, 0, 0, 0xFF,
                                                       0xFF, 0,    0, 0, 0, 0, 0};
        if (std::memcmp(b, kPlaceholderPrefix, sizeof(kPlaceholderPrefix)) == 0 && b[14] == 0 &&
            b[15] >= 1 && b[15] <= 3)
            return false;

        // Our own listener, when it is an IPv6 one.
        in6_addr own = {};
        if (InetPtonW(AF_INET6, self.address.c_str(), &own) == 1 &&
            std::memcmp(b, own.s6_addr, 16) == 0)
            return false;
        return true;
    }
    return false;
}

// The DNS servers configured on every adapter that is up, deduplicated and in the
// order Windows reports them — which is the order Windows itself would try.
std::vector<Upstream> DiscoverUpstreams(const BindEndpoint& self) {
    std::vector<Upstream> out;

    ULONG size = 16 * 1024;
    std::vector<uint8_t> buffer;
    ULONG status = ERROR_BUFFER_OVERFLOW;
    for (int attempt = 0; attempt < 4 && status == ERROR_BUFFER_OVERFLOW; ++attempt) {
        buffer.assign(size, 0);
        status = GetAdaptersAddresses(
            AF_UNSPEC,
            GAA_FLAG_SKIP_UNICAST | GAA_FLAG_SKIP_ANYCAST | GAA_FLAG_SKIP_MULTICAST |
                GAA_FLAG_SKIP_FRIENDLY_NAME,
            nullptr, reinterpret_cast<IP_ADAPTER_ADDRESSES*>(buffer.data()), &size);
    }
    if (status != NO_ERROR) {
        LOGW(L"Resolver: cannot read the machine's DNS servers (err " +
             std::to_wstring(status) + L").");
        return out;
    }

    for (auto* adapter = reinterpret_cast<IP_ADAPTER_ADDRESSES*>(buffer.data()); adapter;
         adapter = adapter->Next) {
        if (adapter->OperStatus != IfOperStatusUp) continue;
        for (auto* server = adapter->FirstDnsServerAddress; server; server = server->Next) {
            const sockaddr* sa = server->Address.lpSockaddr;
            const int len = server->Address.iSockaddrLength;
            if (!sa || len <= 0 || static_cast<size_t>(len) > sizeof(sockaddr_storage))
                continue;
            if (!UsableUpstream(sa, self)) continue;

            Upstream entry;
            std::memcpy(&entry.addr, sa, static_cast<size_t>(len));
            entry.len = len;
            if (entry.addr.ss_family == AF_INET)
                reinterpret_cast<sockaddr_in&>(entry.addr).sin_port = htons(53);
            else
                reinterpret_cast<sockaddr_in6&>(entry.addr).sin6_port = htons(53);

            bool duplicate = false;
            for (const Upstream& known : out)
                if (SameHost(known.addr, entry.addr)) duplicate = true;
            if (!duplicate) out.push_back(entry);
        }
    }
    return out;
}

// ---- In-flight state ---------------------------------------------------------

// A UDP query handed to the upstreams, waiting for one of them to answer.
struct PendingUdp {
    sockaddr_storage client = {};
    int clientLen = 0;
    uint16_t clientId = 0;    // the id to put back before replying
    uint16_t upstreamId = 0;  // the id we asked under, and our key
    uint64_t deadline = 0;
    std::vector<uint8_t> question;  // header + question, to answer with on failure
    Query query;

    // What the client said it can receive, read from its own EDNS0 OPT.
    //
    // Recorded here because the reply is answered much later, from a datagram the
    // client's buffer size no longer appears in. A real upstream truncates to this
    // on its own, so this is a backstop rather than the mechanism — but an
    // upstream that ignores the advertisement, or an answer assembled from several
    // of them, must not be handed to the client oversized.
    size_t udpPayload = kMinUdpPayload;
};

// One TCP client connection, and the upstream connection opened for it if the
// query it carried has to be forwarded. Exactly one query is served at a time; a
// second one that arrives early waits in the reader until the first is answered.
//
// The client-facing half — the socket, its reader, the response owed to it and
// the write cursor into that response — is the shared TcpClientConnection. What
// is added here is the other end of the same conversation: the resolver opens a
// connection of its own to the upstream and watches it in this same loop, so it
// owns that socket, the reader for its replies, and the offset the query has
// been written to.
//
// Both directions are reassembled by the shared reader rather than by a buffer
// this file grows and indexes itself: DNS-over-TCP framing is the same problem on
// the client side and the upstream side, and it is the same problem the forwarder
// has, so it is solved once.
struct TcpSession : TcpClientConnection {
    SOCKET upstream = INVALID_SOCKET;
    bool connecting = false;  // the upstream connect has not completed
    bool busy = false;        // a query is being served and owes a response

    std::vector<uint8_t> upOut;  // length-prefixed query owed to the upstream
    size_t upSent = 0;
    TcpSessionReader upIn;  // reply arriving from the upstream

    std::vector<uint8_t> question;  // header + question, to answer with on failure
    Query query;

    // The base calls this member `socket`. The alias that used to be here — a
    // `SOCKET&` bound to it so the rest of this file could keep saying `client` —
    // is not an option: a reference member deletes the move assignment, and
    // sessions are erased from the middle of a vector. The call sites say
    // `socket` instead.
};

}  // namespace

// Everything the loop owns. Its destructor is the only place sockets are released,
// which is what lets a failed Start() unwind by simply letting the object go.
struct ResolverState {
    SOCKET udp = INVALID_SOCKET;
    SOCKET tcp = INVALID_SOCKET;
    SOCKET upstream4 = INVALID_SOCKET;
    SOCKET upstream6 = INVALID_SOCKET;

    // The endpoint this session bound, so the upstream filter can recognise the
    // server's own address and refuse to forward to it. Kept here rather than read
    // from the object because the loop runs on another thread and this is one of the
    // values it has to be able to read without taking a lock.
    BindEndpoint endpoint;

    std::vector<Upstream> upstreams;
    uint64_t upstreamsAt = 0;

    std::vector<PendingUdp> pending;
    std::vector<TcpSession> sessions;

    uint32_t idState = 0;          // xorshift state for upstream query ids
    std::vector<uint8_t> scratch;  // one datagram at a time

    // The descriptor sets for one pass, kept here rather than in the loop body
    // because they are several hundred bytes and the loop is the hot path this
    // whole design exists to keep cheap.
    WaitSet waits;

    ~ResolverState() {
        for (TcpSession& session : sessions) {
            CloseSocket(session.socket);
            CloseSocket(session.upstream);
        }
        CloseSocket(udp);
        CloseSocket(tcp);
        CloseSocket(upstream4);
        CloseSocket(upstream6);
    }
};

namespace {

void RefreshUpstreams(ResolverState& s) {
    const uint64_t now = Now();
    if (s.upstreamsAt != 0 && now - s.upstreamsAt < kUpstreamRefreshMs) return;
    s.upstreams = DiscoverUpstreams(s.endpoint);
    s.upstreamsAt = now;
}

// An unused transaction id for an upstream query.
//
// The id is what a reply is matched against, so it is drawn from a scrambled
// sequence rather than a counter: a predictable id is an invitation for an
// off-path forgery to land before the real answer does. Source address and
// question are checked as well, on the way in.
uint16_t NextUpstreamId(ResolverState& s) {
    for (int attempt = 0; attempt < 64; ++attempt) {
        s.idState ^= s.idState << 13u;
        s.idState ^= s.idState >> 17u;
        s.idState ^= s.idState << 5u;
        const uint16_t candidate = static_cast<uint16_t>(s.idState);
        bool taken = false;
        for (const PendingUdp& p : s.pending)
            if (p.upstreamId == candidate) taken = true;
        if (!taken) return candidate;
    }
    return static_cast<uint16_t>(s.idState);
}

// The response this resolver would give for `q`, or an empty vector meaning the
// query is not ours to answer and must be forwarded.
std::vector<uint8_t> TryAnswer(const RuleSet& rules, const uint8_t* msg, size_t len,
                               const Query& q) {
    // Only a standard query is interpreted. A response arriving at a listener, an
    // UPDATE or a NOTIFY means something this resolver has no opinion about, and
    // rewriting its header as if it were a lookup would corrupt it.
    if ((q.flags & 0x8000u) != 0) return {};  // QR: already a response
    if (((static_cast<unsigned>(q.flags) >> 11u) & 0xFu) != 0)
        return {};  // opcode other than QUERY
    if (q.qclass != kClassIn) return {};

    const Rule* rule = rules.Match(q.name);
    if (!rule) return {};

    // A Block rule owns every query type; BuildResponse turns it into NXDOMAIN
    // whatever action it is handed, so only a Redirect consults the query type.
    const Action action =
        (rule->action == RuleAction::Block) ? Action::NoData : DecideAction(q.qtype);
    if (action == Action::Forward) return {};
    return BuildResponse(msg, len, q, *rule, action);
}

// ---- UDP ---------------------------------------------------------------------

// The client's own datagram socket, through the shared helper, so the empty
// check and the cast live beside the other one.
void SendUdp(ResolverState& s, const std::vector<uint8_t>& msg, const sockaddr_storage& to,
             int toLen) {
    SendDatagram(s.udp, msg, to, toLen);
}

void ForwardUdp(ResolverState& s, const uint8_t* msg, size_t len, const Query& q,
                const sockaddr_storage& from, int fromLen) {
    RefreshUpstreams(s);

    std::vector<uint8_t> question(msg, msg + q.questionEnd);
    if (s.upstreams.empty() || s.pending.size() >= kMaxPendingUdp) {
        SendUdp(s, BuildStatusResponse(question.data(), question.size(), q, kRcodeServFail),
                from, fromLen);
        return;
    }

    // Relayed verbatim apart from the transaction id, so the client's own EDNS
    // advertisement — and therefore the size at which the upstream will truncate —
    // survives the trip.
    const uint16_t id = NextUpstreamId(s);
    std::vector<uint8_t> outbound(msg, msg + len);
    outbound[0] = static_cast<uint8_t>(id >> 8u);
    outbound[1] = static_cast<uint8_t>(id);

    bool sent = false;
    size_t queried = 0;
    for (const Upstream& upstream : s.upstreams) {
        if (queried >= kUpstreamsQueried) break;
        const SOCKET sock = (upstream.addr.ss_family == AF_INET6) ? s.upstream6 : s.upstream4;
        if (sock == INVALID_SOCKET) continue;
        ++queried;
        if (sendto(sock, reinterpret_cast<const char*>(outbound.data()),
                   static_cast<int>(outbound.size()), 0,
                   reinterpret_cast<const sockaddr*>(&upstream.addr),
                   upstream.len) != SOCKET_ERROR)
            sent = true;
    }
    if (!sent) {
        SendUdp(s, BuildStatusResponse(question.data(), question.size(), q, kRcodeServFail),
                from, fromLen);
        return;
    }

    PendingUdp entry;
    entry.client = from;
    entry.clientLen = fromLen;
    entry.clientId = q.id;
    entry.upstreamId = id;
    entry.deadline = Now() + kForwardTimeoutMs;
    entry.question = std::move(question);
    entry.query = q;
    // Read the client's advertised buffer here, while its own bytes are still in
    // hand. The reply arrives against a datagram that no longer carries it.
    entry.udpPayload = QueryUdpPayloadSize(msg, len);
    s.pending.push_back(std::move(entry));
}

void HandleUdpQuery(ResolverState& s, const RuleSet& rules) {
    sockaddr_storage from = {};
    int fromLen = sizeof(from);
    const int received = recvfrom(s.udp, reinterpret_cast<char*>(s.scratch.data()),
                                  static_cast<int>(s.scratch.size()), 0,
                                  reinterpret_cast<sockaddr*>(&from), &fromLen);
    if (received <= 0) return;

    const size_t len = static_cast<size_t>(received);
    Query q;
    // A message that cannot be read cannot be answered or meaningfully forwarded:
    // there is no question to echo back, so there is nothing to say.
    if (!ParseQuery(s.scratch.data(), len, q)) return;

    std::vector<uint8_t> response = TryAnswer(rules, s.scratch.data(), len, q);
    if (!response.empty()) {
        SendUdp(s, response, from, fromLen);
        return;
    }
    ForwardUdp(s, s.scratch.data(), len, q, from, fromLen);
}

void HandleUpstreamReply(ResolverState& s, SOCKET sock) {
    sockaddr_storage from = {};
    int fromLen = sizeof(from);
    const int received = recvfrom(sock, reinterpret_cast<char*>(s.scratch.data()),
                                  static_cast<int>(s.scratch.size()), 0,
                                  reinterpret_cast<sockaddr*>(&from), &fromLen);
    if (received < 12) return;
    const size_t len = static_cast<size_t>(received);

    // Three things have to agree before a datagram is treated as the answer: it
    // came from a server we actually use, it carries the id we asked under, and it
    // repeats the question we asked. Any one of them alone is forgeable.
    bool fromUpstream = false;
    for (const Upstream& upstream : s.upstreams)
        if (SameHost(upstream.addr, from)) fromUpstream = true;
    if (!fromUpstream) return;

    const uint16_t id =
        static_cast<uint16_t>((static_cast<unsigned>(s.scratch[0]) << 8u) | s.scratch[1]);
    for (auto it = s.pending.begin(); it != s.pending.end(); ++it) {
        if (it->upstreamId != id) continue;
        if (len < it->question.size() ||
            std::memcmp(s.scratch.data() + 12, it->question.data() + 12,
                        it->question.size() - 12) != 0)
            return;

        // Restore the client's transaction id, then check the reply against the
        // size the client said it can take.
        //
        // The upstream was sent the client's own EDNS0 advertisement, so it is
        // expected to have truncated at that size already and this is a no-op.
        // It is here because that expectation is the upstream's to honor and not
        // ours to rely on: one that ignores the advertisement, or answers over a
        // path that reassembles fragments, would otherwise have its oversized
        // reply relayed to a client whose buffer cannot hold it.
        std::vector<uint8_t> response(s.scratch.begin(), s.scratch.begin() + received);
        response[0] = static_cast<uint8_t>(it->clientId >> 8u);
        response[1] = static_cast<uint8_t>(it->clientId);

        if (ApplyUdpBudget(it->question.data(), it->question.size(), response,
                           it->udpPayload) == UdpBudget::CannotFit) {
            // Not even the question fits: the client is owed a failure rather
            // than a datagram it cannot hold.
            response = BuildStatusResponse(it->question.data(), it->question.size(), it->query,
                                           kRcodeServFail);
        }
        if (!response.empty()) {
            sendto(s.udp, reinterpret_cast<const char*>(response.data()),
                   static_cast<int>(response.size()), 0,
                   reinterpret_cast<const sockaddr*>(&it->client), it->clientLen);
        }
        s.pending.erase(it);
        return;
    }
}

// ---- TCP ---------------------------------------------------------------------

// Prefix a response with its 16-bit length and queue it for the client.
//
// The shared member rather than a free function here: framing, the cursor reset
// and the idle deadline are the same three steps the forwarder performs on its
// own sessions, and `TcpClientConnection` is where they live for both. Returns
// false when the message cannot be framed, which for a DNS message means it is
// longer than a 16-bit length can describe.
bool QueueResponse(TcpSession& t, const std::vector<uint8_t>& message) {
    return t.QueueResponse(message);
}

void CloseUpstream(TcpSession& t) {
    CloseSocket(t.upstream);
    t.connecting = false;
    t.upOut.clear();
    t.upSent = 0;
    t.upIn.Clear();
}

// Give up on a forward and tell the client the lookup failed, which is the one
// thing it must not be left wondering about.
void FailTcpForward(TcpSession& t) {
    CloseUpstream(t);
    // A SERVFAIL is a short fixed message, so this cannot fail for length. If it
    // somehow did, the connection would be left with nothing queued — which the
    // loop reads as "idle" and expires on the deadline, rather than as a hang.
    QueueResponse(
        t, BuildStatusResponse(t.question.data(), t.question.size(), t.query, kRcodeServFail));
    t.deadline = Now() + kTcpIdleTimeoutMs;
}

void StartTcpForward(ResolverState& s, TcpSession& t) {
    RefreshUpstreams(s);
    if (s.upstreams.empty()) {
        FailTcpForward(t);
        return;
    }
    // One server, not two: a second connection would double the work to salvage a
    // path that is already the fallback after a truncated UDP reply.
    const Upstream& upstream = s.upstreams.front();
    SOCKET sock = socket(upstream.addr.ss_family, SOCK_STREAM, IPPROTO_TCP);
    if (sock == INVALID_SOCKET) {
        FailTcpForward(t);
        return;
    }
    SetNonBlocking(sock);
    if (connect(sock, reinterpret_cast<const sockaddr*>(&upstream.addr), upstream.len) ==
            SOCKET_ERROR &&
        WSAGetLastError() != WSAEWOULDBLOCK) {
        closesocket(sock);
        FailTcpForward(t);
        return;
    }
    t.upstream = sock;
    t.connecting = true;
    t.deadline = Now() + kForwardTimeoutMs;
}

// Serve the next complete query sitting in the client's reader, if there is one
// and nothing else is in flight. Returns false when the connection is unusable.
bool ServeBufferedQuery(ResolverState& s, TcpSession& t, const RuleSet& rules) {
    if (t.busy || !t.reader.HasMessage()) return true;

    // Taken out of the reader before it is parsed: a message the reader accepted
    // is a whole one, so the framing has already been settled and this cannot
    // desynchronise the stream even if the DNS inside it is nonsense.
    const std::vector<uint8_t> message = t.reader.TakeMessage();

    Query q;
    if (!ParseQuery(message.data(), message.size(), q)) return false;

    t.busy = true;
    std::vector<uint8_t> response = TryAnswer(rules, message.data(), message.size(), q);
    if (!response.empty()) {
        QueueResponse(t, response);
        return true;
    }

    // Forwarded whole, length prefix included, and under the client's own
    // transaction id: one query owns this connection, so there is nothing to
    // demultiplex and no reason to rewrite it. The prefix is re-applied because
    // the reader hands back the payload it framed.
    t.question.assign(message.begin(),
                      message.begin() + static_cast<std::ptrdiff_t>(q.questionEnd));
    t.query = std::move(q);
    t.upOut = EncodeTcpMessage(message);
    if (t.upOut.empty()) return false;
    t.upSent = 0;
    StartTcpForward(s, t);
    return true;
}

bool ReadFromClient(ResolverState& s, TcpSession& t, const RuleSet& rules) {
    char buffer[4096];
    const int received = recv(t.socket, buffer, sizeof(buffer), 0);
    if (received == 0) return false;  // the client closed its half
    if (received < 0) return WSAGetLastError() == WSAEWOULDBLOCK;

    // The reader enforces its own bound and reports a stream it cannot recover
    // from, which for a client connection means the same thing a bad frame does:
    // the connection is closed.
    if (t.reader.Append(reinterpret_cast<const uint8_t*>(buffer),
                        static_cast<size_t>(received)) == TcpSessionReader::State::Broken) {
        return false;
    }
    if (!t.busy) t.deadline = Now() + kTcpIdleTimeoutMs;
    return ServeBufferedQuery(s, t, rules);
}

bool WriteToClient(ResolverState& s, TcpSession& t, const RuleSet& rules) {
    // The shared member, so this and the forwarder cannot disagree about what a
    // partial write means: either byte count can fill a socket buffer midway
    // through a response, and both resume from the offset. Done clears the
    // buffer and the cursor, leaving only this server's own state to reset.
    const WriteResult written = t.Write();
    if (written == WriteResult::WouldBlock) return true;
    if (written == WriteResult::Failed) return false;

    t.busy = false;
    t.Touch();
    // The client may already have pipelined the next one while this was in flight.
    return ServeBufferedQuery(s, t, rules);
}

// Finish connecting, then push the query out. Both halves live here because
// select reports either as "writable".
void WriteToUpstream(TcpSession& t) {
    if (t.connecting) {
        int error = 0;
        int errorLen = sizeof(error);
        if (getsockopt(t.upstream, SOL_SOCKET, SO_ERROR, reinterpret_cast<char*>(&error),
                       &errorLen) == SOCKET_ERROR ||
            error != 0) {
            FailTcpForward(t);
            return;
        }
        t.connecting = false;
    }
    if (WritePending(t.upstream, t.upOut, t.upSent) == WriteResult::Failed) {
        FailTcpForward(t);
    }
}

void ReadFromUpstream(TcpSession& t) {
    char buffer[4096];
    const int received = recv(t.upstream, buffer, sizeof(buffer), 0);
    if (received < 0) {
        if (WSAGetLastError() != WSAEWOULDBLOCK) FailTcpForward(t);
        return;
    }
    if (received == 0) {
        // Closed before a whole reply arrived; there is nothing to relay.
        FailTcpForward(t);
        return;
    }
    if (t.upIn.Append(reinterpret_cast<const uint8_t*>(buffer),
                      static_cast<size_t>(received)) == TcpSessionReader::State::Broken) {
        FailTcpForward(t);
        return;
    }
    if (!t.upIn.HasMessage()) return;

    // Relayed exactly as it came, truncation flag and all: the client asked over
    // TCP, so a reply that does not fit is the upstream's problem to have avoided.
    // The reader strips the prefix; putting it back is what the client expects.
    t.out = EncodeTcpMessage(t.upIn.TakeMessage());
    if (t.out.empty()) {
        FailTcpForward(t);
        return;
    }
    t.outSent = 0;
    CloseUpstream(t);
    t.deadline = Now() + kTcpIdleTimeoutMs;
}

void AcceptTcpClient(ResolverState& s) {
    sockaddr_storage from = {};
    int fromLen = sizeof(from);
    SOCKET client = accept(s.tcp, reinterpret_cast<sockaddr*>(&from), &fromLen);
    if (client == INVALID_SOCKET) return;

    // At the cap this server refuses the newcomer rather than evicting a session.
    // The two servers answer this differently and both are right for themselves;
    // the forwarder's version evicts, and says why there.
    //
    // The reason here: a session's upstream connection lives in this same loop
    // and is watched alongside its client, so one session holds two of the
    // descriptors the fd_set is sized for. Evicting one mid-answer would drop a
    // client that has already been promised a reply from a real server — it
    // would have to notice the silence and retry. A refused newcomer, by
    // contrast, is told immediately and can try again elsewhere.
    if (s.sessions.size() >= kMaxTcpSessions) {
        closesocket(client);
        return;
    }

    if (!PrepareSessionSocket(client)) return;
    TcpSession session;
    session.socket = client;
    session.deadline = Now() + kTcpIdleTimeoutMs;
    s.sessions.push_back(std::move(session));
}

// Everything one connection can do in a single pass. Returns false when it should
// be closed and dropped.
bool ServiceSession(ResolverState& s, TcpSession& t, const WaitSet& waits,
                    const RuleSet& rules) {
    if (t.upstream != INVALID_SOCKET) {
        // A refused connect is reported here rather than as writability.
        if (waits.Failed(t.upstream)) {
            FailTcpForward(t);
        } else if (waits.Writable(t.upstream)) {
            WriteToUpstream(t);
        } else if (waits.Readable(t.upstream)) {
            ReadFromUpstream(t);
        }
    }
    if (!t.out.empty() && waits.Writable(t.socket)) {
        if (!WriteToClient(s, t, rules)) return false;
    } else if (t.out.empty() && waits.Readable(t.socket)) {
        if (!ReadFromClient(s, t, rules)) return false;
    }

    if (Now() < t.deadline) return true;
    // Out of time. A connection that is owed an answer gets one; an idle or stuck
    // one is simply dropped.
    if (t.busy && t.out.empty()) {
        FailTcpForward(t);
        return true;
    }
    return false;
}

void ExpirePendingUdp(ResolverState& s) {
    const uint64_t now = Now();
    for (auto it = s.pending.begin(); it != s.pending.end();) {
        if (now < it->deadline) {
            ++it;
            continue;
        }
        SendUdp(s,
                BuildStatusResponse(it->question.data(), it->question.size(), it->query,
                                    kRcodeServFail),
                it->client, it->clientLen);
        it = s.pending.erase(it);
    }
}

}  // namespace

// ---- LocalResolver -----------------------------------------------------------

LocalResolver::LocalResolver()
    : m_activeRules(std::make_shared<const RuleSet>()),
      m_stopped(CreateEventW(nullptr, TRUE, TRUE, nullptr)) {
    if (!m_stopped)
        LOGE(L"Resolver: cannot create the loop's state event (err " +
             std::to_wstring(GetLastError()) + L"); the server cannot be started.");
}

LocalResolver::~LocalResolver() {
    Stop();
    if (m_stopped) CloseHandle(m_stopped);
}

void LocalResolver::Publish(std::shared_ptr<const RuleSet> rules) {
    std::lock_guard<std::mutex> lock(m_mx);
    m_activeRules = std::move(rules);
}

void LocalResolver::SetEndpoint(const BindEndpoint& endpoint) {
    std::lock_guard<std::mutex> lock(m_mx);
    m_endpoint = endpoint;
}

BindEndpoint LocalResolver::Endpoint() const {
    std::lock_guard<std::mutex> lock(m_mx);
    return m_endpoint;
}

std::shared_ptr<const RuleSet> LocalResolver::ActiveRules() const {
    std::lock_guard<std::mutex> lock(m_mx);
    return m_activeRules;
}

bool LocalResolver::Start() {
    // A previous session may have ended on its own, leaving a joinable thread and
    // closed sockets behind. Tearing that down first means Start always begins from
    // a clean state and can never stack a second loop on the first.
    Stop();

    // Without it there is no way to publish that the loop has ended, and a server
    // whose death cannot be observed is exactly what this endpoint must never be.
    if (!m_stopped) {
        LOGE(L"Resolver: no state event; refusing to start a server nobody can watch.");
        return false;
    }

    if (!SocketRuntime::Ensure()) {
        LOGE(L"Resolver: winsock could not be initialized.");
        return false;
    }

    // Read once, so every half of this session binds and filters against one
    // endpoint even if another thread calls SetEndpoint midway through.
    const BindEndpoint endpoint = Endpoint();

    // Held locally until everything has succeeded: on any failure the state object
    // goes out of scope and its destructor closes whatever was already opened.
    auto state = std::make_unique<ResolverState>();
    state->endpoint = endpoint;

    state->udp = BindUdpListener(endpoint);
    if (state->udp == INVALID_SOCKET) {
        LOGE(std::wstring(L"Resolver: cannot bind UDP ") + endpoint.Text() + L" (err " +
             std::to_wstring(WSAGetLastError()) + L").");
        return false;
    }

    // The shared binder already turned off ICMP-port-unreachable poisoning, which
    // is what a forwarder sharing one socket across several upstreams needs.
    state->tcp = BindTcpListener(endpoint);
    if (state->tcp == INVALID_SOCKET) {
        LOGE(std::wstring(L"Resolver: cannot listen on TCP ") + endpoint.Text() + L" (err " +
             std::to_wstring(WSAGetLastError()) + L").");
        return false;
    }

    // Both families are opened up front; a machine with no IPv6 resolver simply
    // never uses the second one. Only losing both leaves nothing to forward with.
    state->upstream4 = MakeUpstreamSocket(AF_INET);
    state->upstream6 = MakeUpstreamSocket(AF_INET6);
    if (state->upstream4 == INVALID_SOCKET && state->upstream6 == INVALID_SOCKET) {
        LOGE(L"Resolver: cannot open a socket for forwarding (err " +
             std::to_wstring(WSAGetLastError()) + L").");
        return false;
    }

    state->scratch.resize(kMaxMessage);
    // Any seed will do as long as it is not zero, which xorshift cannot leave.
    state->idState = static_cast<uint32_t>(Now()) | 1u;

    m_state = std::move(state);
    m_running.store(true);

    // Cleared before the thread exists, so the loop can only ever set it again —
    // there is no ordering in which a real "the loop ended" signal is cleared by the
    // start that came before it.
    ResetEvent(m_stopped);

    // The one operation here that allocates a kernel object without returning a code
    // for it. Letting it throw would unwind out of Start, out of the service start
    // above it, and into a detached tray worker with no handler — a process killed
    // for being out of memory, with the policy rule still installed.
    try {
        m_thread = std::thread([this] { Loop(); });
    } catch (const std::system_error& e) {
        LOGE(L"Resolver: cannot create the server thread (" + Utf8ToWide(e.what()) + L").");
        m_running.store(false);
        SetEvent(m_stopped);
        m_state.reset();  // closes every socket
        return false;
    }

    LOGI(std::wstring(L"Resolver: listening on ") + endpoint.Text() + L".");
    return true;
}

void LocalResolver::Stop() {
    m_running.store(false);
    if (m_thread.joinable()) m_thread.join();
    m_state.reset();  // closes every socket
}

void LocalResolver::Loop() {
    ResolverState& s = *m_state;

    while (m_running.load()) {
        // One snapshot per pass, so every query answered in it sees the same rules
        // even if a hot-reload publishes a new set halfway through.
        const std::shared_ptr<const RuleSet> rules = ActiveRules();

        // One pass of the shared select skeleton. Which socket goes in which set
        // is the part that differs between the two servers and stays here.
        s.waits.Reset();
        s.waits.WatchRead(s.udp);
        s.waits.WatchRead(s.tcp);
        s.waits.WatchRead(s.upstream4);
        s.waits.WatchRead(s.upstream6);
        for (const TcpSession& session : s.sessions) {
            if (session.out.empty())
                s.waits.WatchRead(session.socket);
            else
                s.waits.WatchWrite(session.socket);
            if (session.upstream != INVALID_SOCKET) {
                if (session.connecting || session.upSent < session.upOut.size()) {
                    s.waits.WatchWrite(session.upstream);
                    s.waits.WatchFailed(session.upstream);
                } else {
                    s.waits.WatchRead(session.upstream);
                }
            }
        }

        if (!s.waits.Wait()) {
            LOGE(L"Resolver: select failed (err " + std::to_wstring(WSAGetLastError()) +
                 L"); the local DNS server is stopping.");
            break;
        }

        if (!s.waits.TimedOut()) {
            // Replies first: they retire pending entries, so a burst of forwards
            // cannot push one past its deadline while its answer sits unread.
            if (s.waits.Readable(s.upstream4)) HandleUpstreamReply(s, s.upstream4);
            if (s.waits.Readable(s.upstream6)) HandleUpstreamReply(s, s.upstream6);
            if (s.waits.Readable(s.udp)) HandleUdpQuery(s, *rules);
            if (s.waits.Readable(s.tcp)) AcceptTcpClient(s);
        }

        for (size_t i = 0; i < s.sessions.size();) {
            if (ServiceSession(s, s.sessions[i], s.waits, *rules)) {
                ++i;
                continue;
            }
            CloseSocket(s.sessions[i].socket);
            CloseSocket(s.sessions[i].upstream);
            s.sessions.erase(s.sessions.begin() + static_cast<ptrdiff_t>(i));
        }

        ExpirePendingUdp(s);
    }

    // The loop can end without anyone asking — a failed select is the only way, but
    // it is a way. Clearing the flag here is what keeps "is the resolver running" an
    // honest answer rather than a memory of having started it, and signalling the
    // event is what turns that from something someone has to think to ask into
    // something a waiter is told. Set last, after the flag, so anyone woken by it
    // reads a state that is already consistent.
    m_running.store(false);
    SetEvent(m_stopped);
}

}  // namespace Dns
