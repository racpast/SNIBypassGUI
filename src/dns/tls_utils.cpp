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

#include "dns/tls_utils.h"

#include <wincrypt.h>

#include <algorithm>
#include <cstring>
#include <limits>
#include <vector>

#include "app/logging.h"
#include "dns/network_utils.h"
#include "dns/socket_utils.h"

namespace Dns {
namespace TlsUtils {
namespace {

using SocketUtils::Now;
using SocketUtils::WaitResult;

constexpr size_t kTlsBufferBytes = size_t{64} * 1024;

uint32_t RemainingMs(uint64_t deadline) {
    const uint64_t now = Now();
    if (now >= deadline) return 0;
    return static_cast<uint32_t>(std::min<uint64_t>(
        deadline - now, static_cast<uint64_t>(std::numeric_limits<uint32_t>::max())));
}

bool SendAll(SOCKET sock, const void* data, size_t len, uint64_t deadline,
             const CancelToken* cancel) {
    const uint32_t remaining = RemainingMs(deadline);
    if (remaining == 0) return false;
    return NetworkUtils::SendAll(sock, static_cast<const uint8_t*>(data), len, remaining,
                                 cancel);
}

bool CertificateMatchesPins(::CtxtHandle* context,
                            const std::vector<std::vector<uint8_t>>& hashes) {
    if (hashes.empty()) return true;

    PCCERT_CONTEXT remote = nullptr;
    if (QueryContextAttributes(context, SECPKG_ATTR_REMOTE_CERT_CONTEXT,
                               static_cast<void*>(&remote)) != SEC_E_OK ||
        remote == nullptr) {
        LOGW(L"TLS: cannot inspect the remote certificate for pin validation");
        return false;
    }

    CERT_CHAIN_PARA params = {};
    params.cbSize = sizeof(params);
    PCCERT_CHAIN_CONTEXT chain = nullptr;
    const BOOL built = CertGetCertificateChain(nullptr, remote, nullptr, remote->hCertStore,
                                               &params, 0, nullptr, &chain);

    bool matched = false;
    if (built && chain != nullptr) {
        for (DWORD simple = 0; simple < chain->cChain && !matched; ++simple) {
            const PCERT_SIMPLE_CHAIN certificates = chain->rgpChain[simple];
            for (DWORD i = 0; i < certificates->cElement && !matched; ++i) {
                const PCCERT_CONTEXT certificate = certificates->rgpElement[i]->pCertContext;
                if (CertCompareCertificateName(certificate->dwCertEncodingType,
                                               &certificate->pCertInfo->Subject,
                                               &certificate->pCertInfo->Issuer)) {
                    continue;  // DNS stamp pins deliberately exclude trust anchors.
                }
                uint8_t digest[32] = {};
                DWORD digestSize = sizeof(digest);
                if (!CryptHashCertificate(0, CALG_SHA_256, 0, certificate->pbCertEncoded,
                                          certificate->cbCertEncoded, digest, &digestSize) ||
                    digestSize != sizeof(digest)) {
                    continue;
                }
                matched = std::any_of(hashes.begin(), hashes.end(), [&](const auto& hash) {
                    return hash.size() == sizeof(digest) &&
                           std::equal(hash.begin(), hash.end(), digest);
                });
            }
        }
    }

    if (chain != nullptr) CertFreeCertificateChain(chain);
    CertFreeCertificateContext(remote);
    if (!matched) LOGW(L"TLS: the server certificate chain does not match the DNS stamp pins");
    return matched;
}

}  // namespace

bool Handshake(SOCKET sock, const std::wstring& sni, CtxtHandle& ctxtHandle,
               CredHandle& credHandle,
               const std::vector<std::vector<uint8_t>>& certificateHashes, uint32_t timeoutMs,
               const CancelToken* cancel) {
    if (sni.empty() || timeoutMs == 0 || (cancel && cancel->Cancelled())) return false;

    const uint64_t deadline = Now() + timeoutMs;

    SECURITY_STATUS status;

    // Initialize credentials
    SCHANNEL_CRED credData = {};
    credData.dwVersion = SCHANNEL_CRED_VERSION;
    credData.dwFlags =
        SCH_CRED_NO_DEFAULT_CREDS | SCH_CRED_AUTO_CRED_VALIDATION | SCH_USE_STRONG_CRYPTO;

    TimeStamp expiry;
    status = AcquireCredentialsHandleW(nullptr, const_cast<wchar_t*>(UNISP_NAME_W),
                                       SECPKG_CRED_OUTBOUND, nullptr, &credData, nullptr,
                                       nullptr, credHandle.Get(), &expiry);
    if (status != SEC_E_OK) {
        LOGW(L"TLS: AcquireCredentialsHandle failed (0x" + std::to_wstring(status) + L")");
        return false;
    }
    credHandle.SetValid(true);

    // Initialize context
    DWORD contextAttr = ISC_REQ_SEQUENCE_DETECT | ISC_REQ_REPLAY_DETECT |
                        ISC_REQ_CONFIDENTIALITY | ISC_REQ_ALLOCATE_MEMORY | ISC_REQ_STREAM;
    DWORD outFlags = 0;
    const wchar_t* targetName = sni.empty() ? nullptr : sni.c_str();

    // The first call produces the ClientHello, which has no input buffers at all
    // — hence the separate SecBufferDesc rather than the loop's shape reused.
    SecBuffer firstOut[1] = {};
    firstOut[0].BufferType = SECBUFFER_TOKEN;
    SecBufferDesc firstOutDesc = {};
    firstOutDesc.ulVersion = SECBUFFER_VERSION;
    firstOutDesc.cBuffers = 1;
    firstOutDesc.pBuffers = firstOut;

    status = InitializeSecurityContextW(
        credHandle.Get(), nullptr, const_cast<wchar_t*>(targetName), contextAttr, 0, 0, nullptr,
        0, ctxtHandle.Get(), &firstOutDesc, &outFlags, &expiry);
    if (status != SEC_I_CONTINUE_NEEDED && status != SEC_E_OK) {
        LOGW(L"TLS: InitializeSecurityContext failed (0x" + std::to_wstring(status) + L")");
        return false;
    }
    ctxtHandle.SetValid(true);

    if (firstOut[0].cbBuffer > 0 && firstOut[0].pvBuffer != nullptr) {
        const bool ok =
            SendAll(sock, firstOut[0].pvBuffer, firstOut[0].cbBuffer, deadline, cancel);
        FreeContextBuffer(firstOut[0].pvBuffer);
        if (!ok) {
            LOGW(L"TLS: failed to send handshake data");
            return false;
        }
    }

    // The deadline covers the whole handshake, not each read: a server that
    // dribbles one byte at a time must not be able to hold the connection open
    // indefinitely by resetting it.
    std::vector<uint8_t> recvBuffer(kTlsBufferBytes);
    size_t recvOffset = 0;

    while (status == SEC_I_CONTINUE_NEEDED || status == SEC_E_INCOMPLETE_MESSAGE) {
        // Process surplus bytes immediately. Only wait for the network if there
        // is no surplus, or Schannel said the current record is incomplete.
        if (recvOffset == 0 || status == SEC_E_INCOMPLETE_MESSAGE) {
            const WaitResult waited = SocketUtils::WaitFor(sock, true, false, deadline, cancel);
            if (waited == WaitResult::Aborted) {
                LOGW(L"TLS: handshake abandoned (cancelled or select failed)");
                return false;
            }
            if (waited == WaitResult::TimedOut) {
                LOGW(L"TLS: handshake timed out");
                return false;
            }

            if (recvOffset == recvBuffer.size()) {
                LOGW(L"TLS: handshake buffer overflow");
                return false;
            }

            const int received =
                recv(sock, reinterpret_cast<char*>(recvBuffer.data() + recvOffset),
                     static_cast<int>(recvBuffer.size() - recvOffset), 0);
            if (received <= 0) {
                if (received == SOCKET_ERROR && WSAGetLastError() == WSAEWOULDBLOCK) continue;
                LOGW(L"TLS: handshake recv failed");
                return false;
            }
            recvOffset += static_cast<size_t>(received);
        }

        SecBuffer inBuffers[2] = {};
        inBuffers[0].BufferType = SECBUFFER_TOKEN;
        inBuffers[0].cbBuffer = static_cast<DWORD>(recvOffset);
        inBuffers[0].pvBuffer = recvBuffer.data();
        inBuffers[1].BufferType = SECBUFFER_EMPTY;

        SecBufferDesc inBufferDesc = {};
        inBufferDesc.ulVersion = SECBUFFER_VERSION;
        inBufferDesc.cBuffers = 2;
        inBufferDesc.pBuffers = inBuffers;

        SecBuffer outBuffers[1] = {};
        outBuffers[0].BufferType = SECBUFFER_TOKEN;
        SecBufferDesc outBufferDesc = {};
        outBufferDesc.ulVersion = SECBUFFER_VERSION;
        outBufferDesc.cBuffers = 1;
        outBufferDesc.pBuffers = outBuffers;

        status = InitializeSecurityContextW(
            credHandle.Get(), ctxtHandle.Get(), const_cast<wchar_t*>(targetName), contextAttr,
            0, 0, &inBufferDesc, 0, nullptr, &outBufferDesc, &outFlags, &expiry);

        if (status == SEC_E_OK || status == SEC_I_CONTINUE_NEEDED ||
            status == SEC_E_INCOMPLETE_MESSAGE) {
            // Nothing to send yet; the surplus handling below still applies.
        } else {
            LOGW(L"TLS: handshake failed (0x" + std::to_wstring(status) + L")");
            return false;
        }

        if (outBuffers[0].cbBuffer > 0 && outBuffers[0].pvBuffer != nullptr) {
            const bool ok =
                SendAll(sock, outBuffers[0].pvBuffer, outBuffers[0].cbBuffer, deadline, cancel);
            FreeContextBuffer(outBuffers[0].pvBuffer);
            if (!ok) {
                LOGW(L"TLS: failed to send handshake data");
                return false;
            }
        }

        // SEC_E_INCOMPLETE_MESSAGE means none of the input can be discarded.
        // Append the rest of this record on the next pass.
        if (status == SEC_E_INCOMPLETE_MESSAGE) continue;

        // Bytes past the token belong to the next handshake or application
        // record; keep them and drop the consumed prefix.
        if (inBuffers[1].BufferType == SECBUFFER_EXTRA && inBuffers[1].cbBuffer > 0) {
            std::memmove(recvBuffer.data(),
                         recvBuffer.data() + recvOffset - inBuffers[1].cbBuffer,
                         inBuffers[1].cbBuffer);
            recvOffset = inBuffers[1].cbBuffer;
        } else {
            recvOffset = 0;
        }
    }

    if (status == SEC_E_OK && CertificateMatchesPins(ctxtHandle.Get(), certificateHashes)) {
        ctxtHandle.SetPending(recvBuffer.data(), recvOffset);
        return true;
    }

    return false;
}

bool Send(SOCKET sock, ::CtxtHandle* context, const std::vector<uint8_t>& data,
          uint32_t timeoutMs, const CancelToken* cancel) {
    if (timeoutMs == 0 || (cancel && cancel->Cancelled())) return false;
    const uint64_t deadline = Now() + timeoutMs;

    SecPkgContext_StreamSizes sizes = {};
    if (QueryContextAttributes(context, SECPKG_ATTR_STREAM_SIZES, &sizes) != SEC_E_OK) {
        return false;
    }

    size_t offset = 0;
    while (offset < data.size()) {
        const size_t chunkSize = std::min<size_t>(data.size() - offset, sizes.cbMaximumMessage);

        std::vector<uint8_t> encrypted(sizes.cbHeader + chunkSize + sizes.cbTrailer);

        SecBuffer buffers[4] = {};
        buffers[0].BufferType = SECBUFFER_STREAM_HEADER;
        buffers[0].pvBuffer = encrypted.data();
        buffers[0].cbBuffer = sizes.cbHeader;

        buffers[1].BufferType = SECBUFFER_DATA;
        buffers[1].pvBuffer = encrypted.data() + sizes.cbHeader;
        buffers[1].cbBuffer = static_cast<DWORD>(chunkSize);
        std::memcpy(buffers[1].pvBuffer, data.data() + offset, chunkSize);

        buffers[2].BufferType = SECBUFFER_STREAM_TRAILER;
        buffers[2].pvBuffer = encrypted.data() + sizes.cbHeader + chunkSize;
        buffers[2].cbBuffer = sizes.cbTrailer;

        buffers[3].BufferType = SECBUFFER_EMPTY;

        SecBufferDesc bufferDesc = {};
        bufferDesc.ulVersion = SECBUFFER_VERSION;
        bufferDesc.cBuffers = 4;
        bufferDesc.pBuffers = buffers;

        if (EncryptMessage(context, 0, &bufferDesc, 0) != SEC_E_OK) {
            return false;
        }

        const DWORD totalSize = buffers[0].cbBuffer + buffers[1].cbBuffer + buffers[2].cbBuffer;
        if (!SendAll(sock, encrypted.data(), totalSize, deadline, cancel)) {
            return false;
        }

        offset += chunkSize;
    }

    return true;
}

RecvResult Recv(SOCKET sock, CtxtHandle& context, uint32_t timeoutMs,
                const CancelToken* cancel) {
    RecvResult result;
    std::vector<uint8_t> pending = context.TakePending();
    std::vector<uint8_t> recvBuffer(std::max(kTlsBufferBytes, pending.size()));
    size_t recvOffset = pending.size();
    std::copy(pending.begin(), pending.end(), recvBuffer.begin());

    const uint64_t deadline = Now() + timeoutMs;

    while (Now() < deadline) {
        // Anything already buffered is decrypted before waiting for more: a
        // response that arrived in the same read as its predecessor must not
        // stall on a read that is never going to happen.
        if (recvOffset == 0) {
            const WaitResult waited = SocketUtils::WaitFor(sock, true, false, deadline, cancel);
            if (waited != WaitResult::Ready) {
                // The wait itself separates the two falsy outcomes: a token that
                // fired leaves the caller uninterested, while a deadline that
                // passed is an ordinary thing to retry past. Telling them apart
                // was impossible while both arrived here as an empty vector.
                result.status =
                    (waited == WaitResult::Aborted && cancel != nullptr && cancel->Cancelled())
                        ? RecvStatus::Cancelled
                        : RecvStatus::TimedOut;
                return result;
            }

            const int received = recv(sock, reinterpret_cast<char*>(recvBuffer.data()),
                                      static_cast<int>(recvBuffer.size()), 0);
            if (received <= 0) {
                if (received == SOCKET_ERROR && WSAGetLastError() == WSAEWOULDBLOCK) continue;
                // Zero is the peer's FIN, and a socket error at this point is not
                // distinguishable from it in any way the caller acts on: either
                // way the stream is over with nothing more to read.
                result.status = RecvStatus::PeerClosed;
                return result;
            }
            recvOffset = static_cast<size_t>(received);
        }

        SecBuffer buffers[4] = {};
        buffers[0].BufferType = SECBUFFER_DATA;
        buffers[0].cbBuffer = static_cast<DWORD>(recvOffset);
        buffers[0].pvBuffer = recvBuffer.data();
        buffers[1].BufferType = SECBUFFER_EMPTY;
        buffers[2].BufferType = SECBUFFER_EMPTY;
        buffers[3].BufferType = SECBUFFER_EMPTY;

        SecBufferDesc bufferDesc = {};
        bufferDesc.ulVersion = SECBUFFER_VERSION;
        bufferDesc.cBuffers = 4;
        bufferDesc.pBuffers = buffers;

        const SECURITY_STATUS status = DecryptMessage(context.Get(), &bufferDesc, 0, nullptr);

        if (status == SEC_I_CONTEXT_EXPIRED) {
            // The peer sent a close_notify. Whatever was decrypted before it is
            // still ours to return; an orderly shutdown is not an error.
            result.status = RecvStatus::PeerClosed;
            return result;
        }
        if (status == SEC_I_RENEGOTIATE) {
            // A renegotiation mid-stream cannot be driven from here — the caller
            // owns the socket but not the handshake. Treat it as the end of the
            // message rather than looping on a state we cannot advance.
            result.status = RecvStatus::Renegotiate;
            return result;
        }
        if (status == SEC_E_INCOMPLETE_MESSAGE) {
            // A record arrived in pieces. Keep the bytes and read the rest; the
            // buffer is never compacted here because Schannel has retained a
            // pointer into it and expects the same span.
            if (recvOffset == recvBuffer.size()) {
                result.status = RecvStatus::Failed;
                result.data.clear();
                LOGW(L"TLS: record does not fit the receive buffer (" +
                     std::to_wstring(recvBuffer.size()) + L" bytes)");
                return result;
            }
            const WaitResult waited = SocketUtils::WaitFor(sock, true, false, deadline, cancel);
            if (waited != WaitResult::Ready) {
                result.status =
                    (waited == WaitResult::Aborted && cancel != nullptr && cancel->Cancelled())
                        ? RecvStatus::Cancelled
                        : RecvStatus::TimedOut;
                return result;
            }
            const int received =
                recv(sock, reinterpret_cast<char*>(recvBuffer.data() + recvOffset),
                     static_cast<int>(recvBuffer.size() - recvOffset), 0);
            if (received <= 0) {
                if (received == SOCKET_ERROR && WSAGetLastError() == WSAEWOULDBLOCK) continue;
                result.status = RecvStatus::PeerClosed;
                return result;
            }
            recvOffset += static_cast<size_t>(received);
            continue;
        }
        if (status == SEC_E_DECRYPT_FAILURE) {
            // The record did not authenticate. This is the case the old shared
            // "empty vector" made indistinguishable from a clean goodbye, and it
            // is the one worth a line in the log: on a path built to survive
            // interference, a record that fails its integrity check is either an
            // active attacker or a broken middlebox, and the operator should not
            // have to infer that from a silent timeout.
            result.status = RecvStatus::DecryptFailed;
            result.data.clear();
            LOGW(
                L"TLS: a received record failed to decrypt (SEC_E_DECRYPT_FAILURE); the "
                L"connection is being treated as unusable");
            return result;
        }
        if (status != SEC_E_OK) {
            result.status = RecvStatus::Failed;
            result.data.clear();
            LOGW(L"TLS: DecryptMessage failed (0x" + std::to_wstring(status) + L")");
            return result;
        }

        for (int i = 0; i < 4; ++i) {
            if (buffers[i].BufferType == SECBUFFER_DATA && buffers[i].cbBuffer > 0) {
                const uint8_t* data = static_cast<const uint8_t*>(buffers[i].pvBuffer);
                result.data.insert(result.data.end(), data, data + buffers[i].cbBuffer);
            }
        }

        // Leftover bytes are the start of the next record. They sit at the front
        // of the buffer, and a fresh read appends after them.
        size_t extra = 0;
        for (int i = 0; i < 4; ++i) {
            if (buffers[i].BufferType == SECBUFFER_EXTRA && buffers[i].cbBuffer > 0) {
                std::memmove(recvBuffer.data(), buffers[i].pvBuffer, buffers[i].cbBuffer);
                extra = buffers[i].cbBuffer;
                break;
            }
        }
        recvOffset = extra;

        if (!result.data.empty()) {
            context.SetPending(recvBuffer.data(), recvOffset);
            result.status = RecvStatus::Ok;
            return result;
        }
    }

    // Out of time with nothing decrypted. This is the loop's own deadline rather
    // than one that `WaitFor` reported, but it is the same outcome to a caller.
    result.status = RecvStatus::TimedOut;
    return result;
}

}  // namespace TlsUtils
}  // namespace Dns
