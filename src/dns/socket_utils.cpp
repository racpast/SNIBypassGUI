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

#include "dns/socket_utils.h"

// SIO_UDP_CONNRESET, which is a Microsoft extension and so lives beside the
// Microsoft extension functions rather than in the portable socket headers.
#include <mswsock.h>
#include <ws2tcpip.h>

#include <cstring>
#include <string>

namespace Dns {
namespace SocketUtils {

uint64_t Now() {
    return GetTickCount64();
}

void SetNonBlocking(SOCKET s) {
    u_long mode = 1;
    ioctlsocket(s, FIONBIO, &mode);
}

void DisableUdpConnReset(SOCKET s) {
    BOOL off = FALSE;
    DWORD returned = 0;
    WSAIoctl(s, SIO_UDP_CONNRESET, &off, sizeof(off), nullptr, 0, &returned, nullptr, nullptr);
}

void CloseSocket(SOCKET& s) {
    if (s != INVALID_SOCKET) {
        closesocket(s);
        s = INVALID_SOCKET;
    }
}

void WaitSet::Reset() {
    FD_ZERO(&m_readable);
    FD_ZERO(&m_writable);
    FD_ZERO(&m_failed);
    m_ready = 0;
}

void WaitSet::WatchRead(SOCKET s) {
    if (s != INVALID_SOCKET) FD_SET(s, &m_readable);
}

void WaitSet::WatchWrite(SOCKET s) {
    if (s != INVALID_SOCKET) FD_SET(s, &m_writable);
}

void WaitSet::WatchFailed(SOCKET s) {
    if (s != INVALID_SOCKET) FD_SET(s, &m_failed);
}

bool WaitSet::Wait() {
    const timeval timeout = TickTimeout();
    m_ready = select(0, &m_readable, &m_writable, &m_failed, &timeout);
    return m_ready != SOCKET_ERROR;
}

WaitResult WaitFor(SOCKET sock, bool readable, bool writable, uint64_t deadline,
                   const CancelToken* cancel) {
    for (;;) {
        if (cancel != nullptr && cancel->Cancelled()) return WaitResult::Aborted;

        const uint64_t now = Now();
        if (now >= deadline) return WaitResult::TimedOut;

        const uint64_t remaining = deadline - now;
        const uint64_t sliceMs = (remaining < kWaitPollMs) ? remaining : kWaitPollMs;

        fd_set readfds;
        fd_set writefds;
        FD_ZERO(&readfds);
        FD_ZERO(&writefds);
        if (readable) FD_SET(sock, &readfds);
        if (writable) FD_SET(sock, &writefds);

        timeval timeout = {};
        timeout.tv_sec = static_cast<long>(sliceMs / 1000);
        timeout.tv_usec = static_cast<long>((sliceMs % 1000) * 1000);

        const int ready = select(0, readable ? &readfds : nullptr,
                                 writable ? &writefds : nullptr, nullptr, &timeout);
        if (ready == SOCKET_ERROR) return WaitResult::Aborted;
        if (ready > 0) return WaitResult::Ready;
    }
}

bool PrepareSessionSocket(SOCKET client) {
    // Non-blocking first: select() reports a socket readable or writable and the
    // contract is that the read or write that follows cannot sit and block the
    // whole loop. A socket that could not be switched is closed here rather than
    // handed on, because a blocking one in this list is a stalled server.
    u_long mode = 1;
    if (ioctlsocket(client, FIONBIO, &mode) != 0) {
        closesocket(client);
        return false;
    }
    return true;
}

SOCKET BindListener(const wchar_t* address, uint16_t port, int type, int protocol) {
    // The family is taken from the literal rather than assumed, in the order a
    // sentence like "127.0.0.1" or "::1" is unambiguous in: a dotted quad is IPv4
    // and anything else that parses is IPv6. Both are needed because a listener's
    // address is configuration — the resolver's endpoint comes from the payload —
    // and a configured value the parser accepted must not then fail at bind time on
    // a family this never tried.
    sockaddr_storage storage = {};
    int addrLen = 0;
    in_addr v4 = {};
    in6_addr v6 = {};
    if (InetPtonW(AF_INET, address, &v4) == 1) {
        auto& addr = reinterpret_cast<sockaddr_in&>(storage);
        addr.sin_family = AF_INET;
        addr.sin_port = htons(port);
        addr.sin_addr = v4;
        addrLen = sizeof(sockaddr_in);
    } else if (InetPtonW(AF_INET6, address, &v6) == 1) {
        auto& addr = reinterpret_cast<sockaddr_in6&>(storage);
        addr.sin6_family = AF_INET6;
        addr.sin6_port = htons(port);
        addr.sin6_addr = v6;
        addrLen = sizeof(sockaddr_in6);
    } else {
        // Not a literal at all. Refused rather than resolved: a name here would be
        // resolved through the DNS this program is in the middle of serving.
        WSASetLastError(WSAEINVAL);
        return INVALID_SOCKET;
    }

    SOCKET s = socket(storage.ss_family, type, protocol);
    if (s == INVALID_SOCKET) return INVALID_SOCKET;

    // SO_EXCLUSIVEADDRUSE is the whole point of binding here rather than with a
    // plain bind: without it another program can later take the same address with
    // SO_REUSEADDR and be handed the queries meant for us. A failure means that
    // guarantee is not in place, so the listener is refused rather than opened
    // with the protection silently missing.
    BOOL exclusive = TRUE;
    if (setsockopt(s, SOL_SOCKET, SO_EXCLUSIVEADDRUSE,
                   reinterpret_cast<const char*>(&exclusive),
                   sizeof(exclusive)) == SOCKET_ERROR) {
        // Saved and restored because closesocket is allowed to overwrite the
        // error, and the caller logs the one that made this fail.
        const int err = WSAGetLastError();
        closesocket(s);
        WSASetLastError(err);
        return INVALID_SOCKET;
    }

    if (bind(s, reinterpret_cast<const sockaddr*>(&storage), addrLen) == SOCKET_ERROR) {
        const int err = WSAGetLastError();
        closesocket(s);
        WSASetLastError(err);
        return INVALID_SOCKET;
    }
    SetNonBlocking(s);
    return s;
}

bool SameHost(const sockaddr_storage& a, const sockaddr_storage& b) {
    if (a.ss_family != b.ss_family) return false;
    if (a.ss_family == AF_INET) {
        return std::memcmp(&reinterpret_cast<const sockaddr_in&>(a).sin_addr,
                           &reinterpret_cast<const sockaddr_in&>(b).sin_addr,
                           sizeof(in_addr)) == 0;
    }
    if (a.ss_family == AF_INET6) {
        return std::memcmp(&reinterpret_cast<const sockaddr_in6&>(a).sin6_addr,
                           &reinterpret_cast<const sockaddr_in6&>(b).sin6_addr,
                           sizeof(in6_addr)) == 0;
    }
    return false;
}

std::wstring AddressText(const sockaddr_storage& addr, int, bool withPort) {
    wchar_t host[INET6_ADDRSTRLEN] = {};

    // InetNtopW rather than getnameinfo because only the literal is wanted: a
    // name here would be the logging path doing its own DNS lookup, and that
    // lookup would go to the very server that may just have failed.
    if (addr.ss_family == AF_INET) {
        const in_addr& v4 = reinterpret_cast<const sockaddr_in&>(addr).sin_addr;
        if (InetNtopW(AF_INET, &v4, host, INET6_ADDRSTRLEN) == nullptr) return L"?";
    } else if (addr.ss_family == AF_INET6) {
        const in6_addr& v6 = reinterpret_cast<const sockaddr_in6&>(addr).sin6_addr;
        if (InetNtopW(AF_INET6, &v6, host, INET6_ADDRSTRLEN) == nullptr) return L"?";
    } else {
        return L"?";
    }

    if (!withPort) return host;

    uint16_t port = 0;
    if (addr.ss_family == AF_INET) {
        port = ntohs(reinterpret_cast<const sockaddr_in&>(addr).sin_port);
    } else {
        port = ntohs(reinterpret_cast<const sockaddr_in6&>(addr).sin6_port);
    }

    // An IPv6 literal goes in brackets when a port is attached, which is the only
    // way the two are not read as one colon-separated field.
    if (addr.ss_family == AF_INET6) {
        return std::wstring(L"[") + host + L"]:" + std::to_wstring(port);
    }
    return std::wstring(host) + L":" + std::to_wstring(port);
}

}  // namespace SocketUtils
}  // namespace Dns
