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

#include "dns/answer.h"

namespace Dns {

Action DecideAction(uint16_t qtype) {
    if (qtype == kTypeA || qtype == kTypeAaaa) return Action::Answer;
    // Force HTTPS/SVCB to NODATA so an alternative-endpoint or ECH record cannot
    // bypass the loopback redirect for a redirected name.
    if (qtype == kTypeHttps || qtype == kTypeSvcb) return Action::NoData;
    return Action::Forward;
}

std::vector<uint8_t> BuildResponse(const uint8_t* query, size_t qlen, const Query& q,
                                   const Rule& rule, Action action) {
    if (action == Action::Forward) return {};

    // A Block rule answers NXDOMAIN for any query type.
    const bool block = (rule.action == RuleAction::Block);
    bool answer = !block && action == Action::Answer;

    // A Redirect rule only holds one address family. If the query asks for the
    // family this rule does not carry (AAAA against a v4-only rule, or A against
    // a v6-only rule), downgrade to NODATA (NOERROR, no answer) so the redirect
    // is not bypassed and no bogus default address is returned.
    if (answer) {
        if ((q.qtype == kTypeA && !rule.hasV4) || (q.qtype == kTypeAaaa && !rule.hasV6))
            answer = false;
    }

    // The response header is built by the wire-format layer, not here: both this
    // and a bare failure response have to agree about which flags a reply carries,
    // and the one place they can agree is the one place that writes them. Zeroing
    // ARCOUNT there is also what drops any EDNS OPT the client attached, which is
    // why an answer record can be appended below without a stale count
    // contradicting it.
    std::vector<uint8_t> r =
        BuildResponseHeader(query, qlen, q, block ? kRcodeNxDomain : kRcodeNoError);
    if (r.empty() || !answer) return r;

    r[7] = 1;            // ANCOUNT
    Put16(r, 0xC00C);    // NAME as a pointer to the question at offset 12
    Put16(r, q.qtype);   // TYPE (A or AAAA)
    Put16(r, kClassIn);  // CLASS
    Put32(r, rule.ttl);
    if (q.qtype == kTypeA) {
        Put16(r, sizeof(rule.v4));
        r.insert(r.end(), rule.v4, rule.v4 + sizeof(rule.v4));
    } else {
        Put16(r, sizeof(rule.v6));
        r.insert(r.end(), rule.v6, rule.v6 + sizeof(rule.v6));
    }
    return r;
}

}  // namespace Dns
