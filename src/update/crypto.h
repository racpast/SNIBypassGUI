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

namespace Crypto {

// Incremental SHA-256, so a multi-chunk file can be hashed as it streams past
// without ever holding the reassembled file in memory.
class Sha256 {
public:
    Sha256();
    ~Sha256();
    Sha256(const Sha256&) = delete;
    Sha256& operator=(const Sha256&) = delete;

    bool valid() const { return hash_ != nullptr; }
    void Add(const void* data, size_t n);

    // Finishes the hash; the object must not be reused afterwards.
    bool Digest(std::vector<uint8_t>& out);

    // Finishing form returning lowercase hex, empty on failure.
    std::wstring Hex();

private:
    void* alg_ = nullptr;
    void* hash_ = nullptr;
    std::vector<uint8_t> object_;
    uint32_t length_ = 32;
};

// Lowercase hex SHA-256 of a buffer. Empty on failure.
std::wstring Sha256Hex(const void* data, size_t n);

// Decode standard base64. Canonical form is required: the input must be a whole
// number of 4-character groups, padding must be present and correct, no character
// may follow the padding, and the bits a canonical encoder discards in the final
// sextet must be zero. Anything else is rejected rather than quietly accepted —
// see the implementation for why a decoder that tolerates non-canonical input is a
// malleability hazard even when the caller happens to check the length.
bool Base64Decode(const std::string& in, std::vector<uint8_t>& out);

// The outcome of a signature check. A failed check is NOT the same event as an
// unavailable crypto provider, and the two must not be reported to the user as the
// same thing: one is evidence about the download, the other is a fact about the
// machine. Collapsing them accuses a correct download of being tampered with on any
// system that lacks the ECDSA P-256 provider.
enum class VerifyResult {
    Ok,            // the signature is valid over the message
    BadSignature,  // the signature is well-formed but does not match
    Unavailable,   // the check could not be performed on this system
};

// Verify a raw r||s (64-byte) ECDSA P-256/SHA-256 signature over `message`,
// against the public key compiled into this binary.
VerifyResult VerifySignature(const std::string& message, const std::vector<uint8_t>& signature);

}  // namespace Crypto
