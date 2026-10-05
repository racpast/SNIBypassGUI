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
// DoT (DNS-over-TLS) client.
//
// Sends DNS queries over TLS on port 853 (RFC 7858). The protocol is simpler
// than DoH: establish a TLS connection, send the DNS query prefixed with its
// 2-byte length, read the response similarly length-prefixed.
//
// Each query opens a fresh connection rather than maintaining a pool, which is
// inefficient but simple and sufficient for the wrapper's design: queries are
// rare (only Nginx's resolver uses this), and racing multiple upstreams means
// most connections are discarded immediately anyway.
#include <cstdint>
#include <string>
#include <vector>

#include "dns/cancel.h"

namespace Dns {

// Send a DNS query to a DoT endpoint over TLS.
//
// `query` is the DNS message in wire format.
// `address` is the server address from DNSStamp (e.g., "1.1.1.1:853" or "1.1.1.1").
// `hostname` is the SNI hostname from DNSStamp (e.g., "dns.cloudflare.com").
//             Empty means no SNI extension.
// `timeoutMs` is the total timeout for the operation.
// `cancel`, when given, abandons the query as soon as it fires: the token closes
// the socket, which unblocks the handshake or the read underneath.
//
// Returns the DNS response in wire format, or an empty vector on failure.
std::vector<uint8_t> QueryDoT(const std::vector<uint8_t>& query, const std::string& address,
                              const std::string& hostname,
                              const std::vector<std::vector<uint8_t>>& certificateHashes,
                              uint32_t timeoutMs, const CancelToken* cancel = nullptr);

}  // namespace Dns
