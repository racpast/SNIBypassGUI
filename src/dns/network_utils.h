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
// Blocking socket operations with a deadline, used by the DoH, DoT and DNSCrypt
// clients.
//
// Every call here takes a CancelToken and registers the socket it is working on
// with it. That registration is what makes a query abandonable after it has
// started: the token closes the socket, which unblocks the select() or recv()
// underneath, and the operation returns failure instead of running to its own
// deadline. A caller that passes no token gets the ordinary deadline-only
// behaviour, which is what the standalone tests use.
//
// The socket is never closed by these calls. Cancellation closes it, and so
// does whoever owns it, but an operation returning false must not — the owner
// has to be able to tell "it failed" from "it is gone".
#include <winsock2.h>

#include <cstddef>
#include <cstdint>
#include <string>
#include <vector>

#include "dns/cancel.h"

namespace Dns {
namespace NetworkUtils {

// A validated numeric IP endpoint. DNS stamps carry literal addresses so
// resolving names here would recurse through the DNS proxy itself.
struct IpEndpoint {
    sockaddr_storage address = {};
    int length = 0;
    std::string host;
};

// Parse an IPv4 or IPv6 literal with an optional port. IPv6 ports require the
// standard bracket form, for example "[2001:db8::1]:443"; an unbracketed IPv6
// literal uses `defaultPort`.
bool ParseIpEndpoint(const std::string& text, uint16_t defaultPort, IpEndpoint& endpoint);

// Connect to a remote endpoint within `timeoutMs`, or until `cancel` fires.
//
// The socket is left non-blocking on success, so a caller that wants to block
// must switch it back itself.
bool ConnectWithTimeout(SOCKET sock, const sockaddr* addr, int addrLen, uint32_t timeoutMs,
                        const CancelToken* cancel = nullptr);

// Send all of `data` over a stream socket within `timeoutMs`.
bool SendAll(SOCKET sock, const std::vector<uint8_t>& data, uint32_t timeoutMs,
             const CancelToken* cancel = nullptr);

// Pointer form used by Schannel output buffers without copying them into a
// temporary vector.
bool SendAll(SOCKET sock, const uint8_t* data, size_t len, uint32_t timeoutMs,
             const CancelToken* cancel = nullptr);

// Send one datagram to `addr` within `timeoutMs`.
bool SendUdp(SOCKET sock, const std::vector<uint8_t>& data, const sockaddr* addr, int addrLen,
             uint32_t timeoutMs, const CancelToken* cancel = nullptr);

// Receive one datagram within `timeoutMs`. An empty result means the deadline
// passed, the token fired, or the socket failed — none of which is worth
// distinguishing to a caller racing several upstreams.
std::vector<uint8_t> RecvUdp(SOCKET sock, uint32_t timeoutMs,
                             const CancelToken* cancel = nullptr);

// Receive exactly `len` bytes within `timeoutMs`.
bool RecvExact(SOCKET sock, uint8_t* buffer, size_t len, uint32_t timeoutMs,
               const CancelToken* cancel = nullptr);

// Receive a length-prefixed message (2-byte big-endian length + payload),
// returning the payload without its prefix.
std::vector<uint8_t> RecvLengthPrefixed(SOCKET sock, uint32_t timeoutMs,
                                        const CancelToken* cancel = nullptr);

}  // namespace NetworkUtils
}  // namespace Dns
