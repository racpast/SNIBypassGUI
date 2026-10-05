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

#include "dns/plain_client.h"

#include <winsock2.h>
#include <ws2tcpip.h>

#include <windows.h>

#include "app/logging.h"
#include "app/text.h"
#include "dns/network_utils.h"
#include "dns/socket_utils.h"
#include "dns/tcp_session.h"

namespace Dns {
namespace {

// Ask once over UDP and return whatever datagram came back.
//
// The socket is fresh per attempt rather than pooled: a query is abandoned the
// moment a faster upstream wins the race, and a socket shared between queries
// would have to be sure no other query's reply was still arriving on it.
std::vector<uint8_t> QueryUdp(const std::vector<uint8_t>& query,
                              const NetworkUtils::IpEndpoint& endpoint, uint32_t timeoutMs,
                              const CancelToken* cancel) {
    SocketUtils::SocketHandle sock(socket(endpoint.address.ss_family, SOCK_DGRAM, IPPROTO_UDP));
    if (!sock.IsValid()) return {};

    // An ICMP port-unreachable from an earlier query must not fail this one's
    // read: the server being down is a thing to wait out, not a sticky error.
    SocketUtils::DisableUdpConnReset(sock);

    if (!sock.RegisterWith(cancel)) return {};

    const sockaddr* addr = reinterpret_cast<const sockaddr*>(&endpoint.address);
    if (!NetworkUtils::SendUdp(sock, query, addr, endpoint.length, timeoutMs, cancel))
        return {};

    return NetworkUtils::RecvUdp(sock, timeoutMs, cancel);
}

// Ask the same question again over TCP, which is what TC=1 asks for.
//
// Deliberately does not set a different timeout budget of its own: the retry is
// part of answering this one query, so it shares the deadline the caller gave.
std::vector<uint8_t> QueryTcp(const std::vector<uint8_t>& query,
                              const NetworkUtils::IpEndpoint& endpoint, uint32_t timeoutMs,
                              const CancelToken* cancel) {
    SocketUtils::SocketHandle sock(
        socket(endpoint.address.ss_family, SOCK_STREAM, IPPROTO_TCP));
    if (!sock.IsValid()) return {};

    if (!sock.RegisterWith(cancel)) return {};

    if (!NetworkUtils::ConnectWithTimeout(sock,
                                          reinterpret_cast<const sockaddr*>(&endpoint.address),
                                          endpoint.length, timeoutMs, cancel)) {
        return {};
    }

    // Framed with the same 16-bit length prefix every DNS-over-TCP exchange uses,
    // through the same helper the other transports do: there is no separate
    // plain-DNS framing to get wrong.
    if (!NetworkUtils::SendAll(sock, EncodeTcpMessage(query), timeoutMs, cancel)) return {};

    return NetworkUtils::RecvLengthPrefixed(sock, timeoutMs, cancel);
}

}  // namespace

std::vector<uint8_t> QueryPlainDns(const std::vector<uint8_t>& query,
                                   const std::string& address, uint32_t timeoutMs,
                                   const CancelToken* cancel) {
    if (query.empty() || query.size() > SocketUtils::kMaxMessage) return {};

    NetworkUtils::IpEndpoint endpoint;
    if (!NetworkUtils::ParseIpEndpoint(address, 53, endpoint)) {
        LOGW(L"Plain DNS: invalid IP endpoint: " + Utf8ToWide(address));
        return {};
    }

    // Split so the escalation does not double the caller's wait: most queries are
    // answered by the first datagram, and a truncated one still has to leave room
    // for the TCP exchange that completes it.
    const uint32_t udpBudget = timeoutMs / 2;
    const uint32_t tcpBudget = timeoutMs - udpBudget;

    std::vector<uint8_t> overUdp = QueryUdp(query, endpoint, udpBudget, cancel);
    if (overUdp.empty()) return {};

    // The whole point of the transport. TC=1 means the server had more to say
    // than one datagram could carry, and the only way to hear it is to ask again
    // over a stream. Returning the truncated answer instead would hand the caller
    // an incomplete response it has no way to complete.
    if (!IsTruncated(overUdp.data(), overUdp.size())) return overUdp;

    LOGI(L"Plain DNS: response from " + Utf8ToWide(address) +
         L" was truncated, retrying over TCP");

    return QueryTcp(query, endpoint, tcpBudget, cancel);
}

}  // namespace Dns
