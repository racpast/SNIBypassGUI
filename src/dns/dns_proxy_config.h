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
// The proxy binds one or more loopback listeners and forwards every query it
// receives to one or more upstream DNS servers (DoH/DoT/DNSCrypt/plain DNS),
// racing them and returning whichever answers first. It has no rules and never
// synthesizes answers — every query is forwarded.
//
// Three kinds of section, each introduced by its prefix, and the shape of the file
// is the shape of the thing:
//
//   [Upstream.NAME]   one server, described by a DNSStamp (sdns://...), which
//                     encodes protocol, address, hostname, path and key material
//                     in a compact string. See https://dnscrypt.info/stamps.
//   [Pool.NAME]       a set of upstreams, named by an Upstreams= list, which is
//                     the group a listener races against.
//   [Listener.NAME]   an address and port to bind, and the pool it races.
//
// That split is what lets one endpoint race encrypted transports while another
// races plain ones: 127.191.98.10:53 can serve DoH/DoT/DNSCrypt while
// 127.191.98.10:5353 serves plain DNS, and nginx can then be pointed at whichever
// it needs. A listener with no Pool= races every upstream in the file, which is
// the one-listener case and is what a file that declares no pools means.
//
// There is no Enabled= key. A section's presence is the declaration: an upstream
// listed in a pool is used, an upstream in no pool is used by every listener that
// names no pool, and a listener with no address is not a listener. A key that
// turned each section on and off was a second way to say something the file
// already said, and it bought nothing — commenting a section out, or deleting it,
// is what "off" means in a file a human maintains.
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
};

// The most listeners one configuration may declare.
//
// A bound rather than a preference, and the reason is the select() budget: every
// listener costs two sockets in the one fd_set the event loop watches — its UDP and
// its TCP — so this number is half of an arithmetic that must fit in FD_SETSIZE,
// beside the client sessions sharing the same array. dns_proxy.cpp asserts that sum;
// this is the constraint the parser enforces so a file asking for more is reported
// by name rather than by an obscure bind failure later.
inline constexpr size_t kMaxListeners = 8;

// One bound endpoint: where to listen, and what to race.
//
// `upstreams` is a resolved snapshot — the endpoints the named pool held at load
// time — rather than a pool name, so the loop never has to look anything up and a
// listener cannot end up racing a set that changed underneath a query already in
// flight.
struct DnsProxyListener {
    std::wstring name;
    std::wstring address;  // literal, as written in the file
    uint16_t port = 53;
    std::vector<DnsProxyEndpoint> upstreams;
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

    // Every upstream the file declares, in file order, whether or not a pool names
    // it. Kept because a listener with no Pool= races this list.
    std::vector<DnsProxyEndpoint> upstreams;

    // Every listener that resolved to at least one upstream, in file order. A
    // listener whose pool is empty or unnameable is not here, because a listener
    // that cannot forward anything is not a listener this proxy can serve.
    std::vector<DnsProxyListener> listeners;

    // Load from the INI file at `path`.
    //
    // Returns a config with no listeners if the file cannot be read, declares no
    // usable listener, or every listener it declares resolves to no upstream —
    // all of which mean there is nothing to forward to, and the proxy refuses to
    // start rather than binding a socket that answers nothing.
    static DnsProxyConfig Load(const std::wstring& path);
};

}  // namespace Dns
