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

#include "app/payload.h"

#include <winsock2.h>
#include <ws2tcpip.h>

#include <windows.h>

#include <algorithm>
#include <vector>

#include "app/logging.h"
#include "app/paths.h"
#include "app/text.h"
#include "platform/ini.h"

namespace Payload {
namespace {

// The [Resolver] section and the one key it carries. Named here rather than at the
// read so the section this module owns appears exactly once.
constexpr const wchar_t* kResolverSection = L"Resolver";
constexpr const wchar_t* kAddressKey = L"Address";

// The default endpoint. Port 53 is not a setting: the policy table's rule format
// carries a server address and no port — the DNS Client service asks it on 53 — so a
// configured port could not be expressed there, and a rule naming an address whose
// server listened elsewhere would break every name it covers.
constexpr uint16_t kResolverPort = 53;
constexpr const wchar_t* kDefaultAddress = L"127.11.45.14";

// Whether `address` is a literal on the loopback range.
//
// 127.0.0.0/8 rather than just 127.0.0.1, matching what the two servers already
// assume: all of 127/8 reaches loopback, and a machine may well have something else
// of its own on 127.0.0.1:53. What is refused is anything that is NOT on loopback —
// an off-loopback listener here would be a DNS server this program opened to the
// network, answering for names an NRPT rule sends to it.
//
// Accepted as a literal only, and that is a security property rather than a
// limitation. A hostname would have to be resolved to be checked, and the resolution
// would go through the very DNS path this program is in the middle of replacing —
// so "127.0.0.1" the name, or an attacker's answer to it, would decide whether the
// value is loopback. InetPtonW accepts a literal and nothing else, which is exactly
// the question being asked.
bool IsLoopbackLiteral(const std::wstring& address) {
    in_addr v4 = {};
    if (InetPtonW(AF_INET, address.c_str(), &v4) == 1) {
        // Network byte order on the wire; host order here, where the first octet is
        // the low byte on a little-endian machine.
        return (ntohl(v4.s_addr) >> 24u) == 127u;
    }

    in6_addr v6 = {};
    if (InetPtonW(AF_INET6, address.c_str(), &v6) == 1) {
        // ::1, the whole of it and nothing else. There is no IPv6 equivalent of the
        // 127/8 range worth allowing: no interface addresses the rest, and a
        // broader rule here would accept an address this program cannot bind.
        static const uint8_t kLoopback6[16] = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1};
        return std::equal(v6.s6_addr, v6.s6_addr + 16, kLoopback6);
    }

    return false;
}

}  // namespace

std::wstring ConfigPath() {
    return ExeDir() + kFileName;
}

bool Present() {
    return GetFileAttributesW(ConfigPath().c_str()) != INVALID_FILE_ATTRIBUTES;
}

Dns::BindEndpoint LoadResolverEndpoint(const std::wstring& path) {
    Dns::BindEndpoint endpoint;
    endpoint.address = kDefaultAddress;
    endpoint.port = kResolverPort;

    // Only if the file can be read at all. The default stands when it cannot: a
    // payload whose descriptor is missing is a broken install that other code reports
    // on, and refusing here as well would make the ordinary "fresh tree, not yet
    // installed" case fail twice.
    const std::vector<Ini::Section> ini = Ini::Read(path);
    if (ini.empty()) return endpoint;

    const std::wstring address = TrimW(Ini::Value(ini, kResolverSection, kAddressKey));
    if (address.empty()) return endpoint;  // absent: the default, and not worth a log line

    if (!IsLoopbackLiteral(address)) {
        LOGE(L"Payload: [Resolver] Address is not a loopback literal (" + address +
             L"); using " + kDefaultAddress + L".");
        return endpoint;
    }

    endpoint.address = address;
    LOGI(L"Payload: resolver endpoint is " + endpoint.Text() + L".");
    return endpoint;
}

