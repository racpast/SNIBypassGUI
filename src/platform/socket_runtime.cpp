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

#include "platform/socket_runtime.h"

namespace SocketRuntime {

bool Ensure() {
    // A function-local static, so the initialization is thread-safe by the language
    // rather than by a lock this would otherwise have to add — and so a second caller
    // pays a guard variable and nothing else.
    //
    // WSAStartup is refcounted, so calling it repeatedly would not be wrong, but it
    // would mean every later caller could match it with a WSACleanup and tear the
    // runtime out from under sockets other threads still hold. Starting once and
    // never stopping is deliberate: this process is finished with sockets only when
    // it exits, at which point the kernel releases everything anyway. Tying a stop to
    // a static destructor instead would run it in an order no translation unit here
    // controls, while a worker thread may still be holding a socket.
    static const bool ready = [] {
        WSADATA data = {};
        return WSAStartup(MAKEWORD(2, 2), &data) == 0;
    }();
    return ready;
}

}  // namespace SocketRuntime
