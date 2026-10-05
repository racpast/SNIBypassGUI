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
// The DNSCrypt certificate, and the cache that holds one per provider.
//
// This lives in its own header rather than inside dnscrypt_client.cpp because the
// cache has an owner: it belongs to the DNS forwarder's state, alongside the
// sockets and the worker pool it serves, and is created and destroyed with the
// session that uses it. A cache with static storage duration outlived every
// object that referenced it — constructed before main, with a mutex and two maps
// alive for the whole process — which is out of keeping with how the rest of this
// codebase treats lifetime (see CancelToken and SocketHandle, which are exact
// about it). Threading it through the caller needs the type in a header, so the
// types it is made of come with it.
//
// The client exposes it as an opaque pointer: `QueryDNSCrypt` takes a
// `CertCache*` and nothing else in that signature depends on anything here.
//
// Thread-safety: every entry point is safe to call concurrently from any number
// of threads, including several asking for the same provider at once.
#include <algorithm>
#include <atomic>
#include <chrono>
#include <condition_variable>
#include <cstddef>
#include <cstdint>
#include <iterator>
#include <map>
#include <memory>
#include <mutex>
#include <string>

#include "dns/cancel.h"

namespace Dns {

// DNSCrypt protocol constants (dnscrypt-protocol draft-denis-dprive-dnscrypt)
//
// resolver-magic: fixed 8-byte constant in every encrypted response, used by the
// client to distinguish DNSCrypt responses from other traffic on the same endpoint.
inline constexpr uint8_t kResolverMagic[] = {0x72, 0x36, 0x66, 0x6e,
                                             0x76, 0x57, 0x6a, 0x38};  // "r6fnvWj8"
inline constexpr size_t kResolverMagicLen = 8;

// client-magic: 8-byte per-certificate identifier, copied from the certificate into
// the first 8 bytes of every query. Each resolver's certificate carries a different
// value, so this cannot be a compile-time constant — it must be read from the cert.
inline constexpr size_t kClientMagicLen = 8;

inline constexpr size_t kPublicKeyLen = 32;
inline constexpr size_t kSecretKeyLen = 32;
inline constexpr size_t kNonceLen = 24;
inline constexpr size_t kHalfNonceLen = 12;
inline constexpr size_t kMacLen = 16;
inline constexpr uint8_t kCertMagic[] = {0x44, 0x4e, 0x53, 0x43};  // "DNSC"
inline constexpr size_t kCertMagicLen = 4;

// es-version values (encryption system)
inline constexpr uint16_t kEsVersionXSalsa20 = 0x0001;   // X25519-XSalsa20Poly1305
inline constexpr uint16_t kEsVersionXChacha20 = 0x0002;  // X25519-XChaCha20Poly1305

// Minimum certificate length for classic (non-PQ) DNSCrypt: cert-magic(4) +
// es-version(2) + minor(2) + signature(64) + resolver-pk(32) + client-magic(8) +
// serial(4) + ts-start(4) + ts-end(4) = 124 bytes (with zero-length extensions).
inline constexpr size_t kCertMinLen = 124;

// Certificate refresh interval: refresh 1 hour before expiration to avoid races
inline constexpr uint32_t kCertRefreshMargin = 3600;

// DNSCrypt certificate
struct DNSCryptCert {
    uint16_t esVersion = 0;  // Encryption system version (0x0001 or 0x0002)
    uint8_t serverPublicKey[kPublicKeyLen] = {};  // Resolver's X25519 public key
    uint8_t clientMagic[kClientMagicLen] = {};    // Certificate identifier for queries
    uint32_t serial = 0;
    uint32_t tsStart = 0;
    uint32_t tsEnd = 0;
    bool valid = false;
};

// Certificate cache entry
struct CachedCert {
    DNSCryptCert cert;
    uint32_t fetchTime = 0;  // When we fetched it (for refresh logic)
};

// The certificate cache, keyed by provider name, with in-flight deduplication,
// sweep-on-insert and a cooldown for providers that keep failing.
//
// Four problems are solved together here, because the same lock answers all of
// them.
//
// A cold start asks every enabled upstream at once, and several upstreams can
// name the same provider. Without deduplication each racing task fetches the
// provider's certificate separately: up to nineteen round trips for one
// certificate, all of them redundant, on the path a user is waiting behind.
// A task that finds a fetch already running waits for that fetch instead of
// starting a second one.
//
// And entries would otherwise accumulate forever. Nothing ever removes a
// provider that stops being used — a rebuild of the upstream list, a config
// edit, and the old entry stays for the life of the process. A sweep on insert
// drops anything that has been unusable for a while, which bounds the map by the
// number of providers that actually work rather than by every name ever seen.
//
// A failed fetch used to be cached nowhere, so a provider that was simply down
// was re-fetched on every single query: one network round trip per DNS lookup,
// forever, for a server that had already been given every chance to answer. A
// failure now records a deadline before which the provider is not tried again.
class CertCache {
public:
    // The certificate for `providerName`, from the cache or from the network.
    //
    // `fetch` performs the actual retrieval and runs with no lock held, so a slow
    // provider cannot stall either a cached lookup or a fetch of a different one.
    // It is called at most once per provider per round of waiters: the first
    // caller to miss becomes the fetcher, and everyone else waits on its result.
    //
    // A cancelled waiter stops waiting. It does not cancel the fetch — the result
    // is shared, and the fetcher may be a thread whose query is still live — so
    // the work is not wasted, it is simply collected by whoever wanted it.
    //
    // An invalid result is returned immediately, without calling `fetch`, while
    // the provider is inside its cooldown.
    template <typename Fetch>
    DNSCryptCert Get(const std::string& providerName, uint32_t now, const CancelToken* cancel,
                     Fetch&& fetch) {
        std::shared_ptr<Pending> pending;

        {
            std::lock_guard<std::mutex> lock(m_mx);
            SweepLocked(now);

            const auto cached = m_entries.find(providerName);
            if (cached != m_entries.end() && Usable(cached->second, now)) {
                return cached->second.cert;
            }

            const auto cooling = m_failures.find(providerName);
            if (cooling != m_failures.end() && !Past(now, cooling->second.retryAfter)) {
                return DNSCryptCert{};
            }

            const auto running = m_pending.find(providerName);
            if (running != m_pending.end()) {
                pending = running->second;  // someone else is already fetching
            } else {
                // The caller that installs the slot is the caller that fetches.
                // Ownership is expressed by the slot being absent, not by a flag
                // on it, so there is no state two threads could both think they
                // set. A second caller racing this one finds the slot and waits.
                pending = std::make_shared<Pending>();
                m_pending.emplace(providerName, pending);
            }
        }

        // No lock is held from here on, so a slow provider stalls nothing else.
        if (pending->owner.load(std::memory_order_acquire)) {
            const DNSCryptCert cert = fetch();

            {
                std::lock_guard<std::mutex> lock(m_mx);
                m_pending.erase(providerName);

                if (cert.valid) {
                    CachedCert entry;
                    entry.cert = cert;
                    entry.fetchTime = now;
                    m_entries[providerName] = entry;
                    m_failures.erase(providerName);
                } else {
                    // Record the failure and lengthen the next wait for this
                    // provider. The first cooldown is short enough that an
                    // upstream returning after a blip is back in service on the
                    // next query or the one after; each further failure doubles
                    // it, so a provider that is truly gone costs one probe per
                    // kMaxFailureBackoffSeconds instead of one per query.
                    Failure& failure = m_failures[providerName];
                    const uint32_t shift = failure.strikes < kMaxFailureBackoffShift
                                               ? failure.strikes
                                               : kMaxFailureBackoffShift;
                    const uint32_t cooldown =
                        std::min(kFailureBackoffSeconds << shift, kMaxFailureBackoffSeconds);
                    if (failure.strikes < kMaxFailureBackoffShift) ++failure.strikes;
                    failure.retryAfter = now + cooldown;
                }
            }

            // Published under the waiter's own lock and only then marked done, so
            // a waiter that sees done also sees the result that goes with it. The
            // slot is a shared_ptr the waiter already holds, so this does not
            // depend on the map entry still being there.
            {
                std::lock_guard<std::mutex> lock(pending->mx);
                pending->result = cert;
                pending->done.store(true, std::memory_order_release);
            }
            pending->cv.notify_all();
            return cert;
        }

        // A waiter. Sleeps until the fetcher publishes, re-reading the cancel
        // flag each pass so abandoning a query does not have to wait out a
        // provider that may take the whole timeout to answer.
        std::unique_lock<std::mutex> lock(pending->mx);
        while (!pending->done.load(std::memory_order_acquire)) {
            if (cancel != nullptr && cancel->Cancelled()) return DNSCryptCert{};
            pending->cv.wait_for(lock, std::chrono::milliseconds(kWaitPollMs));
        }
        return pending->result;
    }

private:
    // How often a waiter re-reads the cancel flag while a fetch is running.
    static constexpr uint32_t kWaitPollMs = 50;

