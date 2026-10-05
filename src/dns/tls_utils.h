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
// Shared TLS utilities using Windows Schannel.
//
// This module provides RAII wrappers and common operations for TLS connections
// used by DoH and DoT clients.
#include <winsock2.h>

#include <windows.h>

#define SECURITY_WIN32
#include <schannel.h>
#include <security.h>

#include <cstdint>
#include <string>
#include <utility>
#include <vector>

#include "dns/cancel.h"
#include "dns/socket_utils.h"

namespace Dns {
namespace TlsUtils {

// RAII wrapper for Schannel credential handle.
class CredHandle {
public:
    CredHandle() = default;
    ~CredHandle() { Release(); }

    CredHandle(const CredHandle&) = delete;
    CredHandle& operator=(const CredHandle&) = delete;

    CredHandle(CredHandle&& other) noexcept : m_handle(other.m_handle), m_valid(other.m_valid) {
        other.m_valid = false;
    }

    CredHandle& operator=(CredHandle&& other) noexcept {
        if (this != &other) {
            Release();
            m_handle = other.m_handle;
            m_valid = other.m_valid;
            other.m_valid = false;
        }
        return *this;
    }

    ::CredHandle* Get() { return &m_handle; }
    const ::CredHandle* Get() const { return &m_handle; }
    bool IsValid() const { return m_valid; }
    void SetValid(bool valid) { m_valid = valid; }

    void Release() {
        if (m_valid) {
            FreeCredentialsHandle(&m_handle);
            m_valid = false;
        }
    }

private:
    ::CredHandle m_handle = {};
    bool m_valid = false;
};

// RAII wrapper for Schannel context handle.
class CtxtHandle {
public:
    CtxtHandle() = default;
    ~CtxtHandle() { Release(); }

    CtxtHandle(const CtxtHandle&) = delete;
    CtxtHandle& operator=(const CtxtHandle&) = delete;

    CtxtHandle(CtxtHandle&& other) noexcept
        : m_handle(other.m_handle),
          m_valid(other.m_valid),
          m_pending(std::move(other.m_pending)) {
        other.m_valid = false;
    }

    CtxtHandle& operator=(CtxtHandle&& other) noexcept {
        if (this != &other) {
            Release();
            m_handle = other.m_handle;
            m_valid = other.m_valid;
            m_pending = std::move(other.m_pending);
            other.m_valid = false;
        }
        return *this;
    }

    ::CtxtHandle* Get() { return &m_handle; }
    const ::CtxtHandle* Get() const { return &m_handle; }
    bool IsValid() const { return m_valid; }
    void SetValid(bool valid) { m_valid = valid; }

    void SetPending(const uint8_t* data, size_t size) {
        if (size == 0) {
            m_pending.clear();
        } else {
            m_pending.assign(data, data + size);
        }
    }
    std::vector<uint8_t> TakePending() { return std::move(m_pending); }

