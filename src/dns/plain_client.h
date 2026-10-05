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
// Plain DNS over UDP, with the TCP escalation RFC 1035 requires.
//
// This is the only upstream transport here that is not encrypted, and the only
// one whose native transport can truncate. A DoH, DoT or DNSCrypt server speaks
// TCP for everything, so a response it sends either arrives whole or does not
// arrive; a plain DNS server answers over UDP by default and has to cut an answer
// that does not fit, setting TC to say so. Handling that is not optional the way
// it is for the encrypted transports — it is the normal case for any answer
// larger than the negotiated datagram size.
//
// So this client is the one place the proxy acts as a *conforming* DNS client:
// ask over UDP, and if the answer comes back truncated, ask the same server the
// same question again over TCP and return that.
#include <cstdint>
#include <string>
#include <vector>

#include "dns/cancel.h"
#include "dns/message.h"

namespace Dns {

// Query a plain DNS server at `address` ("IP:port").
//
// Sends the query over UDP first. If the response carries TC=1 the query is
// retried over TCP, which is what the flag is for: the datagram carried as much
// of the answer as would fit and the rest is available only over a transport
// with no size limit. A response that is not truncated, or a TCP retry that
// fails, is returned as it came — including a second truncated response, which
// is the upstream's problem to have produced and is still worth returning, since
// a client that sees TC can escalate on its own.
//
// Returns the DNS response in wire format, or an empty vector on failure.
std::vector<uint8_t> QueryPlainDns(const std::vector<uint8_t>& query,
                                   const std::string& address, uint32_t timeoutMs,
                                   const CancelToken* cancel = nullptr);

}  // namespace Dns
