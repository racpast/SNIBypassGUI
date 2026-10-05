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

// The bound on select()'s descriptor array is a compile definition, set in
// CMakeLists.txt. It cannot be a `#define` here, and this is worth knowing
// before moving it back: FD_SETSIZE's default is fixed by <winsock.h>, which
// <windows.h> pulls in, and <winsock2.h> guards its own definition so it does
// not argue — so a header that defines it only takes effect if it happens to be
// read before windows.h, and silently does nothing otherwise.
//
// The check below is what turns that silence into a build error. Without it a
// missing -DFD_SETSIZE would surface as a static_assert about socket budgets in
// a server file, which points at the wrong thing entirely.
#if !defined(FD_SETSIZE) || FD_SETSIZE < 128
#error "FD_SETSIZE must be at least 128; it is set as a compile definition in CMakeLists.txt."
#endif

// Winsock plumbing shared by the two DNS servers this program runs.
//
// LocalResolver and DnsProxy both listen on a loopback address, both hand out
// ephemeral sockets to reach real servers, and both have to survive the same
// Windows quirks doing it. Those pieces live here once rather than being
// written twice and drifting apart.
//
// That claim is about more than the socket wrappers: it also covers the pieces
// of a server's own shape the two genuinely share — how many connections a
// loop sized for one fd_set can serve and how long one may sit idle, the
// select-and-check-deadlines preamble every pass begins with, handing a
// datagram to a client, and writing a queued response out. What is *not* here
// is anything a policy could differ on: which connection to drop when the
// budget is full stays with the server that has an opinion about it.
//
// Nothing in this header knows what a DNS message is: it is sockets, deadlines,
// and the UDP behaviour Windows gets wrong.
#include <winsock2.h>

#include <cstdint>
#include <string>
#include <vector>

#include "dns/cancel.h"

namespace Dns {
namespace SocketUtils {

// How often a loop that is otherwise waiting checks its deadlines, in
// milliseconds. Also the longest a Stop() can take to be noticed.
inline constexpr long kTickMs = 100;

// The largest DNS message that can exist: the TCP length prefix is 16 bits.
inline constexpr size_t kMaxMessage = 65535;

// Client connections either server serves at once, and how long one may sit
// idle before it is dropped.
//
// The value is a budget rather than a preference: an fd_set holds FD_SETSIZE
// descriptors, and a listening socket plus whatever else a given loop watches
// comes out of the same array. Each server asserts its own arithmetic against
// it — DnsProxy has one descriptor per session, LocalResolver has two, because
// it holds the upstream connection in the same loop.
inline constexpr size_t kMaxTcpSessions = 32;
inline constexpr uint64_t kTcpIdleTimeoutMs = 15000;

// A `select` timeout that wakes the loop on the next tick, which is what makes
// deadlines and shutdown observable while nothing is arriving.
//
// Precomputed rather than assembled at each `select` because a long is a
// millisecond count and a timeval is seconds plus microseconds, and a server
// that got that arithmetic wrong would wake either never or as fast as the CPU
// allows — neither of which shows up in a test.
inline timeval TickTimeout() {
    timeval timeout = {};
    timeout.tv_sec = kTickMs / 1000;
    timeout.tv_usec = (kTickMs % 1000) * 1000;
    return timeout;
}

// Send one datagram to a client, ignoring the outcome.
//
// A failure here is not actionable: the client is gone, or the stack is out of
// buffers, and either way there is nothing left to retry against — that is the
// receive path's business, not this one's. The empty-check is because a caller
// that is answering with a message it just failed to build would otherwise
// send zero bytes, which on UDP is a valid datagram carrying nothing.
inline void SendDatagram(SOCKET sock, const std::vector<uint8_t>& message,
                         const sockaddr_storage& to, int toLen) {
    if (message.empty()) return;
    sendto(sock, reinterpret_cast<const char*>(message.data()),
           static_cast<int>(message.size()), 0, reinterpret_cast<const sockaddr*>(&to), toLen);
}

// How a write of a queued response ended.
enum class WriteResult {
    Done,        // every byte is out; the caller owns the buffer and should clear it
    WouldBlock,  // the socket's buffer filled: keep the offset, try again later
    Failed,      // the socket is gone; the connection should be dropped
};

// Write `data` starting at `sent`, advancing `sent`. `sent` is a reference
// because a partial write has to be resumed from where it stopped: a stream
// socket takes what it has room for, and re-sending a prefix would corrupt the
// message the client reassembles.
inline WriteResult WritePending(SOCKET sock, const std::vector<uint8_t>& data, size_t& sent) {
    while (sent < data.size()) {
        const int n = send(sock, reinterpret_cast<const char*>(data.data()) + sent,
                           static_cast<int>(data.size() - sent), 0);
        if (n == SOCKET_ERROR) {
            return WSAGetLastError() == WSAEWOULDBLOCK ? WriteResult::WouldBlock
                                                       : WriteResult::Failed;
        }
        sent += static_cast<size_t>(n);
    }
    return WriteResult::Done;
}

// Close `s`, taking it back from `cancel` first so the token cannot close it a
// second time.
//
// A socket handed to a CancelToken is owned by the token: cancelling closes it
// from whichever thread cancels, while this thread may still be sitting in a
// read on it. Whichever thread gets there first must be the only one to close
// it, because on Windows a closed socket value is immediately reusable and a
// second close can tear down an unrelated socket that has since been given the
// same value.
//
// So cleanup goes through here rather than through CloseSocket whenever a token
// is in play: Release() removes the socket from the token, and only if it was
// still registered — meaning the token never cancelled — is it closed here.
// Passing no token is the ordinary non-cancellable case.
inline void ReleaseAndClose(SOCKET& s, const CancelToken* cancel) {
    if (s == INVALID_SOCKET) return;
    if (cancel == nullptr || cancel->Release(s)) {
        closesocket(s);
    }
    s = INVALID_SOCKET;
}

// A socket handle that closes what it holds, and does nothing else with it.
//
// Constructed and destroyed on one thread — it is a local in a call, not a field
// a loop owns — which is why it needs none of the ordering that makes a raw
// SOCKET awkward to pass around.
//
// The handle can also be handed to a CancelToken, and remembering the token is
// the whole point of doing it here: once registered, the socket has two possible
// closers, and the handle itself is the only place that knows which of them got
// there first. Folding that into the destructor is what makes the rule hold on
// every return path rather than only the ones a caller remembered to write.
class SocketHandle {
public:
    SocketHandle() = default;
    explicit SocketHandle(SOCKET s) : m_socket(s) {}
    ~SocketHandle() { Close(); }

