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

#include "dns/dnsstamp.h"

#include <windows.h>

#include <wincrypt.h>

#include <algorithm>
#include <cstring>

#include "app/text.h"

namespace Dns {
namespace {

// Base64 URL decode (RFC 4648)
std::vector<uint8_t> Base64UrlDecode(const std::string& input) {
    if (input.empty()) return {};

    // Convert URL-safe base64 to standard base64
    std::string standard = input;
    std::replace(standard.begin(), standard.end(), '-', '+');
    std::replace(standard.begin(), standard.end(), '_', '/');

    // Add padding if needed
    const size_t padding = (4 - (standard.length() % 4)) % 4;
    standard.append(padding, '=');

    DWORD outLen = 0;
    if (!CryptStringToBinaryA(standard.c_str(), static_cast<DWORD>(standard.size()),
                              CRYPT_STRING_BASE64, nullptr, &outLen, nullptr, nullptr)) {
        return {};
    }

    std::vector<uint8_t> output(outLen);
    if (!CryptStringToBinaryA(standard.c_str(), static_cast<DWORD>(standard.size()),
                              CRYPT_STRING_BASE64, output.data(), &outLen, nullptr, nullptr)) {
        return {};
    }

    output.resize(outLen);
    return output;
}

// Read 64-bit little-endian
bool ReadUint64LE(const uint8_t*& ptr, const uint8_t* end, uint64_t& out) {
    if (ptr + 8 > end) return false;
    out = 0;
    for (int i = 0; i < 8; ++i) {
        out |= static_cast<uint64_t>(ptr[i]) << (i * 8u);
    }
    ptr += 8;
    return true;
}

// Read length-prefixed string (single byte length)
bool ReadLpString(const uint8_t*& ptr, const uint8_t* end, std::string& out) {
    if (ptr >= end) return false;
    const uint8_t len = *ptr++;
    if (ptr + len > end) return false;
    out.assign(reinterpret_cast<const char*>(ptr), len);
    ptr += len;
    return true;
}

// Read length-prefixed binary (single byte length)
bool ReadLpBinary(const uint8_t*& ptr, const uint8_t* end, std::vector<uint8_t>& out) {
    if (ptr >= end) return false;
    const uint8_t len = *ptr++;
    if (ptr + len > end) return false;
    out.assign(ptr, ptr + len);
    ptr += len;
    return true;
}

// Read VLP (Variable Length with continuation bit) list
// Returns false if parsing fails
bool ReadVlpList(const uint8_t*& ptr, const uint8_t* end,
                 std::vector<std::vector<uint8_t>>& items) {
    for (;;) {
        if (ptr >= end) return false;
        const uint8_t vlen = *ptr++;
        const uint8_t len = vlen & 0x7Fu;  // Clear continuation bit
        const bool hasMore = (vlen & 0x80u) != 0;

        if (ptr + len > end) return false;

        if (len > 0) {
            items.emplace_back(ptr, ptr + len);
        }
        ptr += len;

        if (!hasMore) break;
    }
    return true;
}

// Read VLP string list
bool ReadVlpStringList(const uint8_t*& ptr, const uint8_t* end,
                       std::vector<std::string>& items) {
    for (;;) {
        if (ptr >= end) return false;
        const uint8_t vlen = *ptr++;
        const uint8_t len = vlen & 0x7Fu;
        const bool hasMore = (vlen & 0x80u) != 0;

        if (ptr + len > end) return false;

        if (len > 0) {
            items.emplace_back(reinterpret_cast<const char*>(ptr), len);
        }
        ptr += len;

        if (!hasMore) break;
    }
    return true;
}

// Parse DoH stamp (0x02)
DNSStamp ParseDoHStamp(const uint8_t* bin, size_t binLen) {
    DNSStamp stamp;
    stamp.protocol = StampProtocol::DoH;

    if (binLen < 15) return stamp;

    const uint8_t* ptr = bin + 1;
    const uint8_t* end = bin + binLen;

    if (!ReadUint64LE(ptr, end, stamp.props)) return stamp;
    if (!ReadLpString(ptr, end, stamp.address)) return stamp;

    // Read hashes (VLP format)
    std::vector<std::vector<uint8_t>> hashes;
    if (!ReadVlpList(ptr, end, hashes)) return stamp;
    for (const auto& hash : hashes) {
        if (hash.size() != 32) return stamp;
        stamp.hashes.push_back(hash);
    }

    if (!ReadLpString(ptr, end, stamp.hostname)) return stamp;
    if (!ReadLpString(ptr, end, stamp.path)) return stamp;

    // Read optional bootstrap IPs (VLP format)
    if (ptr < end) {
        std::vector<std::string> bootstrapIps;
        if (!ReadVlpStringList(ptr, end, bootstrapIps)) return stamp;
    }
    if (ptr != end) return stamp;

    stamp.valid = true;
    return stamp;
}

// Parse DoT stamp (0x03)
DNSStamp ParseDoTStamp(const uint8_t* bin, size_t binLen) {
    DNSStamp stamp;
    stamp.protocol = StampProtocol::DoT;

    if (binLen < 13) return stamp;

    const uint8_t* ptr = bin + 1;
    const uint8_t* end = bin + binLen;

    if (!ReadUint64LE(ptr, end, stamp.props)) return stamp;
    if (!ReadLpString(ptr, end, stamp.address)) return stamp;

    // Read hashes (VLP format)
    std::vector<std::vector<uint8_t>> hashes;
    if (!ReadVlpList(ptr, end, hashes)) return stamp;
    for (const auto& hash : hashes) {
        if (hash.size() != 32) return stamp;
        stamp.hashes.push_back(hash);
    }

    if (!ReadLpString(ptr, end, stamp.hostname)) return stamp;

    // Read optional bootstrap IPs (VLP format)
    if (ptr < end) {
        std::vector<std::string> bootstrapIps;
        if (!ReadVlpStringList(ptr, end, bootstrapIps)) return stamp;
    }
    if (ptr != end) return stamp;

    stamp.valid = true;
    return stamp;
}

// Parse plain DNS stamp (0x00)
//
// The simplest stamp there is: a protocol byte and one LP string. There is no
// hostname because there is nothing to authenticate — plain DNS is unencrypted,
// and the only name involved is the address itself, which is already here.
DNSStamp ParsePlainDnsStamp(const uint8_t* bin, size_t binLen) {
    DNSStamp stamp;
    stamp.protocol = StampProtocol::PlainDNS;

    const uint8_t* ptr = bin + 1;
    const uint8_t* end = bin + binLen;

    // The props field is present in this format too, even though plain DNS has
    // no properties to describe: the stamp spec keeps every format's fields in
    // the same order, so skipping it here would misread the address.
    if (!ReadUint64LE(ptr, end, stamp.props)) return stamp;
    if (!ReadLpString(ptr, end, stamp.address)) return stamp;
    if (stamp.address.empty()) return stamp;
    if (ptr != end) return stamp;

    stamp.valid = true;
    return stamp;
}

// Parse DNSCrypt stamp (0x01)
DNSStamp ParseDNSCryptStamp(const uint8_t* bin, size_t binLen) {
    DNSStamp stamp;
    stamp.protocol = StampProtocol::DNSCrypt;

    if (binLen < 66) return stamp;

    const uint8_t* ptr = bin + 1;
    const uint8_t* end = bin + binLen;

    if (!ReadUint64LE(ptr, end, stamp.props)) return stamp;
    if (!ReadLpString(ptr, end, stamp.address)) return stamp;
    if (!ReadLpBinary(ptr, end, stamp.publicKey)) return stamp;
    if (!ReadLpString(ptr, end, stamp.providerName)) return stamp;

    if (ptr != end || stamp.publicKey.size() != 32 || stamp.providerName.empty()) return stamp;

    stamp.valid = true;
    return stamp;
}

}  // namespace

DNSStamp ParseDNSStamp(const std::string& stampStr) {
    const std::string prefix = "sdns://";
    if (stampStr.size() <= prefix.size() || stampStr.substr(0, prefix.size()) != prefix) {
        return {};
    }

    std::string base64 = stampStr.substr(prefix.size());
    base64.erase(
        std::remove_if(base64.begin(), base64.end(),
                       [](char c) { return std::isspace(static_cast<unsigned char>(c)); }),
        base64.end());

    std::vector<uint8_t> bin = Base64UrlDecode(base64);
    if (bin.empty() || bin.size() < 1) return {};

    const uint8_t proto = bin[0];

    switch (proto) {
        case 0x00: return ParsePlainDnsStamp(bin.data(), bin.size());
        case 0x01: return ParseDNSCryptStamp(bin.data(), bin.size());
        case 0x02: return ParseDoHStamp(bin.data(), bin.size());
        case 0x03: return ParseDoTStamp(bin.data(), bin.size());
        case 0x04:  // DoQ (DNS-over-QUIC) not yet implemented
        default: return {};
    }
}

DNSStamp ParseDNSStamp(const std::wstring& stamp) {
    return ParseDNSStamp(WideToUtf8(stamp));
}

}  // namespace Dns
