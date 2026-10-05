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
// DNSStamp parser.
//
// DNSStamp is the DNSCrypt project's compact, base64-encoded representation of
// DNS server parameters. A stamp encodes everything needed to reach a DoH, DoT,
// or DNSCrypt server: protocol, address, port, hostname (SNI), path, and hashes.
//
// Format: sdns://<base64-encoded-data>
//
// Specification: https://dnscrypt.info/stamps-specifications
#include <cstdint>
#include <string>
#include <vector>

namespace Dns {

enum class StampProtocol {
    PlainDNS = 0x00,  // Plain DNS over UDP, with TCP escalation on TC=1
    DNSCrypt = 0x01,  // DNSCrypt
    DoH = 0x02,       // DNS-over-HTTPS
    DoT = 0x03,       // DNS-over-TLS
    DoQ = 0x04,       // DNS-over-QUIC (not yet implemented)
    Unknown = 0xFF
};

struct DNSStamp {
    StampProtocol protocol = StampProtocol::Unknown;
    uint64_t props = 0;                        // Properties bitmask
    std::string address;                       // IP:port (IPv6 in brackets)
    std::vector<std::vector<uint8_t>> hashes;  // Server certificate hashes
    std::string hostname;                      // Host name (vhost+SNI)
    std::string path;                          // DoH path
    std::vector<uint8_t> publicKey;            // DNSCrypt provider public key
    std::string providerName;                  // DNSCrypt provider name
    bool valid = false;
};

// Parse a DNSStamp string (sdns://...) into its components.
// Returns a stamp with valid=false if the string is malformed.
DNSStamp ParseDNSStamp(const std::string& stamp);

// Parse a DNSStamp from a wide string (convenience wrapper).
DNSStamp ParseDNSStamp(const std::wstring& stamp);

}  // namespace Dns