    SocketHandle(const SocketHandle&) = delete;
    SocketHandle& operator=(const SocketHandle&) = delete;

    SocketHandle(SocketHandle&& other) noexcept
        : m_socket(other.m_socket), m_token(other.m_token) {
        other.m_socket = INVALID_SOCKET;
        other.m_token = nullptr;
    }

    SocketHandle& operator=(SocketHandle&& other) noexcept {
        if (this != &other) {
            Close();
            m_socket = other.m_socket;
            m_token = other.m_token;
            other.m_socket = INVALID_SOCKET;
            other.m_token = nullptr;
        }
        return *this;
    }

    operator SOCKET() const { return m_socket; }
    SOCKET Get() const { return m_socket; }
    bool IsValid() const { return m_socket != INVALID_SOCKET; }
    explicit operator bool() const { return IsValid(); }

    // Hand the socket to `cancel`, so that cancelling closes it, and remember
    // that fact so this handle never closes it twice.
    //
    // Returns false if the token was already cancelled — in which case it has
    // closed the socket itself, the handle is left empty, and the caller must
    // not use the socket again. Call this before the first operation that can
    // block on the socket; that is the point of it.
    bool RegisterWith(const CancelToken* cancel) {
        if (cancel == nullptr) return IsValid();
        if (!cancel->Register(m_socket)) {
            // Already cancelled: the token closed it as it was registered, and
            // the handle must forget it rather than close it again later.
            m_socket = INVALID_SOCKET;
            return false;
        }
        m_token = cancel;
        return true;
    }

    void Close() {
        ReleaseAndClose(m_socket, m_token);
        m_token = nullptr;
    }

    // Replace what this holds with `s`, closing whatever it held before through
    // the same release path as the destructor. The token is cleared because a
    // fresh socket was never registered with the old one's token, and carrying
    // that token forward would let a cancel close a socket that was bound after
    // the token's work had already been given up on.
    void reset(SOCKET s) {
        Close();
        m_socket = s;
    }

