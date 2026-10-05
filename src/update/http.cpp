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

#include "update/http.h"

#include <windows.h>

#include <winhttp.h>

#include <atomic>
#include <mutex>
#include <string>
#include <vector>

#include "app/logging.h"
#include "app/version.h"
#include "compat/bit_cast.h"

namespace Http {
namespace {

// Per-operation timeouts, in milliseconds. Receive is generous on purpose: it was
// measured that a short one does not return control mid-body, it kills the
// connection (1.5 s against a 6 s server silence failed after 1 KiB of 3 KiB, while
// 60 s completed the same transfer). Cancellation does not go through this value.
constexpr int kResolveTimeoutMs = 15000;
constexpr int kConnectTimeoutMs = 15000;
constexpr int kSendTimeoutMs = 30000;
constexpr int kReceiveTimeoutMs = 60000;

// The read buffer. The documentation is explicit that a buffer smaller than
// WinHTTP's own internal 8 KB can make a read complete synchronously, and that a
// completion which synchronously starts the next read can overflow the stack. 64 KB
// is well clear of that and matches the buffer Sha256File already uses.
constexpr DWORD kReadBufferBytes = 64 * 1024;

// How often Run() re-examines the transfer while waiting. Only affects how promptly
// progress is reported to the caller; it is not a poll of the network.
constexpr DWORD kTickMs = 40;

struct Url {
    std::wstring host;
    std::wstring path;  // includes the leading '/' and any query
    INTERNET_PORT port = 0;
    bool secure = false;
};

// Split `url` into the form WinHTTP takes. False on anything that is not an
// absolute http(s) URL with a non-empty host, which is a programming error in this
// module rather than a runtime condition — every endpoint here is a compile-time
// constant.
bool ParseUrl(const std::wstring& url, Url& out) {
    std::wstring lower = url;
    for (wchar_t& c : lower)
        if (c >= L'A' && c <= L'Z') c = static_cast<wchar_t>(c - L'A' + L'a');

    size_t afterScheme = 0;
    if (lower.rfind(L"https://", 0) == 0) {
        out.secure = true;
        afterScheme = 8;
    } else if (lower.rfind(L"http://", 0) == 0) {
        out.secure = false;
        afterScheme = 7;
    } else {
        return false;
    }

    const size_t slash = url.find(L'/', afterScheme);
    const std::wstring authority = (slash == std::wstring::npos)
                                       ? url.substr(afterScheme)
                                       : url.substr(afterScheme, slash - afterScheme);
    if (authority.empty()) return false;

    out.path = (slash == std::wstring::npos) ? L"/" : url.substr(slash);

    const size_t colon = authority.find(L':');
    if (colon == std::wstring::npos) {
        out.host = authority;
        out.port = 0;  // let WinHTTP pick from the scheme
    } else {
        out.host = authority.substr(0, colon);
        const std::wstring portText = authority.substr(colon + 1);
        if (portText.empty()) return false;
        unsigned long value = 0;
        for (wchar_t c : portText) {
            if (c < L'0' || c > L'9') return false;
            value = value * 10 + static_cast<unsigned long>(c - L'0');
            if (value > 65535) return false;
        }
        out.port = static_cast<INTERNET_PORT>(value);
    }
    return !out.host.empty();
}

// The base type is pinned to one byte because this enum is a member of Transfer and
// only ever holds one of four values; the default int costs three bytes of padding in
// a structure that is already wide. The project's other state enums do the same.
enum class Outcome : std::uint8_t { Pending, Ok, Cancelled, Failed };
}  // namespace

// The live state of one transfer.
//
// Lifetime is a hand-rolled reference count rather than a shared_ptr, because the
// references are held by WinHTTP as much as by this program: every open handle
// holds one, and it is released when that handle's WINHTTP_CALLBACK_STATUS_
// HANDLE_CLOSING arrives. That callback is the only reliable statement that a
// handle will never produce another callback, so it is the only safe place to drop
// a count. The count starts at 1 for the caller inside Run().
struct Transfer {
    std::atomic<LONG> refs{1};