    // How long an expired entry is kept past its end before the next sweep drops
    // it. Long enough that a provider renewing its certificate is not dropped and
    // re-fetched in the meantime; short enough that the map stays bounded.
    static constexpr uint32_t kStaleGraceSeconds = 3600;

    // How long a provider is left alone after its first failed fetch, and the
    // ceiling on how long repeated failures stretch that. The first is close to
    // the query timeout, so a provider that was merely slow returns on the very
    // next query rather than after an arbitrary absence; the ceiling keeps a
    // permanently dead provider from being given up on so thoroughly that a
    // manual config change appears to have no effect.
    static constexpr uint32_t kFailureBackoffSeconds = 30;
    static constexpr uint32_t kMaxFailureBackoffSeconds = 600;

    // Largest number of doublings, which also bounds the strike counter. Five
    // doublings of the first cooldown run 30/60/120/240/480 and the sixth is
    // clamped to the ceiling, so this is the shift at which the ceiling starts to
    // bind rather than a value the progression never reaches.
    static constexpr uint32_t kMaxFailureBackoffShift = 5;

    // How long a failure record outlives its own cooldown before the sweep drops
    // it. Long enough that a provider failing repeatedly keeps climbing the
    // backoff, short enough that a provider that comes back is not remembered as
    // broken for the life of the process.
    static constexpr uint32_t kFailureForgetSeconds = 86400;

