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
// See the license file in the project root for full terms and conditions.

#include "platform/ports.h"

#include <winsock2.h>
#include <ws2tcpip.h>

#include <iphlpapi.h>

#include <algorithm>
#include <cstring>

#include "app/text.h"
#include "platform/process.h"
#include "platform/socket_runtime.h"

namespace Ports {
namespace {

// TCP table ports are big-endian on the wire; the UDP table's are too.
int HostPort(DWORD tablePort) {
    return static_cast<int>(((tablePort & 0xFFu) << 8u) | ((tablePort >> 8u) & 0xFFu));
}

// One socket holding a port, with the address it holds it on.
//
// The address is the whole reason this is a struct rather than a PID. A port is a
// namespace per ADDRESS, not per number — measured on Windows, a holder on
// 127.0.0.2 does not stop a bind of 127.0.0.3 — so "who holds port 53" has no
// answer a caller can use, and the question that does have one is "who holds THIS
// endpoint". Answering it needs the address beside the PID.
struct BoundSocket {
    sockaddr_storage address = {};
    DWORD pid = 0;
};

// The family byte of a sockaddr_storage, for the tables that mix both.
int FamilyOf(const sockaddr_storage& address) {
    return static_cast<int>(address.ss_family);
}

// True if this address is the wildcard of its family: every address of that family,
// which is what makes it conflict with far more than a specific address does.
bool IsWildcardAddress(const sockaddr_storage& address) {
    if (address.ss_family == AF_INET) {
        const auto& v4 = reinterpret_cast<const sockaddr_in&>(address);
        return v4.sin_addr.s_addr == INADDR_ANY;
    }
    if (address.ss_family == AF_INET6) {
        const auto& v6 = reinterpret_cast<const sockaddr_in6&>(address);
        return std::memcmp(&v6.sin6_addr, &in6addr_any, sizeof(in6_addr)) == 0;
    }
    return false;
}

// The two IPv4 literals are the same address.
bool SameV4(const sockaddr_storage& a, const sockaddr_storage& b) {
    return reinterpret_cast<const sockaddr_in&>(a).sin_addr.s_addr ==
           reinterpret_cast<const sockaddr_in&>(b).sin_addr.s_addr;
}

// The two IPv6 literals are the same address.
bool SameV6(const sockaddr_storage& a, const sockaddr_storage& b) {
    return std::memcmp(&reinterpret_cast<const sockaddr_in6&>(a).sin6_addr,
                       &reinterpret_cast<const sockaddr_in6&>(b).sin6_addr,
                       sizeof(in6_addr)) == 0;
}

// Whether a socket bound to `holder` is in the way of a bind of `claim`.
//
// A predicate over the two addresses rather than over their text, and it is the
// measured relation rather than a guess. Holding the wildcard of a family takes
// every address in it, so it is in the way of anything in that family, including a
// claim that names one specific address. Holding a specific address is in the way of
// the same address, and of a claim for the wildcard — which cannot be satisfied
// while any address of the family is taken — but NOT of a different specific
// address, which binds beside it.
//
// IPv6 is asymmetric here on purpose. A v6 socket bound without V6ONLY is
// dual-stack and owns the v4 wildcard as well, so it is in the way of a v4 claim; a
// v4 socket does not touch the v6 namespace, so it is not in the way of a v6 one.
// That asymmetry is what the platform does, not a simplification of it.
bool ConflictsWith(const sockaddr_storage& holder, const sockaddr_storage& claim) {
    const int holderFamily = FamilyOf(holder);
    const int claimFamily = FamilyOf(claim);

    if (claimFamily == AF_INET) {
        if (holderFamily == AF_INET6) return IsWildcardAddress(holder);
        if (holderFamily != AF_INET) return false;
        // A claim for the v4 wildcard cannot be satisfied while any v4 address is
        // taken, so every holder of the port is in its way.
        return IsWildcardAddress(claim) || IsWildcardAddress(holder) || SameV4(holder, claim);
    }

    if (claimFamily == AF_INET6) {
        if (holderFamily != AF_INET6) return false;
        return IsWildcardAddress(claim) || IsWildcardAddress(holder) || SameV6(holder, claim);
    }

    return false;
}

// The address a table row is bound to, one overload per row type.
//
// Overloads rather than a branch on `family` inside the collector, because the two
// families' rows are different TYPES with different member names — dwLocalAddr is a
// DWORD in the v4 row and ucLocalAddr is a 16-byte array in the v6 one — and a
// runtime branch on the family would have to compile BOTH member accesses against
// whichever type the template was instantiated with. The overload is chosen by the
// row type, which is exactly the fact that decides how to read it, so there is no
// second source of truth about which family a row belongs to.
void FillLocalAddress(const MIB_TCPROW_OWNER_PID& row, sockaddr_storage& out) {
    auto& v4 = reinterpret_cast<sockaddr_in&>(out);
    v4.sin_family = AF_INET;
    v4.sin_addr.s_addr = row.dwLocalAddr;  // already network order
}

void FillLocalAddress(const MIB_TCP6ROW_OWNER_PID& row, sockaddr_storage& out) {
    auto& v6 = reinterpret_cast<sockaddr_in6&>(out);
    v6.sin6_family = AF_INET6;
    std::memcpy(&v6.sin6_addr, row.ucLocalAddr, sizeof(in6_addr));
}

void FillLocalAddress(const MIB_UDPROW_OWNER_PID& row, sockaddr_storage& out) {
    auto& v4 = reinterpret_cast<sockaddr_in&>(out);
    v4.sin_family = AF_INET;
    v4.sin_addr.s_addr = row.dwLocalAddr;
}

void FillLocalAddress(const MIB_UDP6ROW_OWNER_PID& row, sockaddr_storage& out) {
    auto& v6 = reinterpret_cast<sockaddr_in6&>(out);
    v6.sin6_family = AF_INET6;
    std::memcpy(&v6.sin6_addr, row.ucLocalAddr, sizeof(in6_addr));
}

// A TCP row that means "this process is holding the port": listening, or bound and
// not listening yet.
//
// The second one is here because it is not hypothetical. Measured on Windows, a
// process that has bound 0.0.0.0:53 with SO_EXCLUSIVEADDRUSE but not yet called
// listen() still blocks a bind of 127.0.0.1:53 — with WSAEACCES — and a table read
// restricted to LISTEN would not see it, which would leave that claim classified as
// a broken declaration when something is in fact holding it.
//
// Every other state is a connection lifecycle: a socket in TIME_WAIT is waiting out
// a timer on a port nothing can bind anyway, and naming its owner would put a
// process in a termination list for a socket it no longer has.
bool IsHolderState(DWORD state) {
    return state == MIB_TCP_STATE_LISTEN || state == MIB_TCP_STATE_CLOSED;
}

template <typename Table>
void CollectListeners(ULONG family, int port, std::vector<BoundSocket>& holders) {
    DWORD size = 0;
    GetExtendedTcpTable(nullptr, &size, FALSE, family, TCP_TABLE_OWNER_PID_ALL, 0);
    if (size == 0) return;
    std::vector<BYTE> buf(size);
    if (GetExtendedTcpTable(buf.data(), &size, FALSE, family, TCP_TABLE_OWNER_PID_ALL, 0) !=
        NO_ERROR)
        return;
    const auto* table = reinterpret_cast<const Table*>(buf.data());
    for (DWORD i = 0; i < table->dwNumEntries; ++i) {
        const auto& row = table->table[i];
        if (!IsHolderState(row.dwState)) continue;
        if (HostPort(row.dwLocalPort) != port) continue;

        BoundSocket holder;
        holder.pid = row.dwOwningPid;
        FillLocalAddress(row, holder.address);
        holders.push_back(holder);
    }
}

// Every UDP socket bound to `port`, whatever its address.
//
// UDP has no state column to filter on: a bound socket is simply an entry in the
// table, so this is every holder rather than every waiter. The address is read for
// the same reason it is read for TCP — it is what decides whether this holder is in
// the way of a particular claim — and the filtering is left to the caller.
template <typename Table>
void CollectUdpHolders(ULONG family, int port, std::vector<BoundSocket>& holders) {
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
        if (HostPort(row.dwLocalPort) != port) continue;

        BoundSocket holder;
        holder.pid = row.dwOwningPid;
        FillLocalAddress(row, holder.address);
        holders.push_back(holder);
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

// How a bind attempt ended, in the only detail the caller needs: whether anything
// could be done about it.
//
// A bool cannot carry this. "Could not bind" covers a socket someone else holds,
// which is answered by freeing the port, and a declaration that cannot be satisfied
// here at all, which is answered by editing it — and telling those apart is the
// whole reason AuditClaims exists. Collapsing them into one bit is what put a
// payload defect in front of the user as a port conflict.
enum class BindStatus : std::uint8_t {
    Bound,            // the socket holds the endpoint
    NotALiteral,      // the address is not a numeric IP literal
    AddressNotLocal,  // WSAEADDRNOTAVAIL: no interface has this address
    InUse,            // something else holds the endpoint
    Unavailable,      // the stack refused for another reason (excluded range, ...)
};

// One bind attempt: the socket on success, and why not otherwise.
struct BindOutcome {
    SOCKET socket = INVALID_SOCKET;
    BindStatus status = BindStatus::Unavailable;
};

// Whether a process the tables can see is holding an address this claim cannot
// coexist with.
//
// Only ever asked to resolve WSAEACCES, and that is the whole of its job. The code
// means two different things — an EXCLUSIVE holder on the wildcard, or a port the
// stack has excluded with nothing bound at all — and this is what tells them apart.
//
// It answers "nobody" for a holder that is bound but has not called listen() yet,
// and that is not a defect here: such a holder fails the bind with WSAEADDRINUSE,
// not WSAEACCES, so it never reaches this question. See the switch in TryBindOne.
bool HasVisibleHolder(const PortClaim& claim) {
    return !BlockersOf(claim).empty();
}

// Try one bind, for one transport, in the mode the claim names, and report the
// socket whether it succeeded or not.
//
// The socket is handed back rather than closed here, and that is the whole reason
// this is a separate step from closing it: a caller testing ONE claim has nothing
// to do with the socket and closes it at once, while a caller testing a SET has to
// keep every socket it opened open until the last claim has been tried — a socket
// closed after the bind would have released the very port the next claim could
// conflict with, and the conflict would go unreported. The caller closes what it is
// given; on failure there is nothing to close.
//
// Nothing is ever listened on: bind() is the whole of what a service needs before
// it can serve, and it is the whole of what is tested here.
//
// Winsock is started here rather than assumed. Without that this function answered
// "cannot bind" for every claim on a machine where nothing was listening, because
// socket() before WSAStartup returns INVALID_SOCKET and the failure is
// indistinguishable from a real one. The runtime is process-wide and this is
// idempotent, so a caller that did start it pays a guard variable.
BindOutcome TryBindOne(const PortClaim& claim, int type, int protocol) {
    BindOutcome outcome;

    if (!SocketRuntime::Ensure()) return outcome;

    sockaddr_storage storage = {};
    const int addrLen = ResolveLiteral(claim.address, storage, claim.port);
    if (addrLen == 0) {
        outcome.status = BindStatus::NotALiteral;
        return outcome;
    }

    const SOCKET s = socket(storage.ss_family, type, protocol);
    if (s == INVALID_SOCKET) return outcome;

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

    if (ok && bind(s, reinterpret_cast<const sockaddr*>(&storage), addrLen) != SOCKET_ERROR) {
        outcome.socket = s;
        outcome.status = BindStatus::Bound;
        return outcome;
    }

    // Read before the socket goes away, because closing it clobbers the thread's
    // last-error and the reason is what the caller is asking for.
    const int error = ok ? WSAGetLastError() : 0;
    closesocket(s);

    switch (error) {
        // The one code that answers the question on its own. This is not a conflict
        // and never was: the address the claim names is not on this machine, so no
        // holder exists for it and no amount of freeing a port would help.
        case WSAEADDRNOTAVAIL: outcome.status = BindStatus::AddressNotLocal; break;

        // Something has the endpoint. Taken at face value rather than confirmed
        // against the tables, because the tables cannot confirm it: a socket that is
        // bound but has not called listen() yet holds the endpoint, blocks the bind
        // with exactly this code, and appears in NO table. Asking the tables here
        // would reclassify a real holder as a broken declaration.
        case WSAEADDRINUSE: outcome.status = BindStatus::InUse; break;

        // Genuinely ambiguous, and the one case that has to be resolved by looking.
        // Measured on Windows 11: an EXCLUSIVE holder on the wildcard makes a bind of
        // one specific address fail with this code, and so does a port inside a range
        // the stack has excluded — with nothing held at all. The tables tell those
        // apart, and only here: whichever of the two it is, the answer decides
        // between offering to terminate a process and telling the user to correct a
        // declaration.
        case WSAEACCES:
            outcome.status =
                HasVisibleHolder(claim) ? BindStatus::InUse : BindStatus::Unavailable;
            break;

        default: outcome.status = BindStatus::Unavailable; break;
    }

    return outcome;
}

// Bind every socket `claim` needs — one for a single transport, both for a claim
// that names both — appending each to `held`.
//
// All-or-nothing per claim: a claim naming both transports whose second bind fails
// has still taken the first, and leaving that half open would make the NEXT claim
// answer a question about a machine this function had itself changed. So a partial
// success is closed again before the status is returned. Closes `held` down to the
// size it had on entry, which is what makes the caller's own cleanup uniform.
//
// The status of the attempt that did not bind: the TCP one when it is TCP that
// failed, otherwise the UDP one. Bound only when every socket the claim needs was
// taken. The first failure is the one reported because it is the one that describes
// why the claim is not held — a claim whose TCP half is conflicted is not held,
// whatever the UDP half would have done.
BindStatus TryBindClaim(const PortClaim& claim, std::vector<SOCKET>& held) {
    const size_t mark = held.size();

    const bool tcp = claim.transport != Transport::Udp;
    const bool udp = claim.transport != Transport::Tcp;

    const auto rollback = [&held, mark] {
        for (size_t i = mark; i < held.size(); ++i) closesocket(held[i]);
        held.resize(mark);
    };

    if (tcp) {
        const BindOutcome outcome = TryBindOne(claim, SOCK_STREAM, IPPROTO_TCP);
        if (outcome.status != BindStatus::Bound) {
            rollback();
            return outcome.status;
        }
        held.push_back(outcome.socket);
    }
    if (udp) {
        const BindOutcome outcome = TryBindOne(claim, SOCK_DGRAM, IPPROTO_UDP);
        if (outcome.status != BindStatus::Bound) {
            rollback();
            return outcome.status;
        }
        held.push_back(outcome.socket);
    }

    return BindStatus::Bound;
}

// The most sockets one claim can need: one per transport, and there are two.
constexpr size_t kMaxSocketsPerClaim = 2;

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
    std::vector<SOCKET> held;
    const bool ok = TryBindClaim(claim, held) == BindStatus::Bound;
    for (SOCKET s : held) closesocket(s);
    return ok;
}

bool CanBindAll(const std::vector<PortClaim>& claims, std::vector<PortClaim>* failed) {
    if (failed) failed->clear();

    // Every socket stays open until every claim has been tried. This is the entire
    // difference from calling CanBind per claim, and it is what makes one claim of a
    // set able to conflict with another: the port the first socket took is still
    // taken when the second one asks for it.
    std::vector<SOCKET> held;
    held.reserve(claims.size() * kMaxSocketsPerClaim);

    bool all = true;
    for (const PortClaim& claim : claims) {
        if (TryBindClaim(claim, held) == BindStatus::Bound) continue;
        all = false;
        if (failed) failed->push_back(claim);
    }

    for (SOCKET s : held) closesocket(s);
    return all;
}

ClaimAudit AuditClaims(const std::vector<PortClaim>& claims) {
    ClaimAudit audit;

    // One pass over the whole set, with every socket held, is what produces the
    // failures; asking each one again on its own is what says whose fault each is.
    // The order matters and is the whole reason this is one function: a claim that
    // fails in the group is a candidate for any of the three lists, and only the
    // second bind starts separating them.
    std::vector<PortClaim> failed;
    if (CanBindAll(claims, &failed)) return audit;

    for (const PortClaim& claim : failed) {
        // Asked on its own, and the STATUS is what classifies it. Asking again
        // because the group's answer cannot tell the two apart: a claim blocked by a
        // sibling and a claim blocked by the machine both fail there.
        std::vector<SOCKET> held;
        const BindStatus alone = TryBindClaim(claim, held);
        for (SOCKET s : held) closesocket(s);

        switch (alone) {
            // Binds alone: what stopped it was one of the claims beside it. Not a
            // port conflict, and not something to go looking for a process to
            // terminate over.
            case BindStatus::Bound: audit.contradicted.push_back(claim); break;

            // Bound by something else. Answered by freeing the endpoint, which is
            // what the conflict path does next.
            case BindStatus::InUse: audit.unavailable.push_back(claim); break;

            // Not a conflict at all, and never was: the address is not a literal,
            // this machine does not have it, or the stack will not give the port to
            // anyone. No process is holding anything, so nothing can be terminated to
            // help — the declaration is what has to change.
            case BindStatus::NotALiteral:
            case BindStatus::AddressNotLocal:
            case BindStatus::Unavailable: audit.invalid.push_back(claim); break;
        }
    }
    return audit;
}

std::vector<PortClaim> ClaimAudit::ConfigurationFaults() const {
    std::vector<PortClaim> faults;
    faults.reserve(contradicted.size() + invalid.size());
    for (const PortClaim& claim : contradicted) faults.push_back(claim);
    for (const PortClaim& claim : invalid) faults.push_back(claim);
    return faults;
}

// Every process holding `port` on an address that stands in the way of `claim`.
//
// The address is resolved from the claim here, once, and compared as an address
// rather than as text: "127.0.0.1" and "127.000.000.001" are the same endpoint, and
// "[::1]" is a spelling the tables never use. A claim whose address is not a literal
// resolves to nothing, which is correct — nothing can be holding an address that
// does not exist — and is also why a malformed claim can never name a process.
std::vector<DWORD> BlockersOf(const PortClaim& claim) {
    sockaddr_storage want = {};
    if (ResolveLiteral(claim.address, want, claim.port) == 0) return {};

    std::vector<BoundSocket> holders;
    const bool tcp = claim.transport != Transport::Udp;
    const bool udp = claim.transport != Transport::Tcp;

    if (tcp) {
        CollectListeners<MIB_TCPTABLE_OWNER_PID>(AF_INET, claim.port, holders);
        CollectListeners<MIB_TCP6TABLE_OWNER_PID>(AF_INET6, claim.port, holders);
    }
    if (udp) {
        CollectUdpHolders<MIB_UDPTABLE_OWNER_PID>(AF_INET, claim.port, holders);
        CollectUdpHolders<MIB_UDP6TABLE_OWNER_PID>(AF_INET6, claim.port, holders);
    }

    std::vector<DWORD> pids;
    for (const BoundSocket& holder : holders) {
        if (!ConflictsWith(holder.address, want)) continue;
        if (holder.pid != 0) pids.push_back(holder.pid);
    }
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
