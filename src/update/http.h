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
#include <functional>
#include <mutex>
#include <string>

// HTTP GET over WinHTTP, with byte-level progress and cooperative cancellation.
//
// The transport is deliberately narrow. Every endpoint this program talks to is a
// compile-time constant, the bodies are small and fully verified afterwards, and
// the update path needs to show the user how far along it is and let them stop.
// That is the whole requirement, and it is what this does.
namespace Http {

// Outcome of one transfer. `Cancelled` is kept distinct from `Failed` because the
// caller must not report a user's own cancellation back to them as an error, and
// the two lead to different cleanup.
enum class Result {
    Ok,
    Cancelled,  // Stop() was called; the body is incomplete and must be discarded
    Failed,     // network, TLS, HTTP status or local failure
};

// The live state of one transfer. Opaque here and defined in http.cpp, so the
// WinHTTP types — and the callback plumbing that owns the object's lifetime — stay
// out of every translation unit that only wants to fetch a URL.
struct Transfer;

// One cancellable GET.
//
//   Http::Request request;
//   request.SetProgress([&](uint64_t n) { bytes.store(n); });
//   // on another thread, whenever the user gives up:
//   request.Stop();
//
// Lifetime: create it, call Run() on the thread that is willing to block, and call
// Stop() from any other thread to abort. The object must outlive both. Stop() is
// idempotent and safe to call before, during or after Run().
//
// Run() BLOCKS, but at no point does it block inside a network call. The transfer
// is asynchronous: WinHTTP delivers completions to a callback on its own thread,
// and Run() only waits to be told what happened. That is what makes Stop() work —
// a synchronous receive cannot be interrupted by anything except its own timeout.
//
// The mechanism, and why it is this one:
//
//   Cancelling closes the request handle. WinHttpReadData on a blocked synchronous
//   transfer cannot be reached by a flag: the thread is parked in the kernel and no
//   amount of checking will wake it. Closing the handle does wake it — the
//   documented result of a close mid-operation is ERROR_WINHTTP_OPERATION_CANCELLED
//   — and in asynchronous mode the close is the supported way to abandon an
//   operation rather than a way to break a working one.
//
//   Shortening the receive timeout and polling a flag between reads was measured and
//   does NOT work: a receive timeout does not return control mid-body, it tears the
//   connection down. Against a server that went quiet for 6 s, a 1.5 s receive
//   timeout failed with a connection error after 1 KiB of 3 KiB, while the same
//   transfer with a 60 s timeout completed. The timeout therefore stays generous,
//   and cancellation goes through the handle.
class Request {
public:
    // Called as the body arrives, with the CUMULATIVE bytes received so far. Runs on
    // the thread inside Run(), never on a WinHTTP callback thread, so it may call
    // Stop() and need not be reentrant-safe against itself.
    using ProgressFn = std::function<void(uint64_t received)>;

    Request();
    ~Request();
    Request(const Request&) = delete;
    Request& operator=(const Request&) = delete;

    // Set before Run(). May be null.
    void SetProgress(ProgressFn fn);

    // Fetch `url` into `out`, blocking until it completes or Stop() is called.
    //
    // `out` is cleared on entry and holds a complete body only on Result::Ok. On
    // Cancelled or Failed it is left empty, so a caller can never mistake a partial
    // body for a whole one — every caller here hashes what it gets, and a truncated
    // buffer would be reported as a hash mismatch, blaming the manifest for a
    // transport fault.
    Result Run(const std::wstring& url, std::string& out);

    // Abort a transfer in progress. Safe from any thread, at any time, including
    // before Run() and after it has returned.
    void Stop();

private:
    // Guards the three fields below.
    //
    // `m_active` is a raw pointer and NOT a shared_ptr, deliberately. The transfer's
    // lifetime is a reference count owned by WinHTTP as much as by this object —
    // every open handle holds one and drops it on its HANDLE_CLOSING callback — so a
    // second, independent ownership scheme here would free the object on the wrong
    // schedule. It is kept alive across Stop() by taking a reference explicitly.
    std::mutex m_mutex;
    Transfer* m_active = nullptr;
    ProgressFn m_progress;
    bool m_cancelledBeforeStart = false;
};

// GET `url` into `out` with no progress reporting and no cancellation. The manifest
// and its signature are small and fetched before anything is shown to the user, so
// they need neither. Returns true on a complete transfer.
//
// This is the shape every pre-existing caller already uses.
bool Get(const std::wstring& url, std::string& out);

}  // namespace Http
