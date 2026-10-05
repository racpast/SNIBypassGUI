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

#include "dns/message.h"

#include <utility>

namespace Dns {

// DNS header and record fields are big-endian on the wire.
uint16_t Read16(const uint8_t* p) {
    return static_cast<uint16_t>((static_cast<unsigned>(p[0]) << 8u) |
                                 static_cast<unsigned>(p[1]));
}

void Put16(std::vector<uint8_t>& v, uint16_t x) {
    v.push_back(static_cast<uint8_t>(static_cast<unsigned>(x) >> 8u));
    v.push_back(static_cast<uint8_t>(x));
}

void Put16At(uint8_t* p, uint16_t x) {
    p[0] = static_cast<uint8_t>(static_cast<unsigned>(x) >> 8u);
    p[1] = static_cast<uint8_t>(x);
}

void Put32(std::vector<uint8_t>& v, uint32_t x) {
    v.push_back(static_cast<uint8_t>(x >> 24u));
    v.push_back(static_cast<uint8_t>(x >> 16u));
    v.push_back(static_cast<uint8_t>(x >> 8u));
    v.push_back(static_cast<uint8_t>(x));
}

namespace {

// Walk the NAME at `off`, the one implementation of that step in the program.
//
// Three callers need it and they need different things from it, which is why it
// is one function with a flag rather than three loops: a question is read (the
// labels go somewhere), a record name is skipped in a response (compression is
// expected and pointers have to be followed), and a record name is skipped in a
// packet whose framing this code does not control (a pointer is not expected,
// and following one would read wherever it points).
//
// `follow` decides which of the last two applies. `out`, when not null, is
// filled with the dotted, lowercased name; lengths are then checked against
// RFC 1035's limits, which a caller that only skips does not need to enforce
// because it is not the one producing a name.
//
// On success `off` is one past the name *as encoded at* `off` — which, for a
// name that left by a pointer, is two bytes later, not the far end of the name
// the pointer names. Getting that wrong would leave every record's fixed block
// four bytes early. On failure `off` is left where the walk gave up and `out` is
// unchanged.
//
// Two things are refused: a pointer that does not point strictly backwards,
// which is what stops a pointer cycle from looping forever, and a label that
// runs past the end of the message.
bool WalkName(const uint8_t* dns, size_t len, size_t& off, bool follow, std::string* out) {
    const size_t start = off;
    bool left = false;  // the name left `start` by a pointer
    size_t pos = off;
    std::string collected;

    // A compressed name can chain, but each hop must move backwards, so no more
    // hops than there are bytes can ever happen. The cap is what keeps a
    // pathological message from turning a walk into a spin even if the
    // backwards rule is somehow satisfied.
    for (size_t hops = 0; hops <= len; ++hops) {
        if (pos >= len) return false;
        const uint8_t label = dns[pos];
        if (label == 0) {
            ++pos;
            // The name ends at the root label that terminated it — unless it was
            // entered through a pointer, in which case its own on-wire extent was
            // the two pointer bytes at `start`.
            off = left ? start + 2 : pos;
            if (out != nullptr) *out = std::move(collected);
            return true;
        }
        if ((label & 0xC0u) == 0xC0u) {
            if (!follow) return false;
            if (pos + 1 >= len) return false;
            const size_t target =
                static_cast<size_t>(((static_cast<unsigned>(label) & 0x3Fu) << 8u) |
                                    static_cast<unsigned>(dns[pos + 1]));
            if (target >= pos) return false;  // must point backwards
            if (!left) {
                left = true;
                off = pos + 2;
            }
            pos = target;
            continue;
        }
        if ((label & 0xC0u) != 0) return false;  // 0x40/0x80 are not label encodings
        ++pos;
        if (pos + label > len) return false;
        if (out != nullptr) {
            if (!collected.empty()) collected.push_back('.');
            for (uint8_t i = 0; i < label; ++i)
                collected.push_back(AsciiLower(static_cast<char>(dns[pos + i])));
            if (collected.size() > kMaxNameChars) return false;  // RFC 1035 §2.3.1
        }
        pos += label;
    }
    return false;
}

}  // namespace

bool SkipName(const uint8_t* dns, size_t len, size_t& off) {
    if (dns == nullptr) return false;
    return WalkName(dns, len, off, true, nullptr);
}

bool EncodeName(const std::string& name, std::vector<uint8_t>& out) {
    std::vector<uint8_t> encoded;
    encoded.reserve(name.size() + 2);

    size_t start = 0;
    while (start <= name.size()) {
        const size_t dot = name.find('.', start);
        const size_t end = (dot == std::string::npos) ? name.size() : dot;
        const size_t len = end - start;

        // The root is the empty name, and it is the one empty label that means
        // something. Anywhere else a zero-length label is a malformed name —
        // "a..b", or a trailing dot — and writing it as a 0 byte would silently
        // turn the rest of the name into the end of it.
        if (len == 0) {
            if (name.empty()) {
                encoded.push_back(0);
                out.insert(out.end(), encoded.begin(), encoded.end());
                return true;
            }
            return false;
        }
        // RFC 1035 §2.3.4. Without this the length byte is written by casting an
        // unbounded size down to uint8_t, which produces a legal-looking byte
        // and a message the server rejects as malformed with nothing to point at.
        if (len > kMaxLabelLength) return false;
        // §2.3.1: 255 bytes covers the labels, the length bytes and the root.
        if (encoded.size() + 1 + len + 1 > kMaxNameLength) return false;

        encoded.push_back(static_cast<uint8_t>(len));
        encoded.insert(encoded.end(), name.begin() + static_cast<ptrdiff_t>(start),
                       name.begin() + static_cast<ptrdiff_t>(end));
        if (dot == std::string::npos) break;
        start = dot + 1;
    }

    encoded.push_back(0);  // root label
    out.insert(out.end(), encoded.begin(), encoded.end());
    return true;
}

bool ParseQuery(const uint8_t* dns, size_t len, Query& out) {
    if (!dns || len < 12) return false;
    out.id = Read16(dns);
    out.flags = Read16(dns + 2);
    if (Read16(dns + 4) < 1) return false;  // QDCOUNT

    size_t off = 12;
    // Compression is not expected in a question, and a question that arrives
    // compressed is one this is not prepared to have interpreted: a pointer here
    // is either a broken client or a deliberate shape, and both are refused
    // rather than guessed at.
    std::string name;
    if (!WalkName(dns, len, off, false, &name)) return false;

    if (off + 4 > len) return false;
    out.qtype = Read16(dns + off);
    out.qclass = Read16(dns + off + 2);
    out.name = std::move(name);
    out.questionEnd = off + 4;
    return true;
}

bool WalkRecords(const uint8_t* dns, size_t len, bool (*fn)(const RecordSpan&, void*),
                 void* ctx) {
    if (!dns || len < 12) return false;

    const uint16_t counts[3] = {Read16(dns + 6), Read16(dns + 8), Read16(dns + 10)};

    // The question precedes the records. Walking past it rather than trusting the
    // caller's own parse is what keeps this usable from a context that has no
    // Query at hand, and it is the same bounds-checked step either way.
    size_t off = 12;
    for (uint16_t i = 0; i < Read16(dns + 4); ++i) {
        if (!SkipName(dns, len, off)) return false;
        if (off + 4 > len) return false;
        off += 4;  // QTYPE + QCLASS
    }

    for (uint16_t section = 0; section < 3; ++section) {
        for (uint16_t i = 0; i < counts[section]; ++i) {
            RecordSpan span;
            span.offset = off;
            if (!SkipName(dns, len, off)) return false;
            if (off + 10 > len) return false;
            const uint16_t rdlength = Read16(dns + off + 8);
            off += 10;
            if (off + rdlength > len) return false;
            off += rdlength;
            span.length = off - span.offset;
            if (fn != nullptr && !fn(span, ctx)) return false;
        }
    }
    return true;
}

size_t QueryUdpPayloadSize(const uint8_t* dns, size_t len) {
    if (!dns || len < 12) return kMinUdpPayload;
    if ((Read16(dns + 2) & 0x8000u) != 0) return kMinUdpPayload;  // a response, not a query

    // Only the additional section is of interest, and the OPT has to be in it.
    // Walking the answer and authority sections of a *query* is not meaningful —
    // they are empty by definition — so those counts are skipped, not walked.
    size_t off = 12;
    for (uint16_t i = 0; i < Read16(dns + 4); ++i) {
        if (!SkipName(dns, len, off)) return kMinUdpPayload;
        if (off + 4 > len) return kMinUdpPayload;
        off += 4;
    }

    size_t payload = kMinUdpPayload;
    const uint16_t additional = Read16(dns + 10);
    for (uint16_t i = 0; i < additional; ++i) {
        if (!SkipName(dns, len, off)) return kMinUdpPayload;
        if (off + 10 > len) return kMinUdpPayload;
        const uint16_t type = Read16(dns + off);
        const uint16_t rdlength = Read16(dns + off + 8);
        if (type == kTypeOpt) {
            // RFC 6891: the requestor's payload size lives in the CLASS field, and
            // there may be only one OPT. Its RDLENGTH is 0 in a query.
            if (rdlength != 0) return kMinUdpPayload;
            payload = static_cast<size_t>(Read16(dns + off + 2));
            break;
        }
        off += 10;
        if (off + rdlength > len) return kMinUdpPayload;
        off += rdlength;
    }

    if (payload < kMinUdpPayload) return kMinUdpPayload;
    return payload;
}

namespace {

// What a WalkRecords walk needs while finding the prefix that fits: the largest
// record end at or below the budget. Passing a record advances it, so the value
// left behind when the walk stops early is exactly the prefix to keep.
struct BudgetWalk {
    size_t limit = 0;
    size_t lastGood = 0;  // end offset of the last record that fit
};

bool KeepIfFits(const RecordSpan& span, void* ctx) {
    BudgetWalk& walk = *static_cast<BudgetWalk*>(ctx);
    if (span.offset + span.length > walk.limit) return false;
    walk.lastGood = span.offset + span.length;
    return true;
}

// Counts the records that survive inside a prefix, per section.
//
// Section membership comes from the header's own counts: the walker yields every
// record in the message in order, so the first ANCOUNT of them are answers, the
// next NSCOUNT authorities, and the rest additionals. A record whose end lies past
// the prefix is the first casualty of the cut, and nothing after it survived
// either.
struct SurvivorTally {
    size_t limit = 0;
    uint16_t declared[3] = {0, 0, 0};
    size_t index = 0;  // records seen so far, cumulatively
    size_t kept[3] = {0, 0, 0};
};

bool TallyIfInside(const RecordSpan& span, void* ctx) {
    SurvivorTally& tally = *static_cast<SurvivorTally*>(ctx);
    if (span.offset + span.length > tally.limit) return false;

    // Which section this record belongs to, decided by how many have come before.
    size_t section = 2;
    size_t remaining = tally.index;
    for (size_t i = 0; i < 2; ++i) {
        if (remaining < tally.declared[i]) {
            section = i;
            break;
        }
        remaining -= tally.declared[i];
    }
    ++tally.kept[section];
    ++tally.index;
    return true;
}

}  // namespace

UdpBudget ApplyUdpBudget(const uint8_t* query, size_t qlen, std::vector<uint8_t>& response,
                         size_t budget) {
    Query q;
    if (!ParseQuery(query, qlen, q)) {
        // Without the question there is no way to tell where the client's copy
        // ends and the records begin, so nothing can be cut safely.
        return UdpBudget::CannotFit;
    }
    if (q.questionEnd > response.size()) return UdpBudget::CannotFit;

    // The header and question are never dropped: a reply that does not repeat the
    // question is one a client will not match to its query at all. Below this
    // there is no truncated answer to send, only a failure to report.
    if (q.questionEnd > budget) return UdpBudget::CannotFit;
    if (response.size() <= budget) return UdpBudget::Fits;
    if (response.size() < 12) return UdpBudget::CannotFit;

    // Find the largest record boundary that fits. A response this cannot walk — a
    // malformed one, or one whose counts disagree with its bytes — keeps only its
    // question: the question's layout is already known from the query, so that is
    // the one cut that is safe without understanding the body.
    //
    // Whether the walk stopped because a record crossed the budget or because the
    // message turned out to be malformed does not have to be told apart here.
    // Either way `lastGood` is the end of the last record that was fully walked,
    // seeded with the question, and that is exactly the prefix to keep.
    BudgetWalk walk;
    walk.limit = budget;
    walk.lastGood = q.questionEnd;
    WalkRecords(response.data(), response.size(), KeepIfFits, &walk);
    const size_t keep = walk.lastGood;

    // Re-count what survived, then describe exactly that in the header. A count
    // that overstates the message is how a client ends up reading past the end of
    // the datagram into whatever follows it.
    SurvivorTally tally;
    tally.limit = keep;
    tally.declared[0] = static_cast<uint16_t>((static_cast<unsigned>(response[6]) << 8u) |
                                              static_cast<unsigned>(response[7]));
    tally.declared[1] = static_cast<uint16_t>((static_cast<unsigned>(response[8]) << 8u) |
                                              static_cast<unsigned>(response[9]));
    tally.declared[2] = static_cast<uint16_t>((static_cast<unsigned>(response[10]) << 8u) |
                                              static_cast<unsigned>(response[11]));
    WalkRecords(response.data(), response.size(), TallyIfInside, &tally);

    Put16At(response.data() + 6, static_cast<uint16_t>(tally.kept[0]));
    Put16At(response.data() + 8, static_cast<uint16_t>(tally.kept[1]));
    Put16At(response.data() + 10, static_cast<uint16_t>(tally.kept[2]));

    // TC is the whole point of the exercise: it is what tells the client the
    // answer is incomplete and that asking again over TCP will get the rest.
    // Nothing else about the message says so — an oversized datagram is simply
    // dropped in transit, and a client that hears nothing repeats over UDP rather
    // than escalating.
    response[2] = static_cast<uint8_t>(static_cast<unsigned>(response[2]) | 0x02u);
    response.resize(keep);
    return UdpBudget::Truncated;
}

std::vector<uint8_t> BuildResponseHeader(const uint8_t* query, size_t qlen, const Query& q,
                                         uint8_t rcode) {
    if (q.questionEnd == 0 || q.questionEnd > qlen) return {};
    std::vector<uint8_t> r(query, query + q.questionEnd);
    r[2] = 0x81;                                 // QR=1, opcode 0, RD=1
    r[3] = static_cast<uint8_t>(0x80u | rcode);  // RA=1, RCODE
    r[6] = 0;
    r[7] = 0;  // ANCOUNT
    r[8] = 0;
    r[9] = 0;  // NSCOUNT
    r[10] = 0;
    r[11] = 0;  // ARCOUNT
    return r;
}

std::vector<uint8_t> BuildStatusResponse(const uint8_t* query, size_t qlen, const Query& q,
                                         uint8_t rcode) {
    return BuildResponseHeader(query, qlen, q, rcode);
}

bool IsTruncated(const uint8_t* dns, size_t len) {
    if (!dns || len < 12) return false;
    const uint16_t flags = static_cast<uint16_t>((static_cast<unsigned>(dns[2]) << 8u) |
                                                 static_cast<unsigned>(dns[3]));
    return (flags & 0x0200u) != 0;  // TC bit is bit 9 (0x0200)
}

}  // namespace Dns
