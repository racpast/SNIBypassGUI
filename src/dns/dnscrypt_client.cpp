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

#include "dns/dnscrypt_client.h"

#include <winsock2.h>
#include <ws2tcpip.h>

#include <windows.h>

#include <sodium.h>

#include <algorithm>
#include <atomic>
#include <condition_variable>
#include <cstring>
#include <ctime>
#include <map>
#include <memory>
#include <mutex>

#include "app/logging.h"
#include "app/text.h"
#include "dns/dnscrypt_cert_cache.h"
#include "dns/message.h"
#include "dns/network_utils.h"
#include "dns/socket_utils.h"
#include "dns/tcp_session.h"

namespace Dns {
namespace {

// Apply ISO/IEC 7816-4 padding: append 0x80, then pad with 0x00 to reach minSize.
// This padding scheme provides an unambiguous delimiter so the receiver can strip
// padding without knowing the original message length in advance.
std::vector<uint8_t> Pad(const std::vector<uint8_t>& data, size_t minSize) {
    std::vector<uint8_t> padded = data;
    padded.push_back(0x80);
    while (padded.size() < minSize) {
        padded.push_back(0x00);
    }
    return padded;
}

// Remove ISO/IEC 7816-4 padding: scan backward for 0x80, verify all bytes after the
// data are 0x00, then strip from the 0x80 onward. A packet that does not end with
// 0x80 followed by zero or more 0x00 is rejected — the padding delimiter is mandatory.
bool Unpad(std::vector<uint8_t>& data) {
    for (size_t i = data.size(); i > 0; --i) {
        const size_t idx = i - 1;
        if (data[idx] == 0x80) {
            data.resize(idx);
            return true;
        } else if (data[idx] != 0x00) {
            return false;  // Invalid padding: non-zero byte before delimiter
        }
    }
    return false;  // Invalid padding: no delimiter found
}

// Build DNS query for provider name (TXT record)
std::vector<uint8_t> BuildCertQuery(const std::string& providerName) {
    std::vector<uint8_t> query;

    // DNS header
    query.resize(12, 0);
    query[0] = 0x00;
    query[1] = 0x00;  // Transaction ID: 0
    query[2] = 0x01;
    query[3] = 0x00;  // Standard query
    query[4] = 0x00;
    query[5] = 0x01;  // 1 question
    // ANCOUNT, NSCOUNT, ARCOUNT = 0

    // Question: the provider name as DNS labels, through the shared encoder so
    // a label over 63 bytes or a name over 255 is refused here rather than
    // written as a byte the server reads as something else. An empty result is
    // the caller's signal that no query can be built.
    if (!EncodeName(providerName, query)) return {};

    // QTYPE = TXT (16)
    query.push_back(0x00);
    query.push_back(0x10);

    // QCLASS = IN (1)
    query.push_back(0x00);
    query.push_back(0x01);

    return query;
}

// Parse DNSCrypt certificate from TXT record data.
//
// The certificate format is:
//   cert-magic(4) + es-version(2) + protocol-minor(2) + signature(64) +
//   resolver-pk(32) + client-magic(8) + serial(4) + ts-start(4) + ts-end(4) +
//   extensions(variable)
//
// The signature covers everything from resolver-pk onward (offset 72+).
// We support es-version 0x0001 (XSalsa20) and 0x0002 (XChaCha20).
DNSCryptCert ParseCertificate(const uint8_t* certData, size_t certLen,
                              const std::vector<uint8_t>& providerPublicKey) {
    DNSCryptCert cert;

    if (certLen < kCertMinLen) return cert;
    if (std::memcmp(certData, kCertMagic, kCertMagicLen) != 0) return cert;

    // Read es-version (big-endian uint16 at offset 4)
    const uint16_t esVersion =
        (static_cast<unsigned>(certData[4]) << 8u) | static_cast<unsigned>(certData[5]);

    // Only accept XSalsa20 (0x0001) or XChaCha20 (0x0002)
    if (esVersion != kEsVersionXSalsa20 && esVersion != kEsVersionXChacha20) {
        return cert;
    }

    // protocol-minor-version (offset 6, 2 bytes) is ignored per spec

    // Signature starts at offset 8 (64 bytes)
    const uint8_t* signature = certData + 8;

    // Signed data starts at offset 72 (after cert-magic + es-version + minor + signature)
    const uint8_t* signedData = certData + 72;
    const size_t signedLen = certLen - 72;

    // Verify Ed25519 signature using the provider's public key
    if (crypto_sign_verify_detached(signature, signedData, signedLen,
                                    providerPublicKey.data()) != 0) {
        return cert;
    }

    const uint8_t* ptr = signedData;

    // Resolver public key (32 bytes)
    std::memcpy(cert.serverPublicKey, ptr, kPublicKeyLen);
    ptr += kPublicKeyLen;

    // Client magic (8 bytes) - this is what the client copies into each query
    std::memcpy(cert.clientMagic, ptr, kClientMagicLen);
    ptr += kClientMagicLen;

    // Serial (4 bytes, big-endian)
    cert.serial = (static_cast<uint32_t>(ptr[0]) << 24u) |
                  (static_cast<uint32_t>(ptr[1]) << 16u) |
                  (static_cast<uint32_t>(ptr[2]) << 8u) | static_cast<uint32_t>(ptr[3]);
    ptr += 4;

    // ts-start (4 bytes, big-endian)
    cert.tsStart = (static_cast<uint32_t>(ptr[0]) << 24u) |
                   (static_cast<uint32_t>(ptr[1]) << 16u) |
                   (static_cast<uint32_t>(ptr[2]) << 8u) | static_cast<uint32_t>(ptr[3]);
    ptr += 4;

    // ts-end (4 bytes, big-endian)
    cert.tsEnd = (static_cast<uint32_t>(ptr[0]) << 24u) |
                 (static_cast<uint32_t>(ptr[1]) << 16u) |
                 (static_cast<uint32_t>(ptr[2]) << 8u) | static_cast<uint32_t>(ptr[3]);
    ptr += 4;

    // Validity: ts-start must be strictly less than ts-end, and current time must be
    // within [ts-start, ts-end] (inclusive on both ends).
    if (cert.tsStart >= cert.tsEnd) {
        return cert;
    }

    const uint32_t now = static_cast<uint32_t>(std::time(nullptr));
    if (now < cert.tsStart || now > cert.tsEnd) {
        return cert;
    }

    cert.esVersion = esVersion;
    cert.valid = true;
    return cert;
}

// Parse TXT RDATA from a DNS answer section.
//
// TXT RDATA is a sequence of <character-string> elements, each prefixed with a 1-byte
// length. Multiple character-strings within a single TXT record must be concatenated
// before interpreting the result as a certificate. Different TXT records represent
// different certificates and must NOT be concatenated together.
//
// Returns the concatenated certificate data, or an empty vector on parse failure.
std::vector<uint8_t> ParseTxtRdata(const uint8_t* rdata, size_t rdlen) {
    std::vector<uint8_t> result;
    size_t offset = 0;

    while (offset < rdlen) {
        if (offset + 1 > rdlen) return {};  // Truncated length byte

        const uint8_t len = rdata[offset];
        offset += 1;

        if (offset + len > rdlen) return {};  // Truncated character-string

        result.insert(result.end(), rdata + offset, rdata + offset + len);
        offset += len;
    }

    return result;
}

// Parse a DNS response and extract all TXT records from the answer section.
// Each TXT record is returned as a separate certificate candidate.
std::vector<std::vector<uint8_t>> ExtractTxtRecords(const uint8_t* response, size_t respLen) {
    std::vector<std::vector<uint8_t>> records;

    if (respLen < 12) return records;

    // Parse DNS header
    const uint16_t qdcount = (static_cast<unsigned>(response[4]) << 8u) | response[5];
    const uint16_t ancount = (static_cast<unsigned>(response[6]) << 8u) | response[7];

    size_t offset = 12;

    // Skip the question section. The name walk is the shared one, so a label that
    // runs past the end and a pointer that points forwards are both refused here
    // the same way they are everywhere else in this program — this used to have
    // its own pair of loops that stepped over a name without those checks, and
    // the step past a pointer could land beyond the message with only the record
    // bounds below catching it.
    for (uint16_t i = 0; i < qdcount && offset < respLen; ++i) {
        if (!SkipName(response, respLen, offset)) return records;
        if (offset + 4 > respLen) return records;
        offset += 4;  // QTYPE + QCLASS
    }

    // Parse answer section
    for (uint16_t i = 0; i < ancount && offset < respLen; ++i) {
        if (!SkipName(response, respLen, offset)) break;

        // Need at least TYPE(2) + CLASS(2) + TTL(4) + RDLENGTH(2) = 10 bytes
        if (offset + 10 > respLen) break;

        const uint16_t rrtype =
            (static_cast<unsigned>(response[offset]) << 8u) | response[offset + 1];
        offset += 2;

        // Skip CLASS (2) + TTL (4)
        offset += 6;

        const uint16_t rdlength =
            (static_cast<unsigned>(response[offset]) << 8u) | response[offset + 1];
        offset += 2;

        if (offset + rdlength > respLen) break;

        // If this is a TXT record (type 16), parse and extract the RDATA
        if (rrtype == 16) {
            std::vector<uint8_t> certData = ParseTxtRdata(response + offset, rdlength);
            if (!certData.empty()) {
                records.push_back(std::move(certData));
            }
        }

        offset += rdlength;
    }

    return records;
}

// Fetch certificate from server (tries UDP first, then TCP if truncated).
//
// The DNSCrypt spec requires both UDP and TCP support for certificate retrieval.
// If the UDP response has TC=1 (truncated), we must retry over TCP.
DNSCryptCert FetchCertificateFromNetwork(const NetworkUtils::IpEndpoint& endpoint,
                                         const std::string& providerName,
                                         const std::vector<uint8_t>& providerPublicKey,
                                         uint32_t timeoutMs,
                                         const CancelToken* cancel = nullptr) {
    DNSCryptCert cert;

    const std::vector<uint8_t> certQuery = BuildCertQuery(providerName);

    // Try UDP first
    SocketUtils::SocketHandle udpSock(
        socket(endpoint.address.ss_family, SOCK_DGRAM, IPPROTO_UDP));
    if (udpSock.IsValid()) {
        u_long mode = 1;
        ioctlsocket(udpSock, FIONBIO, &mode);

        // The token closes sockets it owns, so registration has to happen
        // before the first call that can block on this one. The handle remembers
        // the token, so this socket is closed exactly once by whichever of the
        // two gets there first.
        if (!udpSock.RegisterWith(cancel)) return cert;

        if (NetworkUtils::SendUdp(udpSock, certQuery,
                                  reinterpret_cast<const sockaddr*>(&endpoint.address),
                                  endpoint.length, timeoutMs / 2, cancel)) {
            const std::vector<uint8_t> certResponse =
                NetworkUtils::RecvUdp(udpSock, timeoutMs / 2, cancel);

            if (certResponse.size() >= 12) {
                // Check TC bit (bit 1 of flags, which is byte 2)
                const bool truncated = IsTruncated(certResponse.data(), certResponse.size());

                if (!truncated) {
                    // Try to parse the UDP response
                    const std::vector<std::vector<uint8_t>> txtRecords =
                        ExtractTxtRecords(certResponse.data(), certResponse.size());

                    // Select the best certificate: highest serial, prefer XChaCha20 over XSalsa20
                    uint32_t bestSerial = 0;
                    for (const auto& txtData : txtRecords) {
                        DNSCryptCert candidate =
                            ParseCertificate(txtData.data(), txtData.size(), providerPublicKey);
                        if (candidate.valid) {
                            if (candidate.serial > bestSerial ||
                                (candidate.serial == bestSerial && !cert.valid) ||
                                (candidate.serial == bestSerial &&
                                 candidate.esVersion > cert.esVersion)) {
                                cert = candidate;
                                bestSerial = candidate.serial;
                            }
                        }
                    }

                    if (cert.valid) {
                        return cert;
                    }
                }
            }
        }
    }

    // TCP fallback (either UDP failed, was truncated, or returned no valid cert)
    SocketUtils::SocketHandle tcpSock(
        socket(endpoint.address.ss_family, SOCK_STREAM, IPPROTO_TCP));
    if (!tcpSock.IsValid()) return cert;

    if (!tcpSock.RegisterWith(cancel)) return cert;

    if (!NetworkUtils::ConnectWithTimeout(tcpSock,
                                          reinterpret_cast<const sockaddr*>(&endpoint.address),
                                          endpoint.length, timeoutMs / 2, cancel)) {
        return cert;
    }

    // Send query with TCP length prefix (2-byte big-endian)
    const std::vector<uint8_t> tcpQuery = EncodeTcpMessage(certQuery);
    if (tcpQuery.empty()) return cert;

    if (!NetworkUtils::SendAll(tcpSock, tcpQuery, timeoutMs / 2, cancel)) return cert;

    const std::vector<uint8_t> certResponse =
        NetworkUtils::RecvLengthPrefixed(tcpSock, timeoutMs / 2, cancel);
    if (certResponse.size() < 12) return cert;

    // Parse TCP response
    const std::vector<std::vector<uint8_t>> txtRecords =
        ExtractTxtRecords(certResponse.data(), certResponse.size());

    uint32_t bestSerial = 0;
    for (const auto& txtData : txtRecords) {
        DNSCryptCert candidate =
            ParseCertificate(txtData.data(), txtData.size(), providerPublicKey);
        if (candidate.valid) {
            if (candidate.serial > bestSerial ||
                (candidate.serial == bestSerial && !cert.valid) ||
                (candidate.serial == bestSerial && candidate.esVersion > cert.esVersion)) {
                cert = candidate;
                bestSerial = candidate.serial;
            }
        }
    }

    return cert;
}

// Get certificate (from cache or network). Thread-safe.
//
// A cached certificate is reused while it is still valid (now within
// [tsStart, tsEnd]) and not approaching expiration (more than kCertRefreshMargin
// seconds remaining). Otherwise the shared cache fetches one, and concurrent
// callers asking for the same provider wait on that single fetch rather than
// each starting their own. A provider inside its failure cooldown yields an
// invalid certificate without a network round trip.
DNSCryptCert GetCertificate(CertCache& cache, const NetworkUtils::IpEndpoint& endpoint,
                            const std::string& providerName,
                            const std::vector<uint8_t>& providerPublicKey, uint32_t timeoutMs,
                            const CancelToken* cancel = nullptr) {
    const uint32_t now = static_cast<uint32_t>(std::time(nullptr));

    return cache.Get(providerName, now, cancel, [&]() {
        return FetchCertificateFromNetwork(endpoint, providerName, providerPublicKey, timeoutMs,
                                           cancel);
    });
}

}  // namespace

// Encrypt a DNS query using the given certificate and shared key.
// Returns the encrypted DNSCrypt packet, or empty vector on failure.
// Also returns the client nonce via output parameter for response validation.
static std::vector<uint8_t> EncryptQuery(const std::vector<uint8_t>& query,
                                         const DNSCryptCert& cert,
                                         const uint8_t* clientPublicKey,
                                         const uint8_t* sharedKey, uint8_t* clientNonceOut,
                                         bool useTcp) {
    // Apply ISO/IEC 7816-4 padding to the query
    // For UDP: target 256-byte total packet (188-byte plaintext after overhead)
    // For TCP: add random padding component per spec
    size_t minPlaintextLen = 188;
    if (useTcp) {
        // Add 0-255 bytes of random padding for TCP
        uint8_t randomPad = 0;
        randombytes_buf(&randomPad, 1);
        minPlaintextLen = query.size() + 1 + randomPad;
    }

    const size_t targetLen = std::max(minPlaintextLen, ((query.size() + 1 + 63) / 64) * 64);
    const std::vector<uint8_t> paddedQuery = Pad(query, targetLen);

    // Generate client nonce (12 random bytes)
    randombytes_buf(clientNonceOut, kHalfNonceLen);

    // Build the full 24-byte nonce for encryption: client-nonce || 12 NUL bytes
    uint8_t fullNonce[kNonceLen];
    std::memcpy(fullNonce, clientNonceOut, kHalfNonceLen);
    std::memset(fullNonce + kHalfNonceLen, 0, kHalfNonceLen);

    // Encrypt the padded query
    std::vector<uint8_t> encryptedQuery(paddedQuery.size() + kMacLen);

    if (cert.esVersion == kEsVersionXChacha20) {
        if (crypto_box_curve25519xchacha20poly1305_easy_afternm(
                encryptedQuery.data(), paddedQuery.data(), paddedQuery.size(), fullNonce,
                sharedKey) != 0) {
            return {};
        }
    } else {
        if (crypto_box_easy_afternm(encryptedQuery.data(), paddedQuery.data(),
                                    paddedQuery.size(), fullNonce, sharedKey) != 0) {
            return {};
        }
    }

    // Build DNSCrypt query packet:
    //   client-magic(8) + client-pk(32) + client-nonce(12) + encrypted-query
    std::vector<uint8_t> packet;
    packet.insert(packet.end(), cert.clientMagic, cert.clientMagic + kClientMagicLen);
    packet.insert(packet.end(), clientPublicKey, clientPublicKey + kPublicKeyLen);
    packet.insert(packet.end(), clientNonceOut, clientNonceOut + kHalfNonceLen);
    packet.insert(packet.end(), encryptedQuery.begin(), encryptedQuery.end());

    return packet;
}

// Decrypt a DNSCrypt response and validate it.
// Returns the decrypted DNS response, or empty vector on failure.
static std::vector<uint8_t> DecryptResponse(const std::vector<uint8_t>& response,
                                            const DNSCryptCert& cert,
                                            const uint8_t* clientNonce,
                                            const uint8_t* sharedKey) {
    // Response format: resolver-magic(8) + nonce(24) + encrypted-response
    // Minimum: 8 + 24 + 16(tag) + 12(min DNS) = 60 bytes
    const size_t minResponseLen = kResolverMagicLen + kNonceLen + kMacLen + 12;
    if (response.size() < minResponseLen) {
        return {};
    }

    // Verify resolver magic
    if (std::memcmp(response.data(), kResolverMagic, kResolverMagicLen) != 0) {
        return {};
    }

    // Extract response nonce: client-nonce(12) + resolver-nonce(12)
    const uint8_t* responseNonce = response.data() + kResolverMagicLen;

    // Verify client-nonce matches what we sent
    if (std::memcmp(responseNonce, clientNonce, kHalfNonceLen) != 0) {
        return {};
    }

    // Decrypt response
    const size_t ciphertextLen = response.size() - kResolverMagicLen - kNonceLen;
    std::vector<uint8_t> decrypted(ciphertextLen);

    if (cert.esVersion == kEsVersionXChacha20) {
        if (crypto_box_curve25519xchacha20poly1305_open_easy_afternm(
                decrypted.data(), response.data() + kResolverMagicLen + kNonceLen,
                ciphertextLen, responseNonce, sharedKey) != 0) {
            return {};
        }
    } else {
        if (crypto_box_open_easy_afternm(decrypted.data(),
                                         response.data() + kResolverMagicLen + kNonceLen,
                                         ciphertextLen, responseNonce, sharedKey) != 0) {
            return {};
        }
    }

    // Decryption writes plaintext.size() - tag bytes, so resize to actual plaintext length
    decrypted.resize(ciphertextLen - kMacLen);

    // Remove ISO/IEC 7816-4 padding
    if (!Unpad(decrypted)) {
        return {};
    }

    // Basic DNS response validation
    if (decrypted.size() < 12) {
        return {};
    }

    return decrypted;
}

std::vector<uint8_t> QueryDNSCrypt(const std::vector<uint8_t>& query,
                                   const std::string& address, const std::string& providerName,
                                   const std::vector<uint8_t>& publicKey, CertCache& cache,
                                   uint32_t timeoutMs, const CancelToken* cancel) {
    if (query.empty() || publicKey.size() != kPublicKeyLen) return {};

    // Initialize libsodium (idempotent)
    if (sodium_init() < 0) {
        LOGW(L"DNSCrypt: failed to initialize libsodium");
        return {};
    }

    NetworkUtils::IpEndpoint endpoint;
    if (!NetworkUtils::ParseIpEndpoint(address, 443, endpoint)) {
        LOGW(L"DNSCrypt: invalid IP endpoint: " + Utf8ToWide(address));
        return {};
    }

    // Get certificate (from cache or fetch fresh)
    const DNSCryptCert cert =
        GetCertificate(cache, endpoint, providerName, publicKey, timeoutMs, cancel);
    if (!cert.valid) {
        LOGW(L"DNSCrypt: failed to get valid certificate");
        return {};
    }

    // Generate client keypair
    uint8_t clientPublicKey[kPublicKeyLen];
    uint8_t clientSecretKey[kSecretKeyLen];
    crypto_box_keypair(clientPublicKey, clientSecretKey);

    // Compute shared key based on es-version
    uint8_t sharedKey[kPublicKeyLen];

    if (cert.esVersion == kEsVersionXChacha20) {
        // XChaCha20: use X25519 + HChaCha20 to derive the shared key
        // libsodium provides crypto_box_curve25519xchacha20poly1305_beforenm for this
        if (crypto_box_curve25519xchacha20poly1305_beforenm(sharedKey, cert.serverPublicKey,
                                                            clientSecretKey) != 0) {
            LOGW(L"DNSCrypt: failed to compute XChaCha20 shared key");
            return {};
        }
    } else {
        // XSalsa20: use X25519 (the default crypto_box_beforenm)
        if (crypto_box_beforenm(sharedKey, cert.serverPublicKey, clientSecretKey) != 0) {
            LOGW(L"DNSCrypt: failed to compute XSalsa20 shared key");
            return {};
        }
    }

    // Check for weak public key (all-zero shared secret)
    bool isZero = true;
    for (size_t i = 0; i < kPublicKeyLen; ++i) {
        if (sharedKey[i] != 0) {
            isZero = false;
            break;
        }
    }
    if (isZero) {
        LOGW(L"DNSCrypt: weak public key (zero shared secret)");
        return {};
    }

    // Encrypt query for UDP
    uint8_t clientNonce[kHalfNonceLen];
    std::vector<uint8_t> packet =
        EncryptQuery(query, cert, clientPublicKey, sharedKey, clientNonce, false);
    if (packet.empty()) {
        LOGW(L"DNSCrypt: failed to encrypt query");
        return {};
    }

    // Try UDP first
    SocketUtils::SocketHandle udpSock(
        socket(endpoint.address.ss_family, SOCK_DGRAM, IPPROTO_UDP));
    if (udpSock.IsValid()) {
        u_long mode = 1;
        ioctlsocket(udpSock, FIONBIO, &mode);

        if (!udpSock.RegisterWith(cancel)) return {};

        if (NetworkUtils::SendUdp(udpSock, packet,
                                  reinterpret_cast<const sockaddr*>(&endpoint.address),
                                  endpoint.length, timeoutMs / 2, cancel)) {
            const std::vector<uint8_t> response =
                NetworkUtils::RecvUdp(udpSock, timeoutMs / 2, cancel);

            if (!response.empty()) {
                std::vector<uint8_t> decrypted =
                    DecryptResponse(response, cert, clientNonce, sharedKey);

                if (!decrypted.empty()) {
                    // Check TC bit (bit 1 of byte 2 in DNS header)
                    const bool truncated = IsTruncated(decrypted.data(), decrypted.size());

                    if (!truncated) {
                        return decrypted;
                    }
                    // TC bit set, fall through to TCP retry
                }
            }
        }
    }

    // TCP retry (either UDP failed, or response was truncated)
    // Re-encrypt query with TCP-specific padding
    packet = EncryptQuery(query, cert, clientPublicKey, sharedKey, clientNonce, true);
    if (packet.empty()) {
        LOGW(L"DNSCrypt: failed to encrypt query for TCP");
        return {};
    }

    SocketUtils::SocketHandle tcpSock(
        socket(endpoint.address.ss_family, SOCK_STREAM, IPPROTO_TCP));
    if (!tcpSock.IsValid()) {
        LOGW(L"DNSCrypt: failed to create TCP socket");
        return {};
    }

    if (!tcpSock.RegisterWith(cancel)) return {};

    if (!NetworkUtils::ConnectWithTimeout(tcpSock,
                                          reinterpret_cast<const sockaddr*>(&endpoint.address),
                                          endpoint.length, timeoutMs / 2, cancel)) {
        LOGW(L"DNSCrypt: TCP connection failed");
        return {};
    }

    // Send query with TCP length prefix (2-byte big-endian)
    const std::vector<uint8_t> tcpPacket = EncodeTcpMessage(packet);
    if (tcpPacket.empty()) {
        LOGW(L"DNSCrypt: query too large for TCP framing");
        return {};
    }

    if (!NetworkUtils::SendAll(tcpSock, tcpPacket, timeoutMs / 2, cancel)) {
        LOGW(L"DNSCrypt: failed to send TCP query");
        return {};
    }

    const std::vector<uint8_t> response =
        NetworkUtils::RecvLengthPrefixed(tcpSock, timeoutMs / 2, cancel);
    if (response.empty()) {
        LOGW(L"DNSCrypt: failed to receive TCP response");
        return {};
    }

    std::vector<uint8_t> decrypted = DecryptResponse(response, cert, clientNonce, sharedKey);
    if (decrypted.empty()) {
        LOGW(L"DNSCrypt: failed to decrypt TCP response");
        return {};
    }

    return decrypted;
}

}  // namespace Dns
