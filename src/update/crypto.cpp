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

#include "update/crypto.h"

#include <windows.h>

#include <bcrypt.h>

#include <cstdio>
#include <cstring>

#include "app/logging.h"
#include "update/public_key.h"

namespace Crypto {
namespace {

// STATUS_INVALID_SIGNATURE, from ntstatus.h.
//
// Spelled out rather than included: ntstatus.h is not reachable from the headers
// this file already pulls in, and dragging it in for one constant would be worse
// than naming it here. The value is not from memory — TestUpdateSignatureVerification
// asserts it against a real BCryptVerifySignature call, so a wrong constant fails the
// suite instead of silently mislabeling every bad signature.
constexpr unsigned long kStatusInvalidSignature = 0xC000A000UL;

std::wstring ToHex(const uint8_t* d, size_t n) {
    static const wchar_t kDigits[] = L"0123456789abcdef";
    std::wstring s;
    s.reserve(n * 2);
    for (size_t i = 0; i < n; ++i) {
        s.push_back(kDigits[d[i] >> 4u]);
        s.push_back(kDigits[d[i] & 0xFu]);
    }
    return s;
}

}  // namespace

Sha256::Sha256() {
    auto* alg = reinterpret_cast<BCRYPT_ALG_HANDLE*>(&alg_);
    if (BCryptOpenAlgorithmProvider(alg, BCRYPT_SHA256_ALGORITHM, nullptr, 0) != 0) return;
    DWORD objectLength = 0;
    DWORD written = 0;
    BCryptGetProperty(*alg, BCRYPT_OBJECT_LENGTH, reinterpret_cast<PUCHAR>(&objectLength),
                      sizeof(objectLength), &written, 0);
    BCryptGetProperty(*alg, BCRYPT_HASH_LENGTH, reinterpret_cast<PUCHAR>(&length_),
                      sizeof(length_), &written, 0);
    object_.resize(objectLength);
    auto* hash = reinterpret_cast<BCRYPT_HASH_HANDLE*>(&hash_);
    if (BCryptCreateHash(*alg, hash, object_.data(), objectLength, nullptr, 0, 0) != 0)
        hash_ = nullptr;
}

Sha256::~Sha256() {
    if (hash_) BCryptDestroyHash(static_cast<BCRYPT_HASH_HANDLE>(hash_));
    if (alg_) BCryptCloseAlgorithmProvider(static_cast<BCRYPT_ALG_HANDLE>(alg_), 0);
}

void Sha256::Add(const void* data, size_t n) {
    if (!hash_ || n == 0) return;
    BCryptHashData(static_cast<BCRYPT_HASH_HANDLE>(hash_),
                   static_cast<PUCHAR>(const_cast<void*>(data)), static_cast<ULONG>(n), 0);
}

bool Sha256::Digest(std::vector<uint8_t>& out) {
    if (!hash_) return false;
    out.assign(length_, 0);
    return BCryptFinishHash(static_cast<BCRYPT_HASH_HANDLE>(hash_), out.data(), length_, 0) ==
           0;
}

std::wstring Sha256::Hex() {
    std::vector<uint8_t> digest;
    if (!Digest(digest)) return L"";
    return ToHex(digest.data(), digest.size());
}

std::wstring Sha256Hex(const void* data, size_t n) {
    Sha256 h;
    if (!h.valid()) return L"";
    h.Add(data, n);
    return h.Hex();
}

// Decode standard base64 in canonical form only.
//
// A decoder that "just works" on whatever it is fed is a malleability hazard. Two
// different inputs that both decode to the same bytes (padding that is not counted,
// a truncated final group, non-zero bits padding the last sextet) mean a signature
// over one byte string can be presented as a signature over a differently-spelled
// one. Today the only caller checks `signature.size() == 64` afterwards, which
// happens to make this unreachable — but that is one check in another file holding
// up a decoder with no notion of validity, and it stops being true the moment
// anything variable-length is signed.
//
// So the rules are the strict ones: whole 4-character groups, at most one correctly
// sized padding run, at the very end, with no character after it, and the bits the
// final sextet leaves over must be zero. What `base64.b64encode` in the release tool
// emits is exactly what this accepts, and nothing else is.
bool Base64Decode(const std::string& in, std::vector<uint8_t>& out) {
    const auto sextet = [](char c) -> int {
        if (c >= 'A' && c <= 'Z') return c - 'A';
        if (c >= 'a' && c <= 'z') return c - 'a' + 26;
        if (c >= '0' && c <= '9') return c - '0' + 52;
        if (c == '+') return 62;
        if (c == '/') return 63;
        return -1;
    };

    // Decoded into a local and handed over only on success, with `out` cleared up
    // front so that EVERY failing path leaves the caller with an empty vector.
    //
    // Both halves matter. Writing into `out` as we go would leave a partial buffer
    // behind on a late rejection (the non-canonical-final-sextet case). Assigning only
    // on success but never clearing would be worse in a different way: a caller that
    // reuses the vector would see the PREVIOUS successful result on failure and have
    // no way to tell that the decode it just asked for did not happen.
    out.clear();
    std::vector<uint8_t> decoded;
    decoded.reserve(in.size() / 4 * 3);

    if (in.empty() || in.size() % 4 != 0) return false;

    // Count trailing pad, then require the body before it to be clean base64.
    size_t end = in.size();
    while (end > 0 && in[end - 1] == '=') --end;
    const size_t pad = in.size() - end;
    if (pad > 2) return false;

    uint32_t accumulator = 0;
    unsigned bits = 0;
    for (size_t i = 0; i < end; ++i) {
        const int v = sextet(in[i]);
        if (v < 0) return false;  // includes any '=' before the final run
        accumulator = (accumulator << 6u) | static_cast<uint32_t>(v);
        bits += 6;
        if (bits >= 8) {
            bits -= 8;
            decoded.push_back(static_cast<uint8_t>((accumulator >> bits) & 0xFFu));
        }
    }

    // Any leftover bits are the ones a canonical encoder leaves zero. A non-zero
    // leftover is a second spelling of the same bytes, which is the whole point of
    // rejecting it.
    if (bits > 0 && (accumulator & ((1u << bits) - 1u)) != 0) return false;

    // The padding must account for exactly the bytes that were not emitted.
    if (bits != 0 && bits != 2 && bits != 4) return false;
    const size_t expectedPad = (bits == 0) ? 0 : (bits == 2 ? 1 : 2);
    if (pad != expectedPad) return false;

    out = std::move(decoded);
    return true;
}

// The outcome of a signature check. Encoding it instead of a bool is what keeps a
// missing crypto provider from being reported to the user as evidence of tampering.
VerifyResult VerifySignature(const std::string& message,
                             const std::vector<uint8_t>& signature) {
    if (signature.size() != 64) {
        LOGE(L"Update: signature is " + std::to_wstring(signature.size()) +
             L" bytes, expected 64.");
        return VerifyResult::Unavailable;
    }

    Sha256 hash;
    if (!hash.valid()) return VerifyResult::Unavailable;
    hash.Add(message.data(), message.size());
    std::vector<uint8_t> digest;
    if (!hash.Digest(digest)) return VerifyResult::Unavailable;

    std::vector<uint8_t> blob(sizeof(BCRYPT_ECCKEY_BLOB) + sizeof(kUpdatePublicKey));
    auto* header = reinterpret_cast<BCRYPT_ECCKEY_BLOB*>(blob.data());
    header->dwMagic = BCRYPT_ECDSA_PUBLIC_P256_MAGIC;
    header->cbKey = 32;  // per-coordinate size
    std::memcpy(blob.data() + sizeof(BCRYPT_ECCKEY_BLOB), kUpdatePublicKey,
                sizeof(kUpdatePublicKey));

    BCRYPT_ALG_HANDLE alg = nullptr;
    if (BCryptOpenAlgorithmProvider(&alg, BCRYPT_ECDSA_P256_ALGORITHM, nullptr, 0) != 0) {
        LOGE(L"Update: cannot open the ECDSA P-256 provider.");
        return VerifyResult::Unavailable;
    }

    BCRYPT_KEY_HANDLE key = nullptr;
    NTSTATUS status = BCryptImportKeyPair(alg, nullptr, BCRYPT_ECCPUBLIC_BLOB, &key,
                                          blob.data(), static_cast<ULONG>(blob.size()), 0);
    if (status != 0) {
        BCryptCloseAlgorithmProvider(alg, 0);
        LOGE(L"Update: cannot import the update public key (status 0x" +
             std::to_wstring(static_cast<unsigned long>(status)) + L").");
        return VerifyResult::Unavailable;
    }

    status = BCryptVerifySignature(
        key, nullptr, digest.data(), static_cast<ULONG>(digest.size()),
        const_cast<PUCHAR>(signature.data()), static_cast<ULONG>(signature.size()), 0);
    BCryptDestroyKey(key);
    BCryptCloseAlgorithmProvider(alg, 0);

    if (status == 0) return VerifyResult::Ok;
    // STATUS_INVALID_SIGNATURE is the one NTSTATUS that means "the signature does not
    // match this message". Every other non-zero status is the provider declining to
    // answer — a fact about the machine, which must not be shown to the user as a
    // verdict on the download. The value is defined in ntstatus.h; it is asserted by
    // TestUpdateSignatureVerification, so a wrong constant there fails loudly rather
    // than silently routing every mismatch into the "cannot verify" message.
    if (static_cast<unsigned long>(status) == kStatusInvalidSignature) {
        return VerifyResult::BadSignature;
    }
    LOGE(L"Update: signature check could not be performed (status 0x" +
         std::to_wstring(static_cast<unsigned long>(status)) + L").");
    return VerifyResult::Unavailable;
}

}  // namespace Crypto