    SOCKET Release() {
        const SOCKET s = m_socket;
        m_socket = INVALID_SOCKET;
        m_token = nullptr;
        return s;
    }

private:
    SOCKET m_socket = INVALID_SOCKET;
    const CancelToken* m_token = nullptr;
};

// Milliseconds since the system started. Monotonic, and the only clock any
// deadline in this program is measured against.
uint64_t Now();

// Winsock, started once for the process and never stopped.
//
// WSACleanup belongs to a program that is finished with sockets, and this one is
// finished with them only when it exits — at which point the kernel does the same
// work. Tying it to a static destructor instead would run it in an order no
// translation unit here controls, while a worker thread may still hold a socket.
bool EnsureWinsock();

void SetNonBlocking(SOCKET s);

// Stop Windows from failing a later recvfrom with WSAECONNRESET because an
// earlier datagram drew an ICMP port-unreachable. On a socket that talks to
// several servers at once, one dead server would otherwise poison reads for all
// of them.
void DisableUdpConnReset(SOCKET s);

void CloseSocket(SOCKET& s);

// How a select-based wait ended.
//
// Three outcomes rather than the 1/0/-1 this used to be spelled out as, because
// the callers that matter have to tell "nothing happened yet" from "give up":
// a timeout is an ordinary thing to retry past, while a cancellation or a
// failed select means the operation has no future. Collapsing them into one
// falsy value is how a cancelled query turns into a timed-out one in a log.
enum class WaitResult {
    Ready,     // the socket is in the requested state
    TimedOut,  // the deadline passed with nothing to report
    Aborted,   // cancelled, or select() itself failed
};

// Take a freshly accepted connection the rest of the way into a server, or
// close it.
//
// Every session in either server is set up the same way after accept(): a
// non-blocking mode, so select() on a later pass can never block in recv, and a
// first idle deadline, so a connection that opens and then says nothing is
// dropped like any other. Doing that here rather than in each acceptor is what
// keeps the two from drifting on a detail neither of them has an opinion about.
//
// What is *not* here is the capacity decision, because the two servers answer it
// differently and their reasons are their own: the resolver refuses a newcomer
// once it is full, while the forwarder evicts its longest-idle session first.
// That branch belongs in the caller, where the reason can be written next to it.
//
// Returns true when the socket is ready to be handed to a session, false when it
// could not be prepared and has been closed.
bool PrepareSessionSocket(SOCKET client);

// One pass of a select()-driven event loop, with its descriptor sets in it.
//
// The skeleton around select() is the same in both servers and is the part
// nobody ever intends to write twice: zero the sets, register the always-on
// listeners, register each session's socket in the state that will not block,
// select with a tick-bounded timeout, and treat a failed select as the end.
// Every one of those steps is easy to get subtly wrong in a copy — a set that
// is never zeroed keeps stale descriptors, and an fd_set used past FD_SETSIZE
// corrupts the stack — so the whole preamble is one object rather than two
// regions of two loops that drift.
//
// What is deliberately *not* here is any decision about what the readiness
// means. Which session socket goes in the read set and which in the write set,
// and what to do once a flag comes back set, differ between the two servers and
// stay with them.
//
// One instance is meant to be a local in the loop body and reused across
// passes: it zeroes itself on every Wait().
class WaitSet {
public:
    // Begin a pass with every set empty.
    void Reset();

    // Register `s` in one of the three sets. Every one of these is a no-op on
    // INVALID_SOCKET, because both loops walk a table whose sockets are opened
    // and closed as it runs, and a stray INVALID_SOCKET is what that walk
    // naturally produces.
    void WatchRead(SOCKET s);
    void WatchWrite(SOCKET s);
    void WatchFailed(SOCKET s);

    // Block until something in the sets is ready, at most one tick.
    //
    // False means select() itself failed, which for both servers is the one way
    // the loop ends without being asked to: the caller stops. `TimedOut()` says
    // whether anything was ready, so deadlines can still be serviced on a pass
    // where nothing arrived.
    bool Wait();

    bool TimedOut() const { return m_ready == 0; }
    bool Readable(SOCKET s) const { return FD_ISSET(s, &m_readable) != 0; }
    bool Writable(SOCKET s) const { return FD_ISSET(s, &m_writable) != 0; }
    bool Failed(SOCKET s) const { return FD_ISSET(s, &m_failed) != 0; }

private:
    fd_set m_readable;
    fd_set m_writable;
    fd_set m_failed;
    int m_ready = 0;
};

// Wait until `sock` is readable and/or writable, or `deadline` passes.
//
// Checks `cancel` once per slice rather than once per call, so a token that
// fires mid-wait is noticed within kWaitPollMs instead of at the deadline. The
// socket close that cancelling performs usually unblocks the wait outright;
// this is the fallback for the paths where it does not, and it is why the wait
// is sliced at all rather than handed to select as one long timeout.
//
// The same loop the DNS clients run and the same one TLS runs, so a change to
// how often a cancellation is noticed — or to the fact that select can be
// interrupted — is a change in one place.
WaitResult WaitFor(SOCKET sock, bool readable, bool writable, uint64_t deadline,
                   const CancelToken* cancel);

// How often a wait above re-reads the cancel flag. Public so a caller that
// waits on something else can pick the same granularity instead of inventing
// its own.
inline constexpr uint64_t kWaitPollMs = 25;

// Create, bind and prepare a listener on `address`:`port`.
//
// SO_EXCLUSIVEADDRUSE is what stops another program from later binding the same
// address with SO_REUSEADDR and quietly taking delivery of the queries meant for
// us. The socket comes back non-blocking, so the caller only has to call
// listen() for a stream socket.
//
// Returns INVALID_SOCKET on failure, with the Winsock error left set.
SOCKET BindListener(const wchar_t* address, uint16_t port, int type, int protocol);

// Compare two addresses by family and address bytes, ignoring the port: the port
// is ours to set, and the same server must not be treated as two because two
// adapters list it.
bool SameHost(const sockaddr_storage& a, const sockaddr_storage& b);

// `addr` as text. A host with no port, or `addr`:`port` when `withPort` is set.
//
// Only for logging — the string is not round-trippable and the port is formatted
// from the same sockaddr the address is, so a caller cannot pair one address's
// text with another's port. An address family this does not know yields "?".
std::wstring AddressText(const sockaddr_storage& addr, int addrLen, bool withPort);

}  // namespace SocketUtils
}  // namespace Dns
