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
// DNS message wire format: the header, one question, and the size a datagram is
// allowed to be.
//
// This is the layer both DNS servers share and the only DNS layer either of them
// needs to agree on. It deliberately knows nothing about rules or policy: what a
// query is (ParseQuery), how big the response to it may be (QueryUdpPayloadSize)
// and how to cut one down to that size (ApplyUdpBudget) are wire-format questions
// with the same answers whether the query is being answered from a rule table or
// relayed to an encrypted upstream.
//
// There is no reader for answer or authority records beyond skipping them, and
// that is the point: bytes neither server synthesizes are bytes it never has to
// understand. The one thing it does have to do with them is measure them, which
// is what the record walker below is for.
#include <cstddef>
#include <cstdint>
#include <string>
#include <vector>

namespace Dns {

// Big-endian field access. DNS is a big-endian wire format and every layer that
// reads or writes it needs these, so they live here rather than being spelled out
// again in each translation unit that talks to the wire.
uint16_t Read16(const uint8_t* p);
void Put16(std::vector<uint8_t>& v, uint16_t x);
void Put32(std::vector<uint8_t>& v, uint32_t x);

// Lowercase one ASCII letter, leaving everything else alone.
//
// DNS names are compared case-insensitively (RFC 1035 §2.3.3) and this is the fold
// for them: ASCII only, because a DNS label is a sequence of octets and has no
// encoding to consult a locale about, and ordinal, so the answer cannot depend on
// process state the way std::tolower's does.
//
// Here rather than in each file that needs one. It had been written twice — once in
// message.cpp and once in rules.cpp, character for character — which is what happens
// to a four-line helper that no header owns.
inline char AsciiLower(char c) {
    return (c >= 'A' && c <= 'Z') ? static_cast<char>(c - 'A' + 'a') : c;
}

// Overwrite a big-endian 16-bit field in place, for a count that has to be
// corrected after the bytes are already laid out.
void Put16At(uint8_t* p, uint16_t x);

enum : uint16_t { kTypeA = 1, kTypeAaaa = 28, kTypeSvcb = 64, kTypeHttps = 65 };
enum : uint16_t { kClassIn = 1 };

// The response codes these servers ever put on the wire themselves.
enum : uint8_t { kRcodeNoError = 0, kRcodeServFail = 2, kRcodeNxDomain = 3 };

// EDNS0, and the RFC 6891 requestor's payload size an OPT record carries in the
// CLASS field of its RR. 512 is the size a client that sends no OPT implicitly
// asks for (RFC 1035 §4.2.1), so it is the floor of every negotiation here.
enum : uint16_t { kTypeOpt = 41 };
inline constexpr size_t kMinUdpPayload = 512;

// DNS name limits, from RFC 1035 §2.3.4 and §2.3.1.
//
// The label limit is 63 rather than 64 because the top two bits of the length
// byte are reserved by the compression scheme (§4.1.4): a length of 64 or more
// is read as a pointer, so a label written that way is not a long label, it is
// a different byte sequence entirely.
inline constexpr size_t kMaxLabelLength = 63;
inline constexpr size_t kMaxNameLength = 255;

// The longest a dotted name can be once its labels and length bytes are
// accounted for: 255 minus the length bytes that do not survive decoding.
// Used where a name is being built in memory rather than encoded.
inline constexpr size_t kMaxNameChars = 253;

// The largest UDP response we are willing to send regardless of what the client
// asked for.
//
// DNS Flag Day 2020 settled on 1232: a response at or below it fits the smallest
// MTU a path to it is likely to have without being fragmented, and a fragmented
// DNS reply is one an off-path attacker can drop or partially spoof. Advertising
// a larger buffer than this is a thing clients do, not a thing they are owed.
inline constexpr size_t kMaxUdpPayload = 1232;

// A parsed DNS question (the first question only).
struct Query {
    uint16_t id = 0;
    uint16_t flags = 0;
    std::string name;  // lowercased, dotted, no trailing dot
    uint16_t qtype = 0;
    uint16_t qclass = 0;
    size_t questionEnd = 0;  // offset just past QNAME+QTYPE+QCLASS
};

// Check if a DNS response has the TC (truncated) flag set, meaning it was too
// large for UDP and should be retried over TCP.
bool IsTruncated(const uint8_t* dns, size_t len);

// Parse the header and first question of a DNS message. Bounds checked; returns
// false on a malformed, compressed, or empty question.
bool ParseQuery(const uint8_t* dns, size_t len, Query& out);

// Step over the NAME at `off`, following compression pointers.
//
// On success `off` is one past the name *as encoded at* `off` — which, for a
// record whose name is a pointer, is two bytes later, not the far end of the
// name the pointer names. Getting that wrong leaves every record's fixed block
// four bytes early, so it is worth stating twice.
//
// A name in a response is not required to be uncompressed the way a question is
// — it is the compression that makes a large answer fit at all — so this has to
// follow pointers rather than reject them. Two things are refused: a pointer that
// does not point strictly backwards, which is what stops a pointer cycle from
// looping forever, and a label that runs past the end of the message. Returns
// false in either case, having left `off` where the failure was found.
bool SkipName(const uint8_t* dns, size_t len, size_t& off);

// Append `name` to `out` as DNS labels, terminated by the root label.
//
// `name` is dotted with no trailing dot; the root itself is the empty string,
// which encodes as a single zero byte.
//
// Bounds are the whole reason this exists as a function. A label is at most 63
// bytes (RFC 1035 §2.3.4) and the encoded name at most 255 (§2.3.1), and a
// caller that writes the length byte by casting an unbounded size to uint8_t
// emits an illegal label byte rather than an error — a message the server it is
// sent to rejects as malformed, with nothing in the log to say why. So an empty
// label, one over 63 bytes, or a name over 255 bytes is refused outright and
// `out` is left exactly as it was found, because half an encoded name is worse
// than none.
bool EncodeName(const std::string& name, std::vector<uint8_t>& out);

// The UDP payload size a query's EDNS0 OPT record asks for, or kMinUdpPayload
// (512) when it carries no OPT — which is the size a client that says nothing is
// taken to have asked for.
//
// Bounds checked and total: a malformed question, a truncated OPT or an OPT that
// is not the last record in the additional section all yield the 512 default
// rather than an error, because the caller's only use for the answer is to pick a
// datagram size and a wrong-but-conservative answer costs one retry over TCP,
// while no answer at all would cost the query.
size_t QueryUdpPayloadSize(const uint8_t* dns, size_t len);

// One resource record as the walker below sees it: where it starts and how long
// it is, so a caller can cut the message at the boundary between two of them.
struct RecordSpan {
    size_t offset = 0;  // first byte of the NAME
    size_t length = 0;  // NAME + TYPE + CLASS + TTL + RDLENGTH + RDATA
};

// Walk the sections of `dns` in order, calling `fn` for each whole record with
// its span. Returns false as soon as a record does not fit or cannot be walked,
// having called `fn` only for the records before it.
//
// Names are followed through compression pointers, so the spans this yields are
// the real ones even in a message that compresses; a pointer that does not point
// strictly backwards, or a label that runs past the end, stops the walk. Each
// record is walked by jumping over RDLENGTH bytes rather than by parsing RDATA,
// so a record type this does not know costs nothing — which is what lets a caller
// measure a response without understanding a single record in it.
bool WalkRecords(const uint8_t* dns, size_t len, bool (*fn)(const RecordSpan&, void*),
                 void* ctx);

// How ApplyUdpBudget left the message.
enum class UdpBudget {
    Fits,       // it already fit; it was not touched
    Truncated,  // records were dropped from the end and TC was set
    CannotFit,  // not even the header and question fit: answer SERVFAIL
};

// Cut `response` down to `budget` bytes, at a record boundary, setting TC when
// anything was dropped.
//
// Records come off the end first — additional, then authority, then answer —
// because a response is useful in the order it is read and the answer is the part
// the client asked for. Records are never split: a half-written RR is not a
// shorter answer, it is a malformed one, and a client parsing it learns nothing.
// The counts in the header are rewritten to match what survived, so the message
// stays internally consistent.
//
// TC is the whole point of the exercise. It tells the client the answer is
// incomplete and that asking again over TCP will get the rest, which is the one
// thing a client cannot work out for itself — an oversized datagram is simply
// dropped by the network, and a client that never hears back retries over UDP
// rather than escalating.
//
// Empty on CannotFit: the caller owes the client a SERVFAIL rather than a
// question with no answer, and cannot build one from nothing.
UdpBudget ApplyUdpBudget(const uint8_t* query, size_t qlen, std::vector<uint8_t>& response,
                         size_t budget);

// Build a bare response carrying `rcode` and no records: the header and question
// echoed back, everything else empty. This is how a failed forward is reported,
// since the client is owed an answer even when there was nowhere to get one.
std::vector<uint8_t> BuildStatusResponse(const uint8_t* query, size_t qlen, const Query& q,
                                         uint8_t rcode);

// The header and question of `query` with a response header written over it:
// QR=1, RD=1, RA=1, `rcode` in the low nibble, and every section count zeroed.
// Returns an empty vector if the question does not lie inside the message.
//
// Shared with the resolver rather than private to the status response: a
// synthesized answer starts from exactly this, and the two must never disagree
// about which flags a reply carries.
std::vector<uint8_t> BuildResponseHeader(const uint8_t* query, size_t qlen, const Query& q,
                                         uint8_t rcode);

}  // namespace Dns
