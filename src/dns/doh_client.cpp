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

#include "dns/doh_client.h"

#include <winsock2.h>
#include <ws2tcpip.h>

#include <windows.h>

#include <sstream>
#include <string>

#include "app/logging.h"
#include "app/text.h"
#include "dns/http_response.h"
#include "dns/network_utils.h"
#include "dns/socket_utils.h"
#include "dns/tls_utils.h"

namespace Dns {
namespace {

using SocketUtils::SocketHandle;

// Build HTTP request
std::vector<uint8_t> BuildHttpRequest(const std::string& host, const std::string& path,
                                      const std::vector<uint8_t>& body) {
    std::ostringstream request;
    request << "POST " << path << " HTTP/1.1\r\n";
    request << "Host: " << host << "\r\n";
    request << "Content-Type: application/dns-message\r\n";
    request << "Accept: application/dns-message\r\n";
    request << "Content-Length: " << body.size() << "\r\n";
    request << "Connection: close\r\n";
    request << "\r\n";

    const std::string header = request.str();
    std::vector<uint8_t> result(header.begin(), header.end());
    result.insert(result.end(), body.begin(), body.end());

    return result;
}

// The HTTP response as a sink for the shared TLS receive driver.
//
// The driver stops on the first Feed that returns false and calls Finish on a
// stream that ended early, which is exactly the two-entry interface this parser
// already had — so the adaptation is a rename and nothing else.
struct HttpSink {
    HttpResponseParser parser;

    bool Feed(const uint8_t* data, size_t len) {
        return parser.Feed(data, len) == HttpResponseParser::Result::NeedMore;
    }

    void Finish() { parser.Finish(); }
};

}  // namespace

std::vector<uint8_t> QueryDoH(const std::vector<uint8_t>& query, const std::string& address,
                              const std::string& hostname, const std::string& path,
                              const std::vector<std::vector<uint8_t>>& certificateHashes,
                              uint32_t timeoutMs, const CancelToken* cancel) {
    if (query.empty()) return {};

    NetworkUtils::IpEndpoint endpoint;
    if (!NetworkUtils::ParseIpEndpoint(address, 443, endpoint)) {
        LOGW(L"DoH: invalid IP endpoint: " + Utf8ToWide(address));
        return {};
    }

    SocketHandle sock(socket(endpoint.address.ss_family, SOCK_STREAM, IPPROTO_TCP));
    if (!sock.IsValid()) return {};

    if (!NetworkUtils::ConnectWithTimeout(sock,
                                          reinterpret_cast<const sockaddr*>(&endpoint.address),
                                          endpoint.length, timeoutMs / 2, cancel)) {
        return {};
    }

    // Cancellation closes sockets it owns, so the socket has to be registered
    // before the first call that can block on it. From here on the token is a
    // second owner, and the handle remembers it: every return path below closes
    // the socket exactly once, whichever of the two gets there first.
    if (!sock.RegisterWith(cancel)) return {};

    // No SNI when the stamp carries no hostname. Falling back to the endpoint
    // address would send an IP literal as the server name, which RFC 6066 §3
    // forbids and which no certificate can match; the earlier behaviour then
    // waited for the peer to reject it. An empty hostname is legal in a stamp,
    // so the refusal belongs on this side and it has to be visible.
    if (hostname.empty()) {
        LOGW(L"DoH: no hostname in the stamp, so no SNI can be sent for " +
             Utf8ToWide(address) + L"; refusing to connect");
        return {};
    }

    TlsUtils::CredHandle credHandle;
    TlsUtils::CtxtHandle ctxtHandle;

    if (!TlsUtils::Handshake(sock, Utf8ToWide(hostname), ctxtHandle, credHandle,
                             certificateHashes, timeoutMs / 2, cancel)) {
        return {};
    }

    const std::string& hostHeader = hostname;
    const std::vector<uint8_t> httpRequest = BuildHttpRequest(hostHeader, path, query);

    if (!TlsUtils::Send(sock, ctxtHandle.Get(), httpRequest, timeoutMs / 2, cancel)) {
        return {};
    }

    // Read until the body is framed completely, rather than stopping at the
    // first TLS record: a response whose body spans records would otherwise be
    // truncated at the boundary, and a DNS message cut in half parses as a
    // corrupt one instead of failing outright.
    //
    // The driver owns the deadline; the sink owns what "the whole response" is.
    HttpSink sink;
    TlsUtils::RecvUntil(sock, ctxtHandle, SocketUtils::Now() + timeoutMs, cancel, sink);

    if (sink.parser.result() != HttpResponseParser::Result::Complete) return {};
    return sink.parser.body();
}

}  // namespace Dns
