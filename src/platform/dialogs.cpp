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

#include "platform/dialogs.h"

#include <condition_variable>
#include <cstdint>
#include <cstring>
#include <mutex>
#include <vector>

#include "app/i18n.h"
#include "app/text.h"
#include "app/version.h"

namespace Dialogs {
namespace {

// The application-defined message that carries a dialog request to the UI thread.
// WM_APP + 2, next to the tray's own WM_APP + 1.
constexpr UINT kShowMessage = WM_APP + 2;

// The armed destination, read and written under `g_mutex`.
struct Destination {
    HWND owner = nullptr;
    DWORD uiThread = 0;
};

std::mutex g_mutex;
Destination g_dest;

// One dialog request, in flight between the thread that asked for it and the thread
// that shows it.
//
// The object lives on the REQUESTER's stack and is never heap-allocated, because the
// requester is blocked for the whole of its life — the payload cannot outlive the
// wait, and the UI thread cannot reach it after the wait returns.
struct Request {
    enum class State : std::uint8_t {
        Queued,     // posted, not yet picked up by the UI thread
        Running,    // the UI thread is inside the dialog
        Completed,  // answered
        Abandoned,  // the window went away before the UI thread could pick it up
    };

    std::wstring text;
    UINT flags = MB_OK;

    std::mutex mutex;
    std::condition_variable done;
    State state = State::Queued;
    int result = IDOK;

    // Show it and record the answer. Called on whichever thread ends up owning it.
    void RunOn(HWND owner) {
        const int r = MessageBoxW(owner, text.c_str(), APP_NAME, flags);
        std::lock_guard<std::mutex> lock(mutex);
        result = r;
        state = State::Completed;
        done.notify_one();
    }

    // Give up on the request before it was picked up, and release the waiter.
    //
    // Only ever called on a request that is still Queued. A request that has reached
    // Running is being handled by a UI thread that is provably still running, so
    // nothing else may touch it.
    void Abandon() {
        std::lock_guard<std::mutex> lock(mutex);
        state = State::Abandoned;
        done.notify_one();
    }
};

Destination Snapshot() {
    std::lock_guard<std::mutex> lock(g_mutex);
    return g_dest;
}

// Every request that has been posted but not yet picked up. Guarded by `g_mutex`, and
// touched by both the requesting thread and the UI thread.
//
// This exists for exactly one moment: the window being destroyed while a worker is
// still waiting for an answer. Without it that worker waits forever, because the
// message loop that would have dispatched its request has already returned — and it
// waits while holding a reference to the Runtime, which turns the exit into a hang.
// Requests are on their requesters' stacks, so this holds pointers, never ownership.
std::vector<Request*> g_pending;

// Remove `request` from the pending set, if present. Both the failed-post path and the
// UI thread's dispatch need it, so it lives here rather than inline twice.
void Deregister(Request* request) {
    std::lock_guard<std::mutex> lock(g_mutex);
    for (auto it = g_pending.begin(); it != g_pending.end(); ++it) {
        if (*it == request) {
            g_pending.erase(it);
            return;
        }
    }
}

int ShowInline(HWND owner, const std::wstring& text, UINT flags) {
    return MessageBoxW(owner, text.c_str(), APP_NAME, flags);
}

}  // namespace

void Init(HWND owner) {
    std::lock_guard<std::mutex> lock(g_mutex);
    g_dest.owner = owner;
    g_dest.uiThread = GetCurrentThreadId();
}

HWND Owner() {
    std::lock_guard<std::mutex> lock(g_mutex);
    return g_dest.owner;
}

void Shutdown() {
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        g_dest.owner = nullptr;
        g_dest.uiThread = 0;
    }

    // Any request still waiting to be picked up will never be, now that the loop that
    // would have dispatched it has already returned. Release those waiters rather than
    // leaving them blocked for the lifetime of the process.
    //
    // A request is safe to reach here only while it is still Queued, which is exactly
    // the set that has no handler. The transition to Running happens on the UI thread
    // before it enters the dialog, and it cannot be mid-transition while Shutdown runs:
    // the UI thread executes Shutdown itself, after its message loop has returned, so
    // it is not also dispatching a message at this instant. Every post that could have
    // been picked up was already in the queue before the loop ended, and so was
    // consumed.
    std::vector<Request*> pending;
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        pending.swap(g_pending);
    }
    for (Request* request : pending) request->Abandon();
}

int Show(const std::wstring& text, UINT flags) {
    const Destination dest = Snapshot();

    // No window, or already on its thread: nothing to marshal.
    if (!dest.owner || dest.uiThread == GetCurrentThreadId())
        return ShowInline(dest.owner, text, flags);

    Request request;
    request.text = text;
    request.flags = flags;

    // Registered BEFORE the post, so a Shutdown that happens between the two finds it.
    // The other order leaves a window in which the request has been queued for a
    // window that is being disarmed and no one knows about it yet.
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        g_pending.push_back(&request);
    }

    // Shutdown may have abandoned it between the two statements above. Posting now
    // would leave the request in a queue nothing will ever read; the wait below would
    // still return, so this is tidiness rather than correctness.
    {
        std::lock_guard<std::mutex> lock(request.mutex);
        if (request.state == Request::State::Abandoned) {
            Deregister(&request);
            return IDCANCEL;
        }
    }

    if (!PostMessageW(dest.owner, kShowMessage, 0, reinterpret_cast<LPARAM>(&request))) {
        Deregister(&request);
        // The window went away between the snapshot and the post. Showing it here is
        // the only remaining way to deliver the message; it is unowned, but this is
        // the shutdown edge, not the ordinary path.
        return ShowInline(nullptr, text, flags);
    }

    std::unique_lock<std::mutex> lock(request.mutex);
    request.done.wait(lock, [&request] { return request.state != Request::State::Queued; });
    return request.state == Request::State::Abandoned ? IDCANCEL : request.result;
}

void HandleShowMessage(HWND owner, LPARAM lp) {
    void* requestPtr = nullptr;
    std::memcpy(reinterpret_cast<void*>(&requestPtr), &lp, sizeof(requestPtr));
    auto* request = static_cast<Request*>(requestPtr);

    // Deregister before showing, so Shutdown can no longer consider it pending. From
    // here the request belongs to this thread until the answer is recorded — and this
    // thread is the one that runs Shutdown, so the two can never interleave.
    Deregister(request);

    request->RunOn(owner);
}

bool IsShowMessage(UINT msg) {
    return msg == kShowMessage;
}

}  // namespace Dialogs
