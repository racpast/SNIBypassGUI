// Copyright © 2026 Racpast. All Rights Reserved.
//
// This file is part of SNIBypassGUI, a proprietary software project.
//
// NOTICE: All information contained herein is, and remains the property of
// Racpast. The intellectual and technical concepts contained herein are
// proprietary to Racpast. All rights reserved.
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
#include <map>
#include <utility>

#include "app/logging.h"
#include "app/text.h"
#include "dns/dnsstamp.h"
#include "dns/network_utils.h"
#include "platform/ini.h"

namespace Dns {
namespace {

// The section prefixes, in one place because each appears in two functions: the
// collector that finds them and the report that names what was found.
constexpr const wchar_t* kUpstreamPrefix = L"Upstream.";
constexpr const wchar_t* kPoolPrefix = L"Pool.";
constexpr const wchar_t* kListenerPrefix = L"Listener.";

// A listener is one select() loop's worth of descriptors away from the fd_set
// bound, so the count is capped; see kMaxListeners in the header, which dns_proxy.cpp
// reads too. This file is where the cap is enforced, so a configuration that asks for
// more is reported by section name rather than surfacing as a bind failure.

// Split a comma-separated list, trimming each item and dropping empties.
//
// Commas rather than the '|' the payload uses for its own lists: this file's values
// are upstream names, the separator is the file's own convention, and one file
// having one separator is worth more than matching a different file's.
std::vector<std::wstring> SplitNames(const std::wstring& value) {
    std::vector<std::wstring> out;
    size_t start = 0;
    for (;;) {
        const size_t comma = value.find(L',', start);
        std::wstring token =
            TrimW(comma == std::wstring::npos ? value.substr(start)
                                              : value.substr(start, comma - start));
        if (!token.empty()) out.push_back(std::move(token));
        if (comma == std::wstring::npos) break;
        start = comma + 1;
    }
    return out;
}

// The default port for a transport that did not state one. A stamp carries an
// address and usually a port; when it states only the address, the transport's own
// convention is what it meant, and 443 is DoH's.
uint16_t DefaultPortFor(DnsProxyProtocol protocol) {
    switch (protocol) {
        case DnsProxyProtocol::DoT: return 853;
        case DnsProxyProtocol::PlainDNS: return 53;
        case DnsProxyProtocol::DoH:
        case DnsProxyProtocol::DNSCrypt:
        case DnsProxyProtocol::Unknown: break;
    }
    return 443;
}

// Parse one [Upstream.NAME] section into an endpoint. False means the section was
// skipped, with the reason already logged: a stamp that is absent, unparseable, or
// of a protocol this proxy does not speak.
bool ParseUpstream(const std::vector<Ini::Section>& ini, const std::wstring& section,
                   const std::wstring& name, DnsProxyEndpoint& out) {
    const std::wstring stamp = Ini::Value(ini, section.c_str(), L"Stamp");
    if (stamp.empty()) {
        LOGW(L"DNS forwarder config: [" + section + L"] has no Stamp; skipped.");
        return false;
    }

    const DNSStamp parsed = ParseDNSStamp(stamp);
    if (!parsed.valid) {
        LOGW(L"DNS forwarder config: [" + section + L"] has an invalid DNSStamp; skipped.");
        return false;
    }

    out = DnsProxyEndpoint();
    out.name = name;

    switch (parsed.protocol) {
        case StampProtocol::PlainDNS:
            out.protocol = DnsProxyProtocol::PlainDNS;
            out.address = parsed.address;
            break;
        case StampProtocol::DoH:
            out.protocol = DnsProxyProtocol::DoH;
            out.address = parsed.address;
            out.hostname = parsed.hostname;
            out.path = parsed.path;
            out.certificateHashes = parsed.hashes;
            break;
        case StampProtocol::DoT:
            out.protocol = DnsProxyProtocol::DoT;
            out.address = parsed.address;
            out.hostname = parsed.hostname;
            out.certificateHashes = parsed.hashes;
            break;
        case StampProtocol::DNSCrypt:
            out.protocol = DnsProxyProtocol::DNSCrypt;
            out.address = parsed.address;
            out.providerName = parsed.providerName;
            out.publicKey = parsed.publicKey;
            break;
        default:
            LOGW(L"DNS forwarder config: [" + section + L"] has an unknown protocol; skipped.");
            return false;
    }

    // The endpoint has to be numeric. A stamp naming a hostname would need exactly
    // the lookup this program exists to be in the middle of, so it is refused here
    // rather than left to fail on the first query.
    NetworkUtils::IpEndpoint endpoint;
    if (!NetworkUtils::ParseIpEndpoint(out.address, DefaultPortFor(out.protocol), endpoint)) {
        LOGW(L"DNS forwarder config: [" + section +
             L"] is not a numeric address:port; skipped.");
        return false;
    }
    return true;
}

// The listeners a file declares, resolved against the upstreams and pools that
// parsed. Order is the file's.
std::vector<DnsProxyListener> ResolveListeners(
    const std::vector<Ini::Section>& ini, const std::vector<DnsProxyEndpoint>& upstreams,
    const std::map<std::wstring, std::vector<size_t>>& pools) {
    std::vector<DnsProxyListener> listeners;

    for (const Ini::Section& section : ini) {
        if (section.name.find(kListenerPrefix) != 0) continue;
        if (listeners.size() >= kMaxListeners) {
            LOGW(L"DNS forwarder config: more than " + std::to_wstring(kMaxListeners) +
                 L" listeners declared; [" + section.name + L"] ignored.");
            continue;
        }

        DnsProxyListener listener;
        listener.name = section.name.substr(std::wstring(kListenerPrefix).length());

        // The address has no default. A listener is a declaration of where to bind
        // and what to race, and one with no address is not a listener — silently
        // substituting the old compiled-in endpoint would turn a typo in the file
        // into a listener the user did not ask for.
        listener.address = TrimW(Ini::Value(ini, section.name.c_str(), L"Address"));
        if (listener.address.empty()) {
            LOGW(L"DNS forwarder config: [" + section.name + L"] has no Address; skipped.");
            continue;
        }

        // Validated here so that a value the parser accepted cannot fail at bind
        // time for a reason the log will not name. The port default is 53: this is a
        // DNS proxy, and the alternative would be to make every declaration repeat
        // the one value almost all of them want.
        const int port = Ini::Int(ini, section.name.c_str(), L"Port", 53);
        if (port < 1 || port > 65535) {
            LOGW(L"DNS forwarder config: [" + section.name + L"] has an invalid Port (" +
                 std::to_wstring(port) + L"); skipped.");
            continue;
        }
        listener.port = static_cast<uint16_t>(port);

        // Which upstreams to race. A Pool= names one; its absence means every
        // upstream in the file, which is what a single-listener configuration
        // means and what its author has already written down by listing them.
        const std::wstring poolName = TrimW(Ini::Value(ini, section.name.c_str(), L"Pool"));
        if (poolName.empty()) {
            listener.upstreams = upstreams;
        } else {
            const auto pool = pools.find(LowerW(poolName));
            if (pool == pools.end()) {
                LOGW(L"DNS forwarder config: [" + section.name + L"] names Pool " + poolName +
                     L", which is not declared; skipped.");
                continue;
            }
            // Copied rather than referenced: the listener owns its set, so nothing
            // downstream has to hold the pool table alive.
            for (size_t index : pool->second) listener.upstreams.push_back(upstreams[index]);
        }

        if (listener.upstreams.empty()) {
            LOGW(L"DNS forwarder config: [" + section.name +
                 L"] resolved to no upstream; skipped, because a listener with nothing to "
                 L"forward to would answer every query with a failure.");
            continue;
        }

        listeners.push_back(std::move(listener));
    }

    return listeners;
}

}  // namespace

DnsProxyConfig DnsProxyConfig::Load(const std::wstring& path) {
    DnsProxyConfig cfg;

    // Parsed once: this file is read for a timeout, a pool size, every upstream's
    // stamp, and every listener, and re-reading it per key would repeat the whole
    // file decode each time.
    const std::vector<Ini::Section> ini = Ini::Read(path);
    if (ini.empty()) {
        LOGE(L"DNS forwarder config: cannot read " + path);
        return cfg;
    }

    cfg.timeoutMs = static_cast<uint32_t>(
        std::clamp<int>(Ini::Int(ini, L"General", L"TimeoutMs", 3000), 100, 60000));

    // Clamped rather than refused: a pool of zero would deadlock the proxy, and a
    // pool larger than any query needs is only wasted memory. Both are config
    // mistakes worth surviving, not fatal ones.
    cfg.threadPoolSize = static_cast<size_t>(
        std::clamp<int>(Ini::Int(ini, L"General", L"ThreadPoolSize", 32), 1, 256));

    // Pass 1: the upstreams, each independently usable or skipped. Done first
    // because both of the sections below refer to them by name, and a name can only
    // be resolved once it exists.
    std::map<std::wstring, size_t> upstreamIndex;  // lowercased name -> index
    for (const Ini::Section& section : ini) {
        if (section.name.find(kUpstreamPrefix) != 0) continue;

        const std::wstring name = section.name.substr(std::wstring(kUpstreamPrefix).length());
        DnsProxyEndpoint endpoint;
        if (!ParseUpstream(ini, section.name, name, endpoint)) continue;

        // A repeated name keeps the first: the second would be unreachable by name
        // anyway, and silently letting it win would make the pool that refers to it
        // depend on file order in a way nobody could see.
        const std::wstring key = LowerW(name);
        if (upstreamIndex.count(key) != 0) {
            LOGW(L"DNS forwarder config: upstream " + name +
                 L" is declared twice; the "
                 L"second is ignored. Pool entries resolve to the first.");
            continue;
        }
        upstreamIndex.emplace(key, cfg.upstreams.size());
        cfg.upstreams.push_back(std::move(endpoint));
    }

    // Pass 2: the pools, each a list of upstream names.
    std::map<std::wstring, std::vector<size_t>> pools;
    for (const Ini::Section& section : ini) {
        if (section.name.find(kPoolPrefix) != 0) continue;

        const std::wstring poolName = section.name.substr(std::wstring(kPoolPrefix).length());
        std::vector<size_t> members;
        for (const std::wstring& member :
             SplitNames(Ini::Value(ini, section.name.c_str(), L"Upstreams"))) {
            const auto found = upstreamIndex.find(LowerW(member));
            if (found == upstreamIndex.end()) {
                LOGW(L"DNS forwarder config: [" + section.name + L"] names upstream " + member +
                     L", which is not declared; ignored.");
                continue;
            }
            members.push_back(found->second);
        }

        if (members.empty()) {
            LOGW(L"DNS forwarder config: [" + section.name +
                 L"] lists no usable upstream; skipped.");
            continue;
        }

        const std::wstring key = LowerW(poolName);
        if (pools.count(key) != 0) {
            LOGW(L"DNS forwarder config: pool " + poolName +
                 L" is declared twice; the "
                 L"first is used.");
            continue;
        }
        pools.emplace(key, std::move(members));
    }

    // Pass 3: the listeners, which is where the two above are brought together.
    cfg.listeners = ResolveListeners(ini, cfg.upstreams, pools);

    size_t raced = 0;
    for (const DnsProxyListener& listener : cfg.listeners) raced += listener.upstreams.size();
    LOGI(L"DNS forwarder config loaded: " + std::to_wstring(cfg.upstreams.size()) +
         L" upstream(s), " + std::to_wstring(pools.size()) + L" pool(s), " +
         std::to_wstring(cfg.listeners.size()) + L" listener(s) racing " +
         std::to_wstring(raced) + L" endpoint(s).");

    return cfg;
}

}  // namespace Dns