std::vector<Ports::PortClaim> LoadPortClaims(const std::wstring& path) {
    std::vector<Ports::PortClaim> claims;

    const std::vector<Ini::Section> ini = Ini::Read(path);
    if (ini.empty()) return claims;

    const std::wstring list = Ini::Value(ini, L"Ports", L"Required");
    if (list.empty()) return claims;

    for (const std::wstring& entry : Ini::Split(list)) {
        Ports::PortClaim claim;

        // The trailing fields are split off the right, because the address itself may
        // contain the separator: "/" cannot, so the last one is always the mode and
        // the one before it the transport.
        std::vector<std::wstring> parts;
        size_t start = 0;
        for (;;) {
            const size_t slash = entry.find(L'/', start);
            parts.push_back(TrimW(slash == std::wstring::npos
                                      ? entry.substr(start)
                                      : entry.substr(start, slash - start)));
            if (slash == std::wstring::npos) break;
            start = slash + 1;
        }

        const std::wstring endpoint = parts[0];
        if (parts.size() > 1) {
            const std::wstring transport = LowerW(parts[1]);
            if (transport == L"tcp")
                claim.transport = Ports::Transport::Tcp;
            else if (transport == L"udp")
                claim.transport = Ports::Transport::Udp;
            else if (transport == L"both")
                claim.transport = Ports::Transport::Both;
            else {
                LOGE(L"Payload: port claim '" + entry + L"' has an unknown transport (" +
                     parts[1] + L"); skipped.");
                continue;
            }
        }
        if (parts.size() > 2) {
            const std::wstring mode = LowerW(parts[2]);
            if (mode == L"default")
                claim.mode = Ports::BindMode::Default;
            else if (mode == L"reuse")
                claim.mode = Ports::BindMode::Reuse;
            else if (mode == L"exclusive")
                claim.mode = Ports::BindMode::Exclusive;
            else {
                LOGE(L"Payload: port claim '" + entry + L"' has an unknown bind mode (" +
                     parts[2] + L"); skipped.");
                continue;
            }
        }
        if (parts.size() > 3) {
            LOGE(L"Payload: port claim '" + entry + L"' has trailing fields; skipped.");
            continue;
        }

        // The address ends at the LAST colon, except for a bracketed literal, where it
        // ends at the bracket. This is what makes "[::1]:53" parse and what stops
        // "::1:53" from being read as address "::1:5" and port "3" — a form that is
        // genuinely ambiguous is refused rather than resolved by a guess.
        std::wstring address;
        std::wstring portText;
        if (!endpoint.empty() && endpoint.front() == L'[') {
            const size_t close = endpoint.find(L']');
            if (close == std::wstring::npos || close + 1 >= endpoint.size() ||
                endpoint[close + 1] != L':') {
                LOGE(L"Payload: port claim '" + entry +
                     L"' has a malformed bracketed address; skipped.");
                continue;
            }
            address = endpoint.substr(1, close - 1);
            portText = endpoint.substr(close + 2);
        } else {
            const size_t colon = endpoint.rfind(L':');
            if (colon == std::wstring::npos) {
                LOGE(L"Payload: port claim '" + entry + L"' has no port; skipped.");
                continue;
            }
            address = endpoint.substr(0, colon);
            portText = endpoint.substr(colon + 1);
        }

        if (address.empty() || portText.empty()) {
            LOGE(L"Payload: port claim '" + entry + L"' has no address or no port; skipped.");
            continue;
        }

        // Parsed by hand rather than through Ini::Int: that reads a value out of a
        // parsed file, and this is a whole string known to be digits. Doing it here
        // is also what makes "80abc" and " 80" non-ports, which the converting
        // parser would happily accept as 80.
        uint32_t port = 0;
        bool valid = !portText.empty();
        for (wchar_t c : portText) {
            if (c < L'0' || c > L'9') {
                valid = false;
                break;
            }
            port = port * 10u + static_cast<uint32_t>(c - L'0');
            if (port > 65535u) {
                valid = false;
                break;
            }
        }
        if (!valid || port < 1) {
            LOGE(L"Payload: port claim '" + entry + L"' has an invalid port; skipped.");
            continue;
        }

        claim.address = address;
        claim.port = static_cast<uint16_t>(port);
        claims.push_back(std::move(claim));
    }

    return claims;
}

}  // namespace Payload
