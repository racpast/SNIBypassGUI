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
// DoH (DNS-over-HTTPS) client.
//
// Sends DNS queries over HTTPS using the RFC 8484 wire format: the query is
// sent as-is in a POST body with Content-Type: application/dns-message, and
// the response body is the DNS answer in the same wire format.
//
// GET queries (base64url-encoded in the URL) are also defined by the RFC but
// not implemented here: POST is simpler, universally supported, and never runs
// into URL length limits.
#include <cstdint>
#include <string>
#include <vector>

#include "dns/cancel.h"

namespace Dns {

// Send a DNS query to a DoH endpoint over HTTPS.
//
// `query` is the DNS message in wire format.
// `address` is the server address from DNSStamp (e.g., "1.1.1.1:443" or "1.1.1.1").
// `hostname` is the SNI hostname from DNSStamp (e.g., "dns.cloudflare.com").
// `path` is the HTTP path from DNSStamp (e.g., "/dns-query").
// `timeoutMs` is the total timeout for the operation.
// `cancel`, when given, abandons the query as soon as it fires: the token closes
// the socket, which unblocks the read underneath. This is what lets a race that
// has already been won stop paying for the connections still being opened.
//
// The function connects to `address` but uses `hostname` as TLS SNI, allowing
// IP-based connections with proper SNI for bypassing DNS-based blocking.
//
// Returns the DNS response in wire format, or an empty vector on failure.
std::vector<uint8_t> QueryDoH(const std::vector<uint8_t>& query, const std::string& address,
                              const std::string& hostname, const std::string& path,
                              const std::vector<std::vector<uint8_t>>& certificateHashes,
                              uint32_t timeoutMs, const CancelToken* cancel = nullptr);

}  // namespace Dns
