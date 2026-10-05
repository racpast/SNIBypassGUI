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

#include "dns/dot_client.h"

#include <winsock2.h>
#include <ws2tcpip.h>

#include <windows.h>

#include <cstring>

#include "app/logging.h"
#include "app/text.h"
#include "dns/network_utils.h"
#include "dns/socket_utils.h"
#include "dns/tcp_session.h"
#include "dns/tls_utils.h"

namespace Dns {
namespace {

// DoT's framing as a sink for the shared TLS receive driver: hand the reader
// each decrypted record and stop the moment it holds a whole length-prefixed
// message.
//
// The reader is a member rather than a local because the driver calls Finish()
// on a stream that ended early, and the message has to be taken out afterwards
// either way.
struct DotSink {
    TcpSessionReader reader;

    bool Feed(const uint8_t* data, size_t len) {
        reader.Append(data, len);
        return !reader.HasMessage();
    }

    void Finish() {
        // Nothing to settle: the reader already reports what it has, and a
        // message that arrived whole is taken below whether or not this ran.
    }
};

}  // namespace

std::vector<uint8_t> QueryDoT(const std::vector<uint8_t>& query, const std::string& address,
                              const std::string& hostname,
                              const std::vector<std::vector<uint8_t>>& certificateHashes,
                              uint32_t timeoutMs, const CancelToken* cancel) {
    if (query.empty() || query.size() > SocketUtils::kMaxMessage) return {};

    NetworkUtils::IpEndpoint endpoint;
    if (!NetworkUtils::ParseIpEndpoint(address, 853, endpoint)) {
        LOGW(L"DoT: invalid IP endpoint: " + Utf8ToWide(address));
        return {};
    }

    SocketUtils::SocketHandle sock(
        socket(endpoint.address.ss_family, SOCK_STREAM, IPPROTO_TCP));
    if (!sock.IsValid()) return {};

    if (!NetworkUtils::ConnectWithTimeout(sock,
                                          reinterpret_cast<const sockaddr*>(&endpoint.address),
                                          endpoint.length, timeoutMs / 2, cancel)) {
        return {};
    }

    // Registered before the handshake, because the handshake is the longest
    // blocking step here and the one most worth abandoning. The handle remembers
    // the token, so the socket is closed exactly once by whichever of the two
    // gets there first.
    if (!sock.RegisterWith(cancel)) return {};

    // No SNI when the stamp carries no hostname. Falling back to the endpoint
    // address would send an IP literal as the server name, which RFC 6066 §3
    // forbids and which no certificate can match; the earlier behaviour then
    // waited for the peer to reject it. An empty hostname is legal in a stamp,
    // so the refusal belongs on this side and it has to be visible.
    if (hostname.empty()) {
        LOGW(L"DoT: no hostname in the stamp, so no SNI can be sent for " +
             Utf8ToWide(address) + L"; refusing to connect");
        return {};
    }

    TlsUtils::CredHandle credHandle;
    TlsUtils::CtxtHandle ctxtHandle;

    if (!TlsUtils::Handshake(sock, Utf8ToWide(hostname), ctxtHandle, credHandle,
                             certificateHashes, timeoutMs / 2, cancel)) {
        return {};
    }

    if (!TlsUtils::Send(sock, ctxtHandle.Get(), EncodeTcpMessage(query), timeoutMs / 2,
                        cancel)) {
        return {};
    }

    // DoT framing is length-prefixed, so the whole answer is one message and
    // the shared reader can reassemble it across TLS records. The driver owns
    // the deadline arithmetic; this only says what a complete answer looks like.
    DotSink sink;
    TlsUtils::RecvUntil(sock, ctxtHandle, SocketUtils::Now() + timeoutMs, cancel, sink);
    return sink.reader.TakeMessage();
}

}  // namespace Dns
