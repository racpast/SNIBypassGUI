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
#include <winsock2.h>

// The Winsock runtime, owned by whoever needs a socket.
//
// Every function in the Windows socket API fails with WSANOTINITIALISED until
// WSAStartup has run, and — this is the part that bites — `socket()` does not return
// an error and a usable handle just because the CALLER is careful. It returns
// INVALID_SOCKET, which is indistinguishable from "the system is out of handles"
// and is exactly what a caller checking only for failure will report as a real
// failure. That is not hypothetical: it is what made a port-availability probe
// answer "taken" for every endpoint on a machine where nothing was listening.
//
// So the rule this module exists to enforce is that a component needing a socket
// STARTS the runtime itself rather than assuming some other component already did.
// Idempotent, so the assumption costs nothing where it does hold; the process-wide
// runtime is exactly what is wanted, since a socket may be opened on one thread and
// closed on another.
//
// It lives in platform/ rather than beside the DNS servers, where it used to, because
// it is not a DNS concern: the port checker needs it and is not a DNS component, and
// a lower layer reaching up into dns/ for it would be the dependency running
// backwards. dns/ already includes platform/, so this direction is the existing one.
namespace SocketRuntime {

// Start Winsock for the process, once. Returns false if the runtime could not be
// started at all, which means no socket can be opened anywhere in this process.
//
// Called by every function that opens a socket rather than once at startup: a
// failure here is the caller's to report, and a single startup call would put the
// report somewhere unrelated to the operation that needed it.
bool Ensure();

}  // namespace SocketRuntime
