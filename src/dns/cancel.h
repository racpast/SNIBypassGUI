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
// Cancelling work that is already inside a blocking call.
//
// A flag on its own cannot stop anything: by the time a cancelled task looks at
// it, the task is asleep in select() or recv() waiting for a server that is
// never going to answer. So a token here carries both halves — the flag that
// says "stop", and the sockets that have to be woken for "stop" to mean
// anything.
//
// Closing the socket is what makes this work. It is the one operation that
// unblocks a select() waiting on that socket on Windows, and every call the
// clients make already tolerates its socket disappearing underneath it: a
// closed handle fails, it does not hang. The cost is that the handle must not
// be closed twice, which is why the token owns every socket registered with it
// and hands back a released handle rather than letting a caller close it.
//
// Registration and cancellation take the same lock, so a socket cannot be
// closed while it is being registered, and a socket registered after the token
// was cancelled is closed as it is registered.
#include <winsock2.h>

#include <atomic>
#include <cstdint>
#include <mutex>
#include <vector>

namespace Dns {

// One unit of cancellable work, shared by everything racing on its behalf.
//
// The token outlives no task: it is created by whoever starts the work, handed
// to every task by pointer, and cancelled once. A task holds a raw pointer and
// is joined before the token dies, which is why this is not passed by
// shared_ptr — the ownership is already expressed by the join.
class CancelToken {
public:
    CancelToken() = default;
    ~CancelToken();
    CancelToken(const CancelToken&) = delete;
    CancelToken& operator=(const CancelToken&) = delete;

    // True once Cancel() has been called. Cheap enough to sit in a loop; the
    // acquire pairs with the release in Cancel so a task that reads true also
    // sees everything the canceller did before cancelling.
    bool Cancelled() const { return m_cancelled.load(std::memory_order_acquire); }

    // Take ownership of `s` so that cancelling this token closes it.
    //
    // Returns false — having closed nothing, and without registering — if the
    // token was already cancelled, in which case the caller must close `s`
    // itself. Ownership passes to the token on success, including after it has
    // been released from the token by Release().
    //
    // Register and Release are const because they touch the registration list
    // rather than the token's own state: to the outside, a token is only ever
    // "cancelled" or "not cancelled". Holding a const token is what lets racing
    // code register its own socket without also being able to cancel the work
    // its siblings are doing.
    bool Register(SOCKET s) const;

    // Give up ownership of `s` without closing it, returning whether it was
    // registered. A caller that wants to keep using a socket after a
    // successful operation releases it and closes it on its own terms.
    bool Release(SOCKET s) const;

    // Close every registered socket. Idempotent, and safe to call from another
    // thread than the ones blocked on those sockets — that is the whole point.
    void Cancel();

private:
    std::atomic<bool> m_cancelled{false};
    mutable std::mutex m_mx;
    mutable std::vector<SOCKET> m_sockets;
};

}  // namespace Dns
