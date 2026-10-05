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
// Answer synthesis: turning a matched rule into the bytes a client is answered
// with.
//
// This is the half of what used to be message.* that knows about policy, split
// out so that the wire-format layer can stay wire-format. Only LocalResolver
// synthesizes answers — the forwarder relays whatever an encrypted upstream said
// and never constructs one — so this is the one place the rule table meets the
// message format, and nothing that merely speaks DNS has to be compiled against
// the rule layer.
//
// What stays in message.h is what both servers need: the header, the question,
// and the size a datagram may be. What lives here is the part that answers the
// question rather than relaying it.
#include <cstdint>
#include <vector>

#include "dns/message.h"
#include "dns/rules.h"

namespace Dns {

// How a query whose name matched a rule should be handled.
//   Answer  synthesize an address record
//   NoData  NOERROR with an empty answer section
//   Forward relay to a real upstream and pass its reply back
enum class Action { Forward, Answer, NoData };

// Which of the three a query type is owed. A and AAAA are answerable from a
// redirect; HTTPS and SVCB are forced to NODATA, because an alternative-endpoint
// or ECH record would let a client reach somewhere other than the loopback
// address the rule exists to point it at; everything else is forwarded, since a
// policy-table namespace routes every query type in, not just the redirectable
// ones.
Action DecideAction(uint16_t qtype);

// Build a response for `q` using `rule`. For a Redirect rule, Answer appends one
// A/AAAA record and NoData yields an empty NOERROR answer; a Redirect that lacks
// the queried family (e.g. an AAAA query against a v4-only rule) is downgraded to
// NODATA rather than emitting a default address. A Block rule yields NXDOMAIN
// regardless of `action`, as long as it is not Forward.
std::vector<uint8_t> BuildResponse(const uint8_t* query, size_t qlen, const Query& q,
                                   const Rule& rule, Action action);

}  // namespace Dns
