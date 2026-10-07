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
// See the license file in the project root for full terms and conditions.

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
// This answers about ONE claim against whatever already holds the machine. The
// question a start actually asks is about a whole SET of claims at once, and that
// is a different question with a different answer — see CanBindAll.
//
// Returns false if the claim could not be bound for any reason: a conflict with a
// holder, a malformed address, an address this machine does not have, or an
// unavailable socket. Each of those means the service could not start, which is the
// question asked; which of them it was is what AuditClaims separates.
bool CanBind(const PortClaim& claim);

// Whether every claim in `claims` can be held at the same time, by binding the
// whole set and holding each socket until the last one has been tried.
//
// This is the question a start asks, and it is not the one CanBind answers. Each
// socket is kept open for the remainder of the pass, so a claim that only fails
// because one of ITS OWN siblings got there first is a failure here and a success
// there. Two claims for the same port name the same conflict a foreign holder
// would; two claims for different specific addresses of one port are a legitimate
// pair and both bind, which is exactly the distinction a comparison of endpoint
// text cannot make and why this binds rather than compares.
//
// Returns false if any claim could not be bound; `failed` (when given) receives
// those claims, in declaration order.
bool CanBindAll(const std::vector<PortClaim>& claims, std::vector<PortClaim>* failed = nullptr);

// Why a set of claims could not be bound, split by the one thing the caller has to
// do about it.
//
// All three are answers about the SAME set and can only be produced together. They
// are told apart by asking each failed claim again ON ITS OWN, and reading the reason
// the bind gave: a claim that binds alone was stopped by its own set, one that reports
// the endpoint already held was stopped by the machine, and one that reports the
// declaration itself cannot be satisfied was never a conflict at all.
//
// The split is the whole point, because the halves are answered in opposite ways and
// neither answer works for the other: one of them is "terminate what is in the way",
// and the other is "the declarations themselves cannot be brought up, so no amount of
// freeing a port will help".
struct ClaimAudit {
    // Claims something else on the machine is holding. Free these and the whole set
    // binds, so this is the half to act on by terminating what holds them.
    std::vector<PortClaim> unavailable;

    // Claims that bind on their own but not beside the rest of the set. Not a port
    // problem: the set is self-contradictory, and terminating anything would not
    // help while reporting it as an outside conflict names an innocent program.
    std::vector<PortClaim> contradicted;

    // Claims that cannot be bound as declared by anything, anywhere: an address
    // that is not a numeric literal, an address this machine does not have, or a
    // port the stack refuses to give to anyone. Nothing is in the way — no holder is
    // named for these, because there is none — and the declaration itself is what has
    // to change.
    //
    // A malformed literal lands here rather than in `unavailable` for a reason that
    // is not cosmetic: `unavailable` is answered by terminating the holder of the
    // endpoint, and the holder is found BY PORT. A claim whose address could not be
    // parsed would therefore name whoever happens to hold that port number on an
    // address it never named — an innocent program, terminated over a typo in a file.
    std::vector<PortClaim> invalid;

    // True when anything in the set would require the declarations to change, which
    // is the case no port cleanup can resolve. `unavailable` is deliberately NOT part
    // of this: a held port is answered by freeing it.
    bool HasConfigurationFault() const { return !contradicted.empty() || !invalid.empty(); }

    // The claims HasConfigurationFault() is about, for a log line that names what
    // has to be edited. Contradictions first, then the declarations that cannot be
    // bound: the first is a pair to look at, the second is a line to correct.
    std::vector<PortClaim> ConfigurationFaults() const;
};

// Classify `claims` by binding the whole set once and the failed members again
// individually. Every list is empty when everything binds, and a claim is in exactly
// one of them.
//
// Nothing is compared to decide which. A duplicated declaration is not detected by
// matching two claims against each other; it behaves exactly as it should — the
// second bind failing, the first succeeding alone — and lands in `contradicted`
// because of that. That is why this answers for any pair rather than for the ones
// someone thought to enumerate, including pairs that could not have been spotted by
// inspection, like a wildcard claim beside a specific one.
ClaimAudit AuditClaims(const std::vector<PortClaim>& claims);

// The PIDs actually in the way of `claim`: those holding it on an address the claim
// cannot coexist with.
//
// The address is the whole of what this adds over "every PID on the port", and it is
// not a refinement — it is the difference between naming the process that has to be
// dealt with and naming processes that are not involved. A port is a namespace per
// ADDRESS, not per number: measured on Windows 11, a holder on 127.0.0.2 does not
// stop a bind of 127.0.0.3, and 127/8 is all loopback, so both are ordinary
// endpoints. This list is walked to decide what to TERMINATE, so "everyone on the
// port" is the wrong answer in exactly the direction that does damage.
//
// A holder is in the way when it is on the claim's own address, when the CLAIM is the
// wildcard and therefore wants every address, or when the HOLDER is the wildcard and
// therefore covers the claim's address. An IPv6 wildcard holder counts against an
// IPv4 claim for the same reason: bound without V6ONLY, which is the platform
// default, it owns the IPv4 wildcard as well.
//
// A claim whose address is not a literal names nobody, which is correct twice over:
// nothing can be holding an address that does not exist, and a typo in a declaration
// must never send this after whoever happens to own that port number.
//
// Still only for naming and terminating. Whether the bind will succeed is answered by
// attempting it.
std::vector<DWORD> BlockersOf(const PortClaim& claim);

// Check if a PID is the System process (PID 4) or a critical system service
// that should never be killed. A process whose image path cannot be read counts
// as critical: without an identification there is no basis for terminating it.
bool IsSystemCritical(DWORD pid);

}  // namespace Ports
