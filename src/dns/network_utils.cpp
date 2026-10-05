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

#include "dns/network_utils.h"

#include <ws2tcpip.h>

#include <windows.h>

#include <algorithm>
#include <cstring>
#include <limits>

#include "dns/socket_utils.h"

namespace Dns {
namespace NetworkUtils {
namespace {

using SocketUtils::Now;
using SocketUtils::WaitResult;

// The shared wait, narrowed to what these calls need: true means the socket is
// in the requested state, and everything else — timeout, cancellation, a failed
// select — ends the operation. Every caller here retries only on "would block",
// so a timeout and an abort have the same answer: give up.
bool WaitReady(SOCKET sock, bool readable, bool writable, uint64_t deadline,
               const CancelToken* cancel) {
    return SocketUtils::WaitFor(sock, readable, writable, deadline, cancel) ==
           WaitResult::Ready;
}

}  // namespace

bool ParseIpEndpoint(const std::string& text, uint16_t defaultPort, IpEndpoint& endpoint) {
    if (text.empty() || defaultPort == 0) return false;

    std::string host;
    std::string portText;

    if (text.front() == '[') {
        const size_t close = text.find(']');
        if (close == std::string::npos || close == 1) return false;
        host = text.substr(1, close - 1);
        if (close + 1 < text.size()) {
            if (text[close + 1] != ':' || close + 2 == text.size()) return false;
            portText = text.substr(close + 2);
        }
    } else {
        const size_t firstColon = text.find(':');
        const size_t lastColon = text.rfind(':');
        if (firstColon == std::string::npos || firstColon != lastColon) {
            // No colon at all, or more than one: an unbracketed IPv6 literal. A
            // port would be ambiguous here and therefore requires brackets.
            host = text;
        } else {
            if (firstColon == 0 || firstColon + 1 == text.size()) return false;
            host = text.substr(0, firstColon);
            portText = text.substr(firstColon + 1);
        }
    }

    uint16_t port = defaultPort;
    if (!portText.empty()) {
        uint32_t parsed = 0;
        for (const char c : portText) {
            if (c < '0' || c > '9') return false;
            parsed = parsed * 10 + static_cast<uint32_t>(c - '0');
            if (parsed > 65535) return false;
        }
        if (parsed == 0) return false;
        port = static_cast<uint16_t>(parsed);
    }

    sockaddr_in ipv4 = {};
    ipv4.sin_family = AF_INET;
    ipv4.sin_port = htons(port);
    if (InetPtonA(AF_INET, host.c_str(), &ipv4.sin_addr) == 1) {
        endpoint.address = {};
        std::memcpy(&endpoint.address, &ipv4, sizeof(ipv4));
        endpoint.length = sizeof(ipv4);
        endpoint.host = host;
        return true;
    }

    sockaddr_in6 ipv6 = {};
    ipv6.sin6_family = AF_INET6;
    ipv6.sin6_port = htons(port);
    if (InetPtonA(AF_INET6, host.c_str(), &ipv6.sin6_addr) == 1) {
        endpoint.address = {};
        std::memcpy(&endpoint.address, &ipv6, sizeof(ipv6));
        endpoint.length = sizeof(ipv6);
        endpoint.host = host;
        return true;
    }

    return false;
}

bool ConnectWithTimeout(SOCKET sock, const sockaddr* addr, int addrLen, uint32_t timeoutMs,
                        const CancelToken* cancel) {
    if (cancel && cancel->Cancelled()) return false;

    u_long mode = 1;
    if (ioctlsocket(sock, FIONBIO, &mode) != 0) return false;

    if (connect(sock, addr, addrLen) == 0) return true;
    if (WSAGetLastError() != WSAEWOULDBLOCK) return false;

    const uint64_t deadline = Now() + timeoutMs;
    if (!WaitReady(sock, false, true, deadline, cancel)) return false;

    // A connect that completes is reported as writability; the result is in the
    // socket's pending error, which select() cannot see.
    int error = 0;
    int len = sizeof(error);
    if (getsockopt(sock, SOL_SOCKET, SO_ERROR, reinterpret_cast<char*>(&error), &len) != 0) {
        return false;
    }
    return error == 0;
}

bool SendAll(SOCKET sock, const std::vector<uint8_t>& data, uint32_t timeoutMs,
             const CancelToken* cancel) {
    return SendAll(sock, data.data(), data.size(), timeoutMs, cancel);
}

bool SendAll(SOCKET sock, const uint8_t* data, size_t len, uint32_t timeoutMs,
             const CancelToken* cancel) {
    if (data == nullptr && len != 0) return false;

    const uint64_t deadline = Now() + timeoutMs;
    size_t sent = 0;

    while (sent < len) {
        if (!WaitReady(sock, false, true, deadline, cancel)) return false;

        const size_t remaining = len - sent;
        const int chunk = static_cast<int>(
            std::min(remaining, static_cast<size_t>(std::numeric_limits<int>::max())));
        const int n = send(sock, reinterpret_cast<const char*>(data + sent), chunk, 0);
        if (n <= 0) {
            // Only "would block" is worth retrying; anything else is the socket
            // failing, which the deadline cannot improve.
            if (n == SOCKET_ERROR && WSAGetLastError() == WSAEWOULDBLOCK) continue;
            return false;
        }
        sent += static_cast<size_t>(n);
    }

    return true;
}

bool SendUdp(SOCKET sock, const std::vector<uint8_t>& data, const sockaddr* addr, int addrLen,
             uint32_t timeoutMs, const CancelToken* cancel) {
    const uint64_t deadline = Now() + timeoutMs;

    for (;;) {
        if (!WaitReady(sock, false, true, deadline, cancel)) return false;

        const int n = sendto(sock, reinterpret_cast<const char*>(data.data()),
                             static_cast<int>(data.size()), 0, addr, addrLen);
        if (n > 0) return true;
        if (n == SOCKET_ERROR && WSAGetLastError() == WSAEWOULDBLOCK) continue;
        return false;
    }
}

std::vector<uint8_t> RecvUdp(SOCKET sock, uint32_t timeoutMs, const CancelToken* cancel) {
    const uint64_t deadline = Now() + timeoutMs;

    for (;;) {
        if (!WaitReady(sock, true, false, deadline, cancel)) return {};

        // Sized for the largest datagram a DNS response can be, rather than the
        // 512 bytes that used to truncate every reply carrying more records than
        // the bare question fit in.
        std::vector<uint8_t> buffer(SocketUtils::kMaxMessage);
        const int n = recvfrom(sock, reinterpret_cast<char*>(buffer.data()),
                               static_cast<int>(buffer.size()), 0, nullptr, nullptr);
        if (n < 0) {
            if (WSAGetLastError() == WSAEWOULDBLOCK) continue;
            return {};
        }
        if (n == 0) return {};
        buffer.resize(static_cast<size_t>(n));
        return buffer;
    }
}

bool RecvExact(SOCKET sock, uint8_t* buffer, size_t len, uint32_t timeoutMs,
               const CancelToken* cancel) {
    const uint64_t deadline = Now() + timeoutMs;
    size_t received = 0;

    while (received < len) {
        if (!WaitReady(sock, true, false, deadline, cancel)) return false;

        const int n = recv(sock, reinterpret_cast<char*>(buffer + received),
                           static_cast<int>(len - received), 0);
        if (n <= 0) {
            if (n == SOCKET_ERROR && WSAGetLastError() == WSAEWOULDBLOCK) continue;
            return false;
        }
        received += static_cast<size_t>(n);
    }

    return true;
}

std::vector<uint8_t> RecvLengthPrefixed(SOCKET sock, uint32_t timeoutMs,
                                        const CancelToken* cancel) {
    uint8_t lenBuf[2];
    if (!RecvExact(sock, lenBuf, 2, timeoutMs, cancel)) return {};

    const uint16_t msgLen = (static_cast<unsigned>(lenBuf[0]) << 8u) | lenBuf[1];
    // A zero length is not a DNS message, and the prefix cannot express more
    // than kMaxMessage, so only the degenerate case needs rejecting.
    if (msgLen == 0) return {};

    std::vector<uint8_t> message(msgLen);
    if (!RecvExact(sock, message.data(), msgLen, timeoutMs, cancel)) return {};

    return message;
}

}  // namespace NetworkUtils
}  // namespace Dns
