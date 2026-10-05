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
#include <cstdint>
#include <string>
#include <vector>

// Configuration for the DNS Proxy (DnsProxy).
//
// The proxy listens on a fixed local address and forwards every DNS query it
// receives to one or more upstream DNS servers (DoH/DoT/DNSCrypt/plain DNS),
// racing them and returning whichever answers first. Unlike LocalResolver, it
// has no rules and never synthesizes answers — every query is forwarded.
//
// Upstream endpoints are configured using DNSStamp format (sdns://...), which
// encodes protocol, address, hostname, path, and other parameters in a compact
// base64 string. See https://dnscrypt.info/stamps-specifications for details.
namespace Dns {

enum class DnsProxyProtocol {
    PlainDNS,  // plain DNS over UDP, retried over TCP when the answer is truncated
    DoH,       // DNS-over-HTTPS
    DoT,       // DNS-over-TLS
    DNSCrypt,  // DNSCrypt
    Unknown
};

struct DnsProxyEndpoint {
    std::wstring name;
    DnsProxyProtocol protocol;

    // Common fields
    std::string address;  // IP:port (e.g., "1.1.1.1:443")

    // DoH/DoT fields
    std::string hostname;  // SNI / server name (e.g., "dns.cloudflare.com")
    std::string path;      // DoH path (e.g., "/dns-query")
    std::vector<std::vector<uint8_t>> certificateHashes;

    // DNSCrypt fields
    std::string providerName;        // Provider name (e.g., "2.dnscrypt-cert....")
    std::vector<uint8_t> publicKey;  // Provider public key (32 bytes)

    bool enabled;
};

struct DnsProxyConfig {
    // How long one upstream task may spend on a query before it gives up, in
    // milliseconds. Applies per upstream, not to the race as a whole: the race
    // ends when the first answer arrives, so the slowest upstream never sets the
    // client's wait.
    uint32_t timeoutMs = 3000;

    // Minimum number of upstream tasks that may be in flight at once. Startup
    // scales this with the enabled upstream count so several queries can make
    // progress concurrently, up to the hard worker cap.
    size_t threadPoolSize = 32;

    std::vector<DnsProxyEndpoint> upstreams;

    // Load from INI file at `path`. Returns a config with no upstreams if the
    // file cannot be read or is malformed; the proxy refuses to start with
    // nothing to forward to.
    static DnsProxyConfig Load(const std::wstring& path);

    // All upstreams with Enabled=1.
    std::vector<DnsProxyEndpoint> EnabledUpstreams() const {
        std::vector<DnsProxyEndpoint> result;
        for (const auto& up : upstreams) {
            if (up.enabled) result.push_back(up);
        }
        return result;
    }

    size_t EnabledCount() const {
        size_t count = 0;
        for (const auto& up : upstreams) {
            if (up.enabled) ++count;
        }
        return count;
    }
};

}  // namespace Dns