    // The handles. Guarded by `mx`, because Cancel() closes the request handle from
    // another thread while Run() may be closing the rest.
    std::mutex mx;
    HINTERNET session = nullptr;
    HINTERNET connect = nullptr;
    HINTERNET request = nullptr;

    // The body and how much of it has arrived. The body is appended by callbacks, so
    // it is guarded; the count is an atomic so Run() can read it without the lock
    // while it waits.
    std::string body;
    std::atomic<uint64_t> received{0};

    std::atomic<Outcome> outcome{Outcome::Pending};
    std::atomic<unsigned long> errorCode{0};

    // Signalled once and for all when the transfer reaches a terminal state.
    HANDLE done = nullptr;
    // Signalled by a callback that appended bytes, so Run() can report progress
    // promptly instead of only when the transfer ends.
    HANDLE wake = nullptr;

    // Reused across reads. Allocated once at header time and freed by the destructor.
    std::vector<char> buffer;

    bool statusChecked = false;

    void AddRef() { refs.fetch_add(1); }
    void Release() {
        if (refs.fetch_sub(1) == 1) delete this;
    }

    // Record the terminal state. First writer wins, so a late callback after a
    // cancellation cannot overwrite the verdict that the caller will act on.
    void Finish(Outcome o, unsigned long err) {
        Outcome expected = Outcome::Pending;
        if (outcome.compare_exchange_strong(expected, o)) errorCode.store(err);
        SetEvent(done);
    }
};

namespace {

void CloseHandles(Transfer* t) {
    HINTERNET s = nullptr, c = nullptr, r = nullptr;
    {
        std::lock_guard<std::mutex> lock(t->mx);
        s = t->session;
        c = t->connect;
        r = t->request;
        t->session = nullptr;
        t->connect = nullptr;
        t->request = nullptr;
    }
    // Closed outside the lock: WinHttpCloseHandle blocks until the handle's pending
    // callbacks have run, and those callbacks take the same lock.
    if (r) WinHttpCloseHandle(r);
    if (c) WinHttpCloseHandle(c);
    if (s) WinHttpCloseHandle(s);
}

void StartNextRead(Transfer* t);
void RequestHeaders(Transfer* t);

// The status callback. Runs on a WinHTTP thread, so it does the minimum: record
// bytes, advance the state machine, and never block.
void CALLBACK OnStatus(HINTERNET handle, DWORD_PTR context, DWORD status, void* info,
                       DWORD infoLen) {
    // The context arrives as an integer holding an address, and BitCast is how the
    // bits come back as a pointer. Neither a cast nor a memcpy would do: see
    // compat/bit_cast.h for why each is rejected by one of this project's gates.
    Transfer* t = BitCast<Transfer*>(context);
    if (!t) return;

    if (status == WINHTTP_CALLBACK_STATUS_HANDLE_CLOSING) {
        // The one moment it is certain this handle will never call back again.
        t->Release();
        return;
    }

    switch (status) {
        case WINHTTP_CALLBACK_STATUS_SENDREQUEST_COMPLETE:
            if (!WinHttpReceiveResponse(t->request, nullptr))
                t->Finish(Outcome::Failed, GetLastError());
            break;

        case WINHTTP_CALLBACK_STATUS_HEADERS_AVAILABLE: {
            DWORD code = 0;
            DWORD len = sizeof(code);
            if (!WinHttpQueryHeaders(
                    t->request, WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER,
                    WINHTTP_HEADER_NAME_BY_INDEX, &code, &len, WINHTTP_NO_HEADER_INDEX)) {
                t->Finish(Outcome::Failed, GetLastError());
                break;
            }
            if (code < 200 || code >= 300) {
                LOGW(L"Update: HTTP status " + std::to_wstring(code) + L".");
                t->Finish(Outcome::Failed, code);
                break;
            }
            t->statusChecked = true;
            t->buffer.resize(kReadBufferBytes);
            RequestHeaders(t);
            break;
        }

        case WINHTTP_CALLBACK_STATUS_DATA_AVAILABLE: {
            // In asynchronous mode the count arrives here and the out-parameter
            // must be NULL; passing a pointer is documented to fault.
            const DWORD avail = infoLen ? *static_cast<DWORD*>(info) : 0;
            if (avail == 0) {
                t->Finish(Outcome::Ok, 0);  // end of body
                break;
            }
            if (avail > t->buffer.size()) t->buffer.resize(avail);
            // The read must be issued from inside this callback's thread. Issuing it
            // from another thread after the callback returns is not valid.
            if (!WinHttpReadData(t->request, t->buffer.data(), avail, nullptr))
                t->Finish(Outcome::Failed, GetLastError());
            break;
        }

        case WINHTTP_CALLBACK_STATUS_READ_COMPLETE: {
            const DWORD got = infoLen;
            if (got == 0) {
                t->Finish(Outcome::Ok, 0);  // end of body
                break;
            }
            {
                std::lock_guard<std::mutex> lock(t->mx);
                t->body.append(t->buffer.data(), got);
            }
            t->received.fetch_add(got);
            SetEvent(t->wake);
            StartNextRead(t);
            break;
        }

        case WINHTTP_CALLBACK_STATUS_REQUEST_ERROR: {
            auto* result = static_cast<WINHTTP_ASYNC_RESULT*>(info);
            const unsigned long err = result ? result->dwError : 0;
            if (err == ERROR_WINHTTP_OPERATION_CANCELLED)
                t->Finish(Outcome::Cancelled, err);
            else
                t->Finish(Outcome::Failed, err);
            break;
        }

        default: break;
    }
    (void)handle;
}

void RequestHeaders(Transfer* t) {
    // NULL out-parameter: in asynchronous mode the count is delivered through the
    // DATA_AVAILABLE callback.
    if (!WinHttpQueryDataAvailable(t->request, nullptr))
        t->Finish(Outcome::Failed, GetLastError());
}

void StartNextRead(Transfer* t) {
    RequestHeaders(t);
}

}  // namespace

Request::Request() = default;

Request::~Request() {
    Stop();
}

void Request::SetProgress(ProgressFn fn) {
    std::lock_guard<std::mutex> lock(m_mutex);
    m_progress = std::move(fn);
}

void Request::Stop() {
    Transfer* active = nullptr;
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        if (!m_active) {
            // Cancelled before or after Run(). Run() reads this so a Stop() that
            // arrives first is not lost.
            m_cancelledBeforeStart = true;
            return;
        }
        active = m_active;
        active->AddRef();  // keep it alive across the close below
    }

