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
// See the LICENSE.md file in the project root for full license terms.

#include "platform/ports.h"

#include <winsock2.h>
#include <ws2tcpip.h>

#include <iphlpapi.h>

#include <algorithm>

#include "app/text.h"
#include "platform/process.h"
#include "platform/socket_runtime.h"

namespace Ports {
namespace {

// TCP table ports are big-endian on the wire; the UDP table's are too.
int HostPort(DWORD tablePort) {
    return static_cast<int>(((tablePort & 0xFFu) << 8u) | ((tablePort >> 8u) & 0xFFu));
}

template <typename Table>
void CollectListeners(ULONG family, int port, std::vector<DWORD>& pids) {
    DWORD size = 0;
    GetExtendedTcpTable(nullptr, &size, FALSE, family, TCP_TABLE_OWNER_PID_LISTENER, 0);
    if (size == 0) return;
    std::vector<BYTE> buf(size);
    if (GetExtendedTcpTable(buf.data(), &size, FALSE, family, TCP_TABLE_OWNER_PID_LISTENER,
                            0) != NO_ERROR)
        return;
    const auto* table = reinterpret_cast<const Table*>(buf.data());
    for (DWORD i = 0; i < table->dwNumEntries; ++i) {
        const auto& row = table->table[i];
        if (row.dwState == MIB_TCP_STATE_LISTEN && HostPort(row.dwLocalPort) == port)
            pids.push_back(row.dwOwningPid);
    }
}

// Every UDP socket bound to `port`, whatever its address.
//
// UDP has no state column to filter on: a bound socket is simply an entry in the
// table, so this is every holder rather than every waiter. The address is ignored
// on purpose — this is the diagnostic that runs after a bind has already failed, and
// what it is for is naming the process to ask about or terminate, which one entry
// answers whether it holds the wildcard or one address of it.
template <typename Table>
void CollectUdpHolders(ULONG family, int port, std::vector<DWORD>& pids) {
    DWORD size = 0;
    GetExtendedUdpTable(nullptr, &size, FALSE, family, UDP_TABLE_OWNER_PID, 0);
    if (size == 0) return;
    std::vector<BYTE> buf(size);
    if (GetExtendedUdpTable(buf.data(), &size, FALSE, family, UDP_TABLE_OWNER_PID, 0) !=
        NO_ERROR)
        return;
    const auto* table = reinterpret_cast<const Table*>(buf.data());
    for (DWORD i = 0; i < table->dwNumEntries; ++i) {
        const auto& row = table->table[i];
        if (HostPort(row.dwLocalPort) == port) pids.push_back(row.dwOwningPid);
    }
}

// Parse `address` as a numeric literal, filling `storage`. Returns 0 on failure,
// and the length to bind with otherwise — the family comes from the literal rather
// than from the claimant, so a v6 claim is tested as a v6 bind.
int ResolveLiteral(const std::wstring& address, sockaddr_storage& storage, uint16_t port) {
    in_addr v4 = {};
    if (InetPtonW(AF_INET, address.c_str(), &v4) == 1) {
        auto& addr = reinterpret_cast<sockaddr_in&>(storage);
        addr.sin_family = AF_INET;
        addr.sin_port = htons(port);
        addr.sin_addr = v4;
        return sizeof(sockaddr_in);
    }

    in6_addr v6 = {};
    if (InetPtonW(AF_INET6, address.c_str(), &v6) == 1) {
        auto& addr = reinterpret_cast<sockaddr_in6&>(storage);
        addr.sin6_family = AF_INET6;
        addr.sin6_port = htons(port);
        addr.sin6_addr = v6;
        return sizeof(sockaddr_in6);
    }

    return 0;
}

// Try one bind, for one transport, in the mode the claim names.
//
// The socket is closed on every path, including success — the question is whether
// the bind is possible, and holding it afterwards would answer differently for every
// later check. Nothing is ever listened on: bind() is the whole of what a service
// needs before it can serve, and it is the whole of what is tested here.
//
// Winsock is started here rather than assumed. Without that this function answered
// "cannot bind" for every claim on a machine where nothing was listening, because
// socket() before WSAStartup returns INVALID_SOCKET and the failure is
// indistinguishable from a real one. The runtime is process-wide and this is
// idempotent, so a caller that did start it pays a guard variable.
bool CanBindOne(const PortClaim& claim, int type, int protocol) {
    if (!SocketRuntime::Ensure()) return false;

    sockaddr_storage storage = {};
    const int addrLen = ResolveLiteral(claim.address, storage, claim.port);
    if (addrLen == 0) return false;

    const SOCKET s = socket(storage.ss_family, type, protocol);
    if (s == INVALID_SOCKET) return false;

    bool ok = true;

    // The option the claim names, and only that one. Setting SO_EXCLUSIVEADDRUSE and
    // SO_REUSEADDR together is refused by the stack, and a claim that asked for both
    // is a claim that does not describe a real socket.
    if (claim.mode == BindMode::Exclusive) {
        BOOL exclusive = TRUE;
        ok = setsockopt(s, SOL_SOCKET, SO_EXCLUSIVEADDRUSE,
                        reinterpret_cast<const char*>(&exclusive),
                        sizeof(exclusive)) != SOCKET_ERROR;
    } else if (claim.mode == BindMode::Reuse) {
        BOOL reuse = TRUE;
        ok = setsockopt(s, SOL_SOCKET, SO_REUSEADDR, reinterpret_cast<const char*>(&reuse),
                        sizeof(reuse)) != SOCKET_ERROR;
    }

    if (ok) {
        ok = bind(s, reinterpret_cast<const sockaddr*>(&storage), addrLen) != SOCKET_ERROR;
    }

    closesocket(s);
    return ok;
}

}  // namespace

std::wstring PortClaim::Text() const {
    std::wstring text = address + L":" + std::to_wstring(port);

    // The transport and the mode are named only when they are not the defaults, which
    // is the same rule the declarations themselves follow: the short form is what
    // almost every claim says, and spelling it out here would make every message
    // longer to read than the one claim that differs.
    if (transport != Transport::Tcp) {
        text += transport == Transport::Udp ? L"/udp" : L"/both";
    }
    if (mode == BindMode::Reuse) text += L"/reuse";
    if (mode == BindMode::Exclusive) text += L"/exclusive";
    return text;
}

std::wstring PortClaim::Describe() const {
    std::wstring text;
    switch (transport) {
        case Transport::Tcp: text = L"TCP "; break;
        case Transport::Udp: text = L"UDP "; break;
        case Transport::Both: text = L"TCP/UDP "; break;
    }
    return text + address + L":" + std::to_wstring(port);
}

bool CanBind(const PortClaim& claim) {
    switch (claim.transport) {
        case Transport::Tcp: return CanBindOne(claim, SOCK_STREAM, IPPROTO_TCP);
        case Transport::Udp: return CanBindOne(claim, SOCK_DGRAM, IPPROTO_UDP);
        case Transport::Both:
            // Both, because the consumer needs both: a DNS server that can have its
            // TCP socket and not its UDP one is a server that fails on the other half
            // of its own start.
            return CanBindOne(claim, SOCK_STREAM, IPPROTO_TCP) &&
                   CanBindOne(claim, SOCK_DGRAM, IPPROTO_UDP);
    }
    return false;
}

std::vector<DWORD> ListenersOn(int port) {
    std::vector<DWORD> pids;
    CollectListeners<MIB_TCPTABLE_OWNER_PID>(AF_INET, port, pids);
    CollectListeners<MIB_TCP6TABLE_OWNER_PID>(AF_INET6, port, pids);
    std::sort(pids.begin(), pids.end());
    pids.erase(std::unique(pids.begin(), pids.end()), pids.end());
    return pids;
}

std::vector<DWORD> UdpHoldersOn(int port) {
    std::vector<DWORD> pids;
    CollectUdpHolders<MIB_UDPTABLE_OWNER_PID>(AF_INET, port, pids);
    CollectUdpHolders<MIB_UDP6TABLE_OWNER_PID>(AF_INET6, port, pids);
    std::sort(pids.begin(), pids.end());
    pids.erase(std::unique(pids.begin(), pids.end()), pids.end());
    return pids;
}

bool IsSystemCritical(DWORD pid) {
    if (pid == 0 || pid == 4) return true;  // Idle and System

    // The System process (PID 4) hosts kernel-mode drivers including http.sys,
    // which parks 80/443 for IIS/BranchCache. We handle http.sys by stopping the
    // services (net stop http), never by killing PID 4.
    std::wstring image;
    if (!Process::TryImagePath(pid, image)) return true;  // unidentified: assume critical
    image = LowerW(image);

    // System-protected paths: anything under System32 that's not our service.
    if (image.find(L"\\system32\\") != std::wstring::npos ||
        image.find(L"\\syswow64\\") != std::wstring::npos) {
        return true;
    }

    // Common system services that legitimately hold ports.
    return image.find(L"\\svchost.exe") != std::wstring::npos ||
           image.find(L"\\services.exe") != std::wstring::npos ||
           image.find(L"\\lsass.exe") != std::wstring::npos ||
           image.find(L"\\wininit.exe") != std::wstring::npos ||
           image.find(L"\\csrss.exe") != std::wstring::npos;
}

}  // namespace Ports