    // One provider that failed, and when it may be tried again.
    struct Failure {
        uint32_t strikes = 0;
        uint32_t retryAfter = 0;
    };

    // One fetch in progress, and the result it will hand its waiters.
    //
    // Held by shared_ptr because the waiters have to reach it without the map:
    // the fetcher removes the map entry before it publishes, so a waiter that
    // looked the entry up again could miss it entirely. Holding the slot itself
    // is what makes the hand-off independent of the map's contents.
    struct Pending {
        // True only on the slot its creator installed. Waiters get false, which
        // is what routes them to the wait path instead of the network.
        std::atomic<bool> owner{true};

        std::mutex mx;
        std::condition_variable cv;
        std::atomic<bool> done{false};
        DNSCryptCert result;
    };

    // Certificate timestamps are 32-bit seconds, so all of them are compared in
    // the same unsigned wrap-around domain the fields themselves are in: `now >
    // deadline` is how a deadline one wrap in the future reads as past rather
    // than as decades away.
    static bool Past(uint32_t now, uint32_t deadline) { return now > deadline; }

    static bool Usable(const CachedCert& entry, uint32_t now) {
        return entry.cert.valid && now >= entry.cert.tsStart && now <= entry.cert.tsEnd &&
               (entry.cert.tsEnd - now) > kCertRefreshMargin;
    }

    void SweepLocked(uint32_t now) {
        for (auto it = m_entries.begin(); it != m_entries.end();) {
            const uint32_t tsEnd = it->second.cert.tsEnd;
            // tsEnd is a wrap-around-prone 32-bit timestamp, so the age is
            // computed in the same unsigned domain the field itself is in.
            const bool longDead = now > tsEnd && (now - tsEnd) > kStaleGraceSeconds;
            it = longDead ? m_entries.erase(it) : std::next(it);
        }

        // Failure records are swept on a much longer clock than their cooldown,
        // and this is the whole reason the backoff works. A provider is retried
        // as soon as its cooldown expires, but the record has to outlive that
        // retry: erasing it when the cooldown lapsed would clear the strike count
        // and make every failure a first failure, so 30s, 30s, 30s, 30s would be
        // the entire progression and the doubling above would never be reached.
        for (auto it = m_failures.begin(); it != m_failures.end();) {
            it = Past(now, it->second.retryAfter + kFailureForgetSeconds) ? m_failures.erase(it)
                                                                          : std::next(it);
        }
    }

    std::mutex m_mx;
    std::map<std::string, CachedCert> m_entries;
    // Providers with a fetch outstanding, each with the slot its waiters hold a
    // reference to. The fetcher erases the entry once the result is published.
    std::map<std::string, std::shared_ptr<Pending>> m_pending;
    // Providers that failed to produce a certificate, with the backoff that keeps
    // a dead one from costing a network round trip on every query.
    std::map<std::string, Failure> m_failures;
};

}  // namespace Dns
