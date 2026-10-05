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

#include "dns/dns_proxy_config.h"

#include <windows.h>

#include <algorithm>

#include "app/logging.h"
#include "app/text.h"
#include "dns/dnsstamp.h"
#include "dns/network_utils.h"
#include "platform/ini.h"

namespace Dns {
namespace {

// Every section name in an INI file, in file order.
std::vector<std::wstring> EnumerateSections(const std::vector<Ini::Section>& sections) {
    std::vector<std::wstring> names;
    names.reserve(sections.size());
    for (const Ini::Section& section : sections) names.push_back(section.name);
    return names;
}

}  // namespace

DnsProxyConfig DnsProxyConfig::Load(const std::wstring& path) {
    DnsProxyConfig cfg;

    // Parsed once: this file is read for a timeout, a pool size, and every upstream's
    // stamp, and re-reading it per key would repeat the whole file decode each time.
    const std::vector<Ini::Section> ini = Ini::Read(path);

    cfg.timeoutMs = Ini::Int(ini, L"General", L"TimeoutMs", 3000);

    // Clamped rather than refused: a pool of zero would deadlock the proxy, and a
    // pool larger than any query needs is only wasted memory. Both are config
    // mistakes worth surviving, not fatal ones.
    cfg.threadPoolSize = static_cast<size_t>(
        std::clamp<int>(Ini::Int(ini, L"General", L"ThreadPoolSize", 32), 1, 256));

    const std::vector<std::wstring> sections = EnumerateSections(ini);

    const std::wstring upstreamPrefix = L"Upstream.";
    for (const std::wstring& section : sections) {
        if (section.find(upstreamPrefix) != 0) continue;

        const std::wstring name = section.substr(upstreamPrefix.length());

        const bool enabled = Ini::Int(ini, section.c_str(), L"Enabled", 0) != 0;

        const std::wstring stamp = Ini::Value(ini, section.c_str(), L"Stamp");
        if (stamp.empty()) {
            LOGW(L"DNS forwarder config: [" + section + L"] missing Stamp, skipped");
            continue;
        }

        const DNSStamp parsed = ParseDNSStamp(stamp);
        if (!parsed.valid) {
            LOGW(L"DNS forwarder config: [" + section + L"] invalid DNSStamp, skipped");
            continue;
        }

        DnsProxyEndpoint up;
        up.name = name;
        up.enabled = enabled;
        switch (parsed.protocol) {
            case StampProtocol::PlainDNS:
                up.protocol = DnsProxyProtocol::PlainDNS;
                up.address = parsed.address;
                break;
            case StampProtocol::DoH:
                up.protocol = DnsProxyProtocol::DoH;
                up.address = parsed.address;
                up.hostname = parsed.hostname;
                up.path = parsed.path;
                up.certificateHashes = parsed.hashes;
                break;
            case StampProtocol::DoT:
                up.protocol = DnsProxyProtocol::DoT;
                up.address = parsed.address;
                up.hostname = parsed.hostname;
                up.certificateHashes = parsed.hashes;
                break;
            case StampProtocol::DNSCrypt:
                up.protocol = DnsProxyProtocol::DNSCrypt;
                up.address = parsed.address;
                up.providerName = parsed.providerName;
                up.publicKey = parsed.publicKey;
                break;
            default:
                LOGW(L"DNS forwarder config: [" + section + L"] unknown protocol, skipped");
                continue;
        }

        // Each transport has its own conventional port, and a stamp that omits
        // one means that transport's default rather than 443.
        uint16_t defaultPort = 443;
        if (up.protocol == DnsProxyProtocol::DoT) defaultPort = 853;
        if (up.protocol == DnsProxyProtocol::PlainDNS) defaultPort = 53;
        NetworkUtils::IpEndpoint endpoint;
        if (!NetworkUtils::ParseIpEndpoint(up.address, defaultPort, endpoint)) {
            LOGW(L"DNS forwarder config: [" + section + L"] invalid numeric endpoint, skipped");
            continue;
        }

        cfg.upstreams.push_back(up);
    }

    const size_t enabledCount = cfg.EnabledCount();
    LOGI(L"DNS forwarder config loaded: " + std::to_wstring(cfg.upstreams.size()) +
         L" upstream(s), " + std::to_wstring(enabledCount) + L" enabled");

    return cfg;
}

}  // namespace Dns
