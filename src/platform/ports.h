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
// See the LICENSE.md file in the project root for full license terms.

#pragma once
#include <windows.h>

#include <cstdint>
#include <string>
#include <vector>

// Whether our services can have the endpoints they need, and who is in the way.
//
// A port is not a switch that is on or off, and "is anything listening on 443" is
// the wrong question. Whether a bind succeeds depends on the OPTIONS the holder
// chose, which are not visible from outside its process — so no amount of reading
// the system's tables can answer it. That was measured rather than reasoned about;
// the combinations that matter, all on one machine, are:
//
//   holder                    newcomer                    result
//   ------------------------  --------------------------  ----------------------
//   127.0.0.1:443  default    0.0.0.0:443    SO_REUSEADDR  both bind
//   0.0.0.0:443    default    0.0.0.0:443    default       second fails (WSAEADDRINUSE)
//   0.0.0.0:443    default    0.0.0.0:443    SO_REUSEADDR  second fails (WSAEACCES)
//   same address   REUSEADDR  same address   REUSEADDR     both bind, routing ambiguous
//   any address    EXCLUSIVE  same address   anything      second fails
//
// Two things follow. First, the only honest test is to attempt the bind the
// consumer itself would attempt, with `address`, `transport` and `mode` exactly as
// that consumer uses them — which is what PortClaim is and what CanBind does.
// Second, a specific address and the wildcard coexist in more combinations than
// they conflict in, so a service holding 127.11.11.11:443 is not a reason for one
// that wants 0.0.0.0:443 to refuse to start: the bind itself will say so.
//
// For the diagnostics that name what is in the way — and for the decision to
// terminate a holder — the tables are still what is read, because a PID is only
// discoverable there. Reading them is not a substitute for CanBind; it is what
// runs afterwards, once CanBind has already established that something is wrong.
namespace Ports {

// Which of the two independent port namespaces a claim is about.
//
// Independent is exact: TCP and UDP have separate port spaces, and a service
// holding UDP 443 does not stop anything binding TCP 443. A DNS server needs both,
// a web server needs TCP alone.
enum class Transport {
    Tcp,
    Udp,
    Both,
};

// How the consumer binds, which is what decides whether a held port is a conflict
// at all. These are the three spellings a socket has, not a preference.
enum class BindMode {
    // No option set. The usual case for a program that never thought about this;
    // it can coexist with a specific-address holder but not with a wildcard one.
    Default,

    // SO_REUSEADDR. Binds even alongside another REUSEADDR holder on the same
    // address — the combination Microsoft documents as producing undefined packet
    // routing, which is why a claim files it under a question rather than an
    // entitlement.
    Reuse,

    // SO_EXCLUSIVEADDRUSE, which this program's own listeners use. Nothing else can
    // hold any part of the namespace it takes, and nothing else can take it after.
    Exclusive,
};

// One endpoint a service needs, declared rather than guessed at.
//
// The fields are the ones that decide the bind, so a declaration is exactly as
// precise as the question "can this service start" needs to be. `address` is a
// numeric literal: a wildcard claim conflicts with far more than a specific one,
// and treating them alike is the mistake this type exists to stop making.
struct PortClaim {
    std::wstring address;
    uint16_t port = 0;
    Transport transport = Transport::Tcp;
    BindMode mode = BindMode::Default;

    // "address:port", with the transport and mode spelled out when they are not the
    // defaults: "0.0.0.0:443", "0.0.0.0:53/both/exclusive".
    //
    // For the LOG. It is the full claim, because a line saying why a bind failed is
    // read by someone diagnosing the bind, and the mode is the part that says which
    // conflicts were possible. It is NOT for anything shown to a user — see Describe().
    std::wstring Text() const;

    // The same endpoint as a person reads it: "TCP 0.0.0.0:443",
    // "UDP 127.0.0.1:53", "TCP/UDP 127.191.98.10:53".
    //
    // The bind mode is deliberately absent, and that is the whole reason this exists
    // beside Text(). The mode decides whether a held port is a CONFLICT at all, so it
    // is indispensable to the check — but it is an internal property of a socket that
    // the user cannot act on. "/reuse" and "/exclusive" in a dialog ask the reader to
    // understand a distinction that changes nothing they can do about it.
    //
    // The transport IS shown, because it is the opposite: "TCP 443" and "UDP 443" are
    // different conflicts with different owners, and knowing which one is held is what
    // tells a user where to look. TCP and UDP are protocol names, not words, so this
    // is the same string in every language and needs no table entry.
    std::wstring Describe() const;
};

// Whether `claim` can be bound right now, by trying it.
//
// Opens a socket with exactly the family, transport and option the claim names,
// binds to exactly that address and port, and closes it again. Nothing is listened
// on and nothing is served: the bind either succeeds or it does not, and either
// answer is the whole question.
//
// A claim naming both transports is satisfiable only if both bind, since the
// consumer needs both and would fail on whichever it reached second.
//
// Returns false only for a bind that failed. A malformed address, an unavailable
// socket, or a failure to set the requested option also returns false — each of
// those means the service could not start either, which is the question asked.
bool CanBind(const PortClaim& claim);

// PIDs holding a LISTEN on `port`, across IPv4 and IPv6, deduplicated.
//
// For naming what is in the way, never for deciding whether anything is. See the
// header comment: a listing says WHO, and only a bind says WHETHER.
std::vector<DWORD> ListenersOn(int port);

// PIDs holding a bound UDP socket on `port`. UDP has no LISTEN state, so this is
// every socket that has bound the port rather than every one waiting on it.
std::vector<DWORD> UdpHoldersOn(int port);

// Check if a PID is the System process (PID 4) or a critical system service
// that should never be killed. A process whose image path cannot be read counts
// as critical: without an identification there is no basis for terminating it.
bool IsSystemCritical(DWORD pid);

}  // namespace Ports