    // Closing the request handle is what aborts the operation. The transfer's own
    // callback then completes with ERROR_WINHTTP_OPERATION_CANCELLED and Run()
    // returns Cancelled. Closing here, on the caller's thread, is the documented
    // mechanism rather than an abuse of one.
    {
        HINTERNET r = nullptr;
        {
            std::lock_guard<std::mutex> lock(active->mx);
            r = active->request;
            active->request = nullptr;
        }
        if (r) WinHttpCloseHandle(r);
    }

    active->Release();
}

Result Request::Run(const std::wstring& url, std::string& out) {
    out.clear();

    Url parts;
    if (!ParseUrl(url, parts)) {
        LOGE(L"Update: refusing a malformed endpoint: " + url);
        return Result::Failed;
    }

    bool alreadyCancelled = false;
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        alreadyCancelled = m_cancelledBeforeStart;
    }
    if (alreadyCancelled) return Result::Cancelled;

    // One Transfer per Run(), held by its own reference for the duration of this
    // call and by one reference per open WinHTTP handle.
    auto* t = new Transfer();
    t->done = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    t->wake = CreateEventW(nullptr, FALSE, FALSE, nullptr);
    if (!t->done || !t->wake) {
        CloseHandle(t->done);
        CloseHandle(t->wake);
        t->Release();
        return Result::Failed;
    }

    t->session =
        WinHttpOpen(APP_NAME L"/" APP_VERSION_NUM, WINHTTP_ACCESS_TYPE_NO_PROXY,
                    WINHTTP_NO_PROXY_NAME, WINHTTP_NO_PROXY_BYPASS, WINHTTP_FLAG_ASYNC);
    if (!t->session) {
        LOGE(L"Update: WinHttpOpen failed (err " + std::to_wstring(GetLastError()) + L").");
        CloseHandles(t);
        t->Release();
        return Result::Failed;
    }
    t->AddRef();  // the session's HANDLE_CLOSING will release it
    WinHttpSetStatusCallback(t->session, OnStatus, WINHTTP_CALLBACK_FLAG_ALL_COMPLETIONS, 0);
    WinHttpSetTimeouts(t->session, kResolveTimeoutMs, kConnectTimeoutMs, kSendTimeoutMs,
                       kReceiveTimeoutMs);

    // The manifest and every chunk are fetched through WinHTTP with its own defaults,
    // NOT through WinINet's per-user configuration. WinINet reads the proxy and PAC
    // settings out of HKCU — a hive the user owns — so the endpoint, the certificate
    // chain and the redirect target of every update request would be things the local
    // user (or anything running as them) could redefine. The manifest is signed and
    // verified before a field of it is parsed, so that was never a way to change WHAT
    // the update says; it was a way to change WHERE the request goes, whether it
    // completes, and whether the server's certificate chain is the real one.
    //
    // So: no proxy, no redirects, TLS 1.2 as a hard floor, and a status code that must
    // be read and must be 2xx. The redirect option's return value is checked rather
    // than discarded — a silently ignored option would leave redirects enabled while
    // the code reads as though they were off.
    DWORD redirectPolicy = WINHTTP_OPTION_REDIRECT_POLICY_NEVER;
    if (!WinHttpSetOption(t->session, WINHTTP_OPTION_REDIRECT_POLICY, &redirectPolicy,
                          sizeof(redirectPolicy))) {
        LOGE(L"Update: cannot disable redirects (err " + std::to_wstring(GetLastError()) +
             L"); refusing to fetch over an unconstrained redirect policy.");
        CloseHandles(t);
        t->Release();
        return Result::Failed;
    }

    t->connect = WinHttpConnect(t->session, parts.host.c_str(), parts.port, 0);
    if (!t->connect) {
        LOGE(L"Update: cannot connect to " + parts.host + L" (err " +
             std::to_wstring(GetLastError()) + L").");
        CloseHandles(t);
        t->Release();
        return Result::Failed;
    }
    t->AddRef();
    WinHttpSetStatusCallback(t->connect, OnStatus, WINHTTP_CALLBACK_FLAG_ALL_COMPLETIONS, 0);

    // WINHTTP_FLAG_ASYNC belongs to WinHttpOpen and ONLY there. Passing it here as
    // well fails with ERROR_INVALID_PARAMETER (87), the request handle is never
    // created, no callback ever fires, and a caller waiting on one waits forever --
    // which is exactly how this presented: a test that hung with almost no CPU time.
    t->request = WinHttpOpenRequest(t->connect, L"GET", parts.path.c_str(), nullptr,
                                    WINHTTP_NO_REFERER, WINHTTP_DEFAULT_ACCEPT_TYPES,
                                    parts.secure ? WINHTTP_FLAG_SECURE : 0);
    if (!t->request) {
        LOGE(L"Update: cannot create the request for " + url + L" (err " +
             std::to_wstring(GetLastError()) + L").");
        CloseHandles(t);
        t->Release();
        return Result::Failed;
    }
    t->AddRef();
    WinHttpSetStatusCallback(t->request, OnStatus, WINHTTP_CALLBACK_FLAG_ALL_COMPLETIONS, 0);

    // Hand the transfer to its own callbacks. Without this the callback receives a
    // null context and there is nothing to route the completion to.
    //
    // A DWORD_PTR holding the pointer, not a Transfer**, which is what the option
    // expects and what the callback reads back. Passing the address of the local
    // instead would hand the callback a pointer to a stack slot that is gone by the
    // time the next completion arrives.
    {
        DWORD_PTR ctx = BitCast<DWORD_PTR>(t);
        WinHttpSetOption(t->request, WINHTTP_OPTION_CONTEXT_VALUE, &ctx, sizeof(ctx));
    }

    if (parts.secure) {
        DWORD protocols = WINHTTP_FLAG_SECURE_PROTOCOL_TLS1_2;
        WinHttpSetOption(t->request, WINHTTP_OPTION_SECURE_PROTOCOLS, &protocols,
                         sizeof(protocols));
    }

    // Publish before the request is sent, so a Stop() arriving during the send can
    // reach the handle.
    //
    // The reuse check is here rather than at the top because a Request is one
    // transfer at a time: running it twice would make Stop() ambiguous about which
    // transfer it is cancelling.
    bool cancelledBeforeStart = false;
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        if (m_active) {
            LOGE(L"Update: Request reused; refusing to start a second transfer.");
            CloseHandles(t);
            t->Release();
            return Result::Failed;
        }
        m_active = t;
        t->AddRef();  // the caller-side (m_active) reference
        cancelledBeforeStart = m_cancelledBeforeStart;
    }

    // A Stop() that landed between Run() starting and the transfer being published
    // is honoured here, before a single byte is requested.
    if (cancelledBeforeStart) {
        t->Finish(Outcome::Cancelled, 0);
    } else if (!WinHttpSendRequest(t->request, WINHTTP_NO_ADDITIONAL_HEADERS, 0,
                                   WINHTTP_NO_REQUEST_DATA, 0, 0, 0)) {
        const unsigned long err = GetLastError();
        LOGE(L"Update: cannot start the request for " + url + L" (err " + std::to_wstring(err) +
             L").");
        t->Finish(Outcome::Failed, err);
    }

    // Wait for a terminal state, reporting progress as it arrives. This is where the
    // caller's thread spends the transfer: never inside a network call, only waiting
    // on an event. That is exactly what lets Stop() from another thread take effect
    // immediately rather than at the next timeout.
    const ProgressFn progress = [this] {
        std::lock_guard<std::mutex> lock(m_mutex);
        return m_progress;
    }();
    uint64_t reported = 0;
    for (;;) {
        HANDLE waits[2] = {t->done, t->wake};
        WaitForMultipleObjects(2, waits, FALSE, kTickMs);

        const uint64_t now = t->received.load(std::memory_order_acquire);
        if (progress && now != reported) {
            reported = now;
            progress(now);
        }

        if (t->outcome.load(std::memory_order_acquire) != Outcome::Pending) break;
        if (WaitForSingleObject(t->done, 0) == WAIT_OBJECT_0) break;
    }

    const Outcome outcome = t->outcome.load(std::memory_order_acquire);
    const unsigned long err = t->errorCode.load();

    if (outcome == Outcome::Ok) {
        // Hand the body over under the lock, so a callback cannot still be appending.
        std::lock_guard<std::mutex> lock(t->mx);
        out = std::move(t->body);
        t->body.clear();
    } else {
        out.clear();
    }

    // Terminal: no operation is outstanding, so every handle can go. Closing them
    // triggers the HANDLE_CLOSING callbacks that release their references.
    CloseHandles(t);

    // Drop the caller-side reference, then this thread's. The object survives until
    // the last HANDLE_CLOSING arrives, which is why none of the above needed to know
    // whether a callback was still in flight.
    //
    // The release is taken while the reference is cleared, under the same lock, so
    // Stop() can never observe a stale m_active. Clearing the pointer without
    // releasing it is how this leaked one Transfer per download.
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        Transfer* callerRef = m_active;
        m_active = nullptr;
        if (callerRef) callerRef->Release();
    }
    t->Release();

    if (outcome == Outcome::Ok) return Result::Ok;
    if (outcome == Outcome::Cancelled) {
        LOGI(L"Update: transfer cancelled: " + url);
        return Result::Cancelled;
    }
    if (err >= 400 && err < 600) {
        LOGE(L"Update: " + url + L" returned HTTP " + std::to_wstring(err) + L".");
    } else {
        LOGE(L"Update: request to " + url + L" failed (err " + std::to_wstring(err) + L").");
    }
    return Result::Failed;
}

bool Get(const std::wstring& url, std::string& out) {
    Request request;
    return request.Run(url, out) == Result::Ok;
}

}  // namespace Http
