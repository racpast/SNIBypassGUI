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

#include "dns/cancel.h"

#include <algorithm>

namespace Dns {

CancelToken::~CancelToken() {
    // Nothing may still be using a registered socket by the time the token dies,
    // so what is left here is a leak rather than a race. Closing it is still the
    // right answer: the alternative is leaking the handle.
    Cancel();
}

bool CancelToken::Register(SOCKET s) const {
    if (s == INVALID_SOCKET) return false;

    std::lock_guard<std::mutex> lock(m_mx);
    if (m_cancelled.load(std::memory_order_acquire)) {
        // Cancelled while this socket was being created. Closing it here rather
        // than returning it keeps the two halves in one place — the caller is
        // told it did not get registered, and never sees a handle that outlives
        // the cancellation.
        closesocket(s);
        return false;
    }
    m_sockets.push_back(s);
    return true;
}

bool CancelToken::Release(SOCKET s) const {
    std::lock_guard<std::mutex> lock(m_mx);
    const auto it = std::find(m_sockets.begin(), m_sockets.end(), s);
    if (it == m_sockets.end()) return false;
    m_sockets.erase(it);
    return true;
}

void CancelToken::Cancel() {
    // Set before the sockets are closed, so a task that checks the flag while
    // the closes are still in flight already knows to give up rather than
    // starting a fresh operation on a socket about to disappear.
    m_cancelled.store(true, std::memory_order_release);

    std::vector<SOCKET> doomed;
    {
        std::lock_guard<std::mutex> lock(m_mx);
        doomed.swap(m_sockets);
    }

    // Closed outside the lock: closesocket can block briefly in the kernel, and
    // a task trying to register its next socket must not wait on that.
    for (SOCKET s : doomed) {
        closesocket(s);
    }
}

}  // namespace Dns