    void Release() {
        if (m_valid) {
            DeleteSecurityContext(&m_handle);
            m_valid = false;
        }
        m_pending.clear();
    }

private:
    ::CtxtHandle m_handle = {};
    bool m_valid = false;
    std::vector<uint8_t> m_pending;
};

// Perform a TLS handshake over `sock`.
//
// Returns true on success. On success, credHandle and ctxtHandle are populated
// and must be kept alive for subsequent encryption/decryption operations.
//
// A cancelled token abandons the handshake: the token closes the socket, which
// unblocks the read underneath, and this returns false. The caller must not
// close the socket itself in that case — see the token's contract.
bool Handshake(SOCKET sock, const std::wstring& sni, CtxtHandle& ctxtHandle,
               CredHandle& credHandle,
               const std::vector<std::vector<uint8_t>>& certificateHashes, uint32_t timeoutMs,
               const CancelToken* cancel = nullptr);

// Encrypt and send data over TLS.
// Returns true if all data was sent, false on error.
bool Send(SOCKET sock, ::CtxtHandle* context, const std::vector<uint8_t>& data,
          uint32_t timeoutMs, const CancelToken* cancel = nullptr);

// How a receive ended.
//
// The distinction that matters is `DecryptFailed` against `PeerClosed`. Both used
// to arrive as an empty vector, so a message that failed its Poly1305/AEAD check
// on the way in looked exactly like the peer having said goodbye — in a program
// whose whole purpose is to keep working through network interference, that is
// the one distinction an operator would most want and could not get. The rest are
// separated because they call for different responses: a timeout is worth
// retrying past, a cancellation means the answer is no longer wanted, and a
// renegotiation is a protocol state this layer cannot advance.
enum class RecvStatus {
    Ok,             // a record was decrypted; `data` holds it
    TimedOut,       // the deadline passed with nothing to report
    Cancelled,      // the token fired
    PeerClosed,     // a clean close_notify, or the peer closed the connection
    Renegotiate,    // the peer asked to renegotiate, which this layer cannot drive
    DecryptFailed,  // the record did not authenticate
    Failed,         // the socket failed, or Schannel returned an unexpected status
};

struct RecvResult {
    RecvStatus status = RecvStatus::Failed;
    std::vector<uint8_t> data;

    bool ok() const { return status == RecvStatus::Ok; }
};

// Receive and decrypt one TLS application record within `timeoutMs`.
//
// Encrypted bytes beyond that record remain attached to `context` for the next
// call, including application data that arrived with the final handshake token.
//
// A `TimedOut`, `Cancelled` or `PeerClosed` result is ordinary and is not logged;
// a `DecryptFailed` is logged here, because by the time it reaches a caller the
// reason has been reduced to "no answer" and the record that failed is gone.
RecvResult Recv(SOCKET sock, CtxtHandle& context, uint32_t timeoutMs,
                const CancelToken* cancel = nullptr);

// Feed decrypted records to `sink` until it is finished or `deadline` passes.
//
// DoH and DoT do the same thing between the handshake and the answer, and the
// only part that differs is what frames the stream on top of TLS — an HTTP
// response for one, DNS's own length prefix for the other. So the framing is
// the parameter and the loop is here: read a record, hand it over, stop the
// moment the framer says it has a whole message, and tell the framer the stream
// ended when it does not.
//
// The point of it is the deadline. `Recv` is called with whatever time is left
// rather than with the whole timeout, so a peer that dribbles records cannot
// extend the exchange by resetting the clock on every one — a property that was
// easy to lose when each client re-derived it for itself.
//
// `sink` must provide:
//   bool Feed(const uint8_t* data, size_t len)  — false once it is finished
//   void Finish()                               — the stream ended incomplete
// and is left in whatever state those two calls give it; the caller reads the
// result out of its own framer, which is the only thing that knows what
// "complete" means for it.
template <typename Sink>
void RecvUntil(SOCKET sock, CtxtHandle& context, uint64_t deadline, const CancelToken* cancel,
               Sink& sink) {
    // Qualified rather than left to lookup: this is a template, so a name with
    // no argument depending on the parameter cannot be found by ADL at
    // definition time, and SOCKET is a plain integer here rather than a class
    // that would carry an associated namespace.
    while (SocketUtils::Now() < deadline) {
        const RecvResult received =
            Recv(sock, context, static_cast<uint32_t>(deadline - SocketUtils::Now()), cancel);
        // Every non-`Ok` status ends the stream for this purpose: a timeout, a
        // cancellation, a close, a failed record and a renegotiation all mean no
        // further bytes are coming. `Recv` has already logged the ones that are
        // not ordinary, which is what keeps a decrypt failure from being filed
        // away here as just another end of stream.
        if (!received.ok()) break;
        if (!sink.Feed(received.data.data(), received.data.size()))
            return;  // the framer is done
    }

    // Out of time, or the peer stopped sending, with the framer still waiting
    // for the rest. Handing it the end rather than returning silently is what
    // keeps "the stream ended early" from looking like "nothing arrived yet".
    sink.Finish();
}

}  // namespace TlsUtils
}  // namespace Dns
