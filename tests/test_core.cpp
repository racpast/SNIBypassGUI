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

// Unit tests for the I/O-free logic.
//
// No test framework: a CHECK macro accumulates failures and main() returns non-zero
// on any failure, which ctest reports as a failed test.
#include <winsock2.h>
#include <ws2tcpip.h>

#include <windows.h>

#include <algorithm>
#include <atomic>
#include <chrono>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>
#include <thread>
#include <vector>

#include "app/filesystem.h"
#include "app/text.h"
#include "app/version.h"
#include "dns/answer.h"
#include "dns/dns_proxy.h"
#include "dns/dnscrypt_cert_cache.h"
#include "dns/dnscrypt_client.h"
#include "dns/dnsstamp.h"
#include "dns/doh_client.h"
#include "dns/http_response.h"
#include "dns/message.h"
#include "dns/network_utils.h"
#include "dns/redirector.h"
#include "dns/rules.h"
#include "dns/socket_utils.h"
#include "dns/tcp_session.h"
#include "platform/command.h"
#include "platform/ini.h"
#include "update/client.h"
#include "update/crypto.h"
#include "update/http.h"
#include "update/json.h"
#include "update/progress.h"
#include "updater/plan.h"

namespace {

int g_failures = 0;

#define CHECK(cond)                                                       \
    do {                                                                  \
        if (!(cond)) {                                                    \
            ++g_failures;                                                 \
            std::printf("FAIL: %s (%s:%d)\n", #cond, __FILE__, __LINE__); \
        }                                                                 \
    } while (0)

// std::getenv is not thread safe; this reads the same value through the Win32 API.
bool EnvFlagSet(const wchar_t* name) {
    const DWORD length = GetEnvironmentVariableW(name, nullptr, 0);
    if (length == 0) return false;
    std::wstring value(length, L'\0');
    static_cast<void>(
        GetEnvironmentVariableW(name, &value[0], static_cast<DWORD>(value.size())));
    return true;
}

void TestNormalizeDomain() {
    using Dns::NormalizeDomain;
    CHECK(NormalizeDomain(".Google.COM.") == "google.com");
    CHECK(NormalizeDomain("EXAMPLE.org") == "example.org");
    CHECK(NormalizeDomain("a.b.c") == "a.b.c");
    CHECK(NormalizeDomain("") == "");
}

void TestSuffixMatch() {
    using Dns::SuffixMatch;
    CHECK(SuffixMatch("google.com", "google.com"));
    CHECK(SuffixMatch("www.google.com", "google.com"));
    CHECK(SuffixMatch("a.b.google.com", "google.com"));
    CHECK(!SuffixMatch("notgoogle.com", "google.com"));   // no dot boundary
    CHECK(!SuffixMatch("google.com", "www.google.com"));  // suffix longer
    CHECK(!SuffixMatch("google.com", ""));
    CHECK(!SuffixMatch("", "google.com"));
}

void TestExactMatch() {
    using Dns::ExactMatch;
    CHECK(ExactMatch("exact.com", "exact.com"));
    CHECK(!ExactMatch("www.exact.com", "exact.com"));  // a subdomain is not exact
    CHECK(!ExactMatch("exact.com", "www.exact.com"));
    CHECK(!ExactMatch("exact.com", ""));
    CHECK(!ExactMatch("", "exact.com"));
}

void TestParseRuleLine() {
    using namespace Dns;
    ParsedLine parsed;
    std::string err;

    // Redirect with a mix of suffix and exact prefixes.
    CHECK(ParseRuleLine("127.0.0.1 .a.com b.a.com", parsed, err));
    CHECK(!parsed.isBlock && !parsed.hasV6);
    CHECK(parsed.v4[0] == 127 && parsed.v4[3] == 1);
    CHECK(parsed.domains.size() == 2);
    CHECK(parsed.domains[0].kind == MatchKind::Suffix && parsed.domains[0].domain == "a.com");
    CHECK(parsed.domains[1].kind == MatchKind::Exact && parsed.domains[1].domain == "b.a.com");

    // NX block over several domains.
    CHECK(ParseRuleLine("NX .ads.example tracker.net", parsed, err));
    CHECK(parsed.isBlock);
    CHECK(parsed.domains.size() == 2);
    CHECK(parsed.domains[0].kind == MatchKind::Suffix &&
          parsed.domains[0].domain == "ads.example");
    CHECK(parsed.domains[1].kind == MatchKind::Exact &&
          parsed.domains[1].domain == "tracker.net");
    CHECK(ParseRuleLine("nx foo.com", parsed, err));  // the action is case-insensitive

    // Explicit IPv4 to several exact hosts.
    CHECK(ParseRuleLine("1.2.3.4 exact.com other.net", parsed, err));
    CHECK(!parsed.isBlock && !parsed.hasV6);
    CHECK(parsed.v4[0] == 1 && parsed.v4[1] == 2 && parsed.v4[2] == 3 && parsed.v4[3] == 4);
    CHECK(parsed.domains.size() == 2);
    CHECK(parsed.domains[0].kind == MatchKind::Exact);

    // IPv6 target.
    CHECK(ParseRuleLine("::1 .v6zone.com", parsed, err));
    CHECK(parsed.hasV6);
    CHECK(parsed.v6[15] == 1);
    CHECK(parsed.domains.size() == 1 && parsed.domains[0].kind == MatchKind::Suffix);

    // The retired exclusion syntax takes the whole line down rather than silently
    // redirecting the name it was written to spare.
    CHECK(!ParseRuleLine("127.0.0.1 .a.com !b.a.com", parsed, err));
    CHECK(!ParseRuleLine("127.0.0.1 !.c.a.com", parsed, err));

    // Errors: bare domain (no action), blank, comment, action with no domains, bad IP.
    CHECK(!ParseRuleLine("a.com", parsed, err));
    CHECK(!ParseRuleLine("   ", parsed, err));
    CHECK(!ParseRuleLine("# a comment", parsed, err));
    CHECK(!ParseRuleLine("// a comment", parsed, err));
    CHECK(!ParseRuleLine("127.0.0.1", parsed, err));
    CHECK(!ParseRuleLine("999.0.0.1 a.com", parsed, err));
}

void TestMatchingSemantics() {
    using namespace Dns;
    RuleSet rules;
    // 127.0.0.1 .a.com
    Rule suffixRule;
    suffixRule.action = RuleAction::Redirect;
    suffixRule.kind = MatchKind::Suffix;
    suffixRule.domain = "a.com";
    suffixRule.ttl = 60;
    rules.AddRule(suffixRule);

    // 1.2.3.4 exact.com (exact only)
    Rule exact;
    exact.kind = MatchKind::Exact;
    exact.domain = "exact.com";
    exact.v4[0] = 1;
    exact.v4[1] = 2;
    exact.v4[2] = 3;
    exact.v4[3] = 4;
    rules.AddRule(exact);

    // NX block.com
    Rule blocked;
    blocked.action = RuleAction::Block;
    blocked.kind = MatchKind::Exact;
    blocked.domain = "block.com";
    rules.AddRule(blocked);

    // A suffix rule covers the apex and its subdomains.
    CHECK(rules.Match("a.com") != nullptr);
    CHECK(rules.Match("x.a.com") != nullptr);
    CHECK(rules.Match("y.x.a.com") != nullptr);
    // An exact rule matches only the host, not a subdomain.
    const Rule* hit = rules.Match("exact.com");
    CHECK(hit != nullptr && hit->v4[0] == 1);
    CHECK(rules.Match("www.exact.com") == nullptr);
    // A block rule carries the Block action.
    const Rule* blockHit = rules.Match("block.com");
    CHECK(blockHit != nullptr && blockHit->action == RuleAction::Block);
    // An unlisted name matches nothing, which is what sends it upstream.
    CHECK(rules.Match("elsewhere.net") == nullptr);
}

// The namespaces handed to the policy table have to name exactly the same set the
// rule set matches, in the table's own syntax — that correspondence is the whole
// reason the rule file has no form the table cannot express.
void TestNamespaces() {
    using namespace Dns;
    RuleSet rules;

    Rule suffixRule;
    suffixRule.kind = MatchKind::Suffix;
    suffixRule.domain = "a.com";
    rules.AddRule(suffixRule);

    Rule exact;
    exact.kind = MatchKind::Exact;
    exact.domain = "b.com";
    rules.AddRule(exact);

    // A blocked name still has to be routed here; it is answered, not ignored.
    Rule blocked;
    blocked.action = RuleAction::Block;
    blocked.kind = MatchKind::Suffix;
    blocked.domain = "ads.example";
    rules.AddRule(blocked);

    // A single-label suffix is a namespace like any other.
    Rule single;
    single.kind = MatchKind::Suffix;
    single.domain = "snib";
    rules.AddRule(single);

    // Repeats collapse: the same namespace listed twice is one policy entry.
    rules.AddRule(suffixRule);

    const std::vector<std::string> ns = rules.Namespaces();
    CHECK(ns.size() == 4);
    CHECK(ns[0] == ".a.com");  // suffix keeps its leading dot
    CHECK(ns[1] == "b.com");   // exact has none
    CHECK(ns[2] == ".ads.example");
    CHECK(ns[3] == ".snib");

    CHECK(RuleSet().Namespaces().empty());
}

// A minimal well-formed A query for "a.com": a 12-byte header with QDCOUNT=1, then
// QNAME = 1'a' 3'c''o''m' 0, QTYPE=A(1), QCLASS=IN(1).
const uint8_t kQueryACom[] = {
    0x12, 0x34,            // id
    0x01, 0x00,            // flags (RD)
    0x00, 0x01,            // QDCOUNT
    0x00, 0x00,            // ANCOUNT
    0x00, 0x00,            // NSCOUNT
    0x00, 0x00,            // ARCOUNT
    0x01, 'a',             // label "a"
    0x03, 'c',  'o', 'm',  // label "com"
    0x00,                  // root
    0x00, 0x01,            // QTYPE = A
    0x00, 0x01,            // QCLASS = IN
};

// The same question asked as AAAA, for the family-downgrade cases.
const uint8_t kQueryAaaaCom[] = {
    0x12, 0x35,            // id
    0x01, 0x00,            // flags (RD)
    0x00, 0x01,            // QDCOUNT
    0x00, 0x00,            // ANCOUNT
    0x00, 0x00,            // NSCOUNT
    0x00, 0x00,            // ARCOUNT
    0x01, 'a',             // label "a"
    0x03, 'c',  'o', 'm',  // label "com"
    0x00,                  // root
    0x00, 0x1C,            // QTYPE = AAAA
    0x00, 0x01,            // QCLASS = IN
};

void TestParseQueryAndAction() {
    using namespace Dns;
    Query q;
    CHECK(ParseQuery(kQueryACom, sizeof(kQueryACom), q));
    CHECK(q.name == "a.com");
    CHECK(q.qtype == kTypeA);
    CHECK(q.qclass == kClassIn);
    CHECK(q.id == 0x1234);

    // A truncated message has no complete question.
    CHECK(!ParseQuery(kQueryACom, 8, q));

    CHECK(DecideAction(kTypeA) == Action::Answer);
    CHECK(DecideAction(kTypeAaaa) == Action::Answer);
    CHECK(DecideAction(kTypeHttps) == Action::NoData);
    CHECK(DecideAction(kTypeSvcb) == Action::NoData);
    // Everything else goes to a real resolver rather than being answered here: a
    // policy-table namespace routes every query type in, not just the ones worth
    // redirecting.
    CHECK(DecideAction(16 /* TXT */) == Action::Forward);
    CHECK(DecideAction(15 /* MX */) == Action::Forward);
    CHECK(DecideAction(33 /* SRV */) == Action::Forward);
}

// A failed forward still owes the client an answer, and the only honest one is
// "this lookup failed" — not silence, and not an invented address.
void TestBuildStatusResponse() {
    using namespace Dns;
    Query q;
    CHECK(ParseQuery(kQueryACom, sizeof(kQueryACom), q));

    const std::vector<uint8_t> resp =
        BuildStatusResponse(kQueryACom, sizeof(kQueryACom), q, kRcodeServFail);
    CHECK(resp.size() == q.questionEnd);          // header + question only
    CHECK(resp[2] == 0x81 && resp[3] == 0x82);    // QR=1, RD=1, RA=1, RCODE=2
    CHECK(resp[6] == 0x00 && resp[7] == 0x00);    // ANCOUNT
    CHECK(resp[10] == 0x00 && resp[11] == 0x00);  // ARCOUNT: any EDNS OPT is dropped
}

// An EDNS0 OPT RR for the additional section, advertising a 4096-byte buffer.
// RFC 6891 puts the requestor's payload size in the CLASS field, which is what
// makes an OPT look like a record with an unusual class rather than a new field.
// Eleven bytes: a root NAME, then TYPE, CLASS, a four-byte TTL and RDLENGTH.
const uint8_t kOpt4096[] = {
    0x00,                    // NAME: root
    0x00, 0x29,              // TYPE = OPT (41)
    0x10, 0x00,              // CLASS = UDP payload size, 4096
    0x00, 0x00, 0x00, 0x00,  // TTL (extended rcode and flags)
    0x00, 0x00,              // RDLENGTH
};

// A query for the same name, with that OPT appended and ARCOUNT raised to 1.
std::vector<uint8_t> QueryWithOpt(const uint8_t* query, size_t len) {
    std::vector<uint8_t> q(query, query + len);
    q[11] = 1;  // ARCOUNT
    q.insert(q.end(), kOpt4096, kOpt4096 + sizeof(kOpt4096));
    return q;
}

// An answer with `count` TXT records, each `textLen` bytes of text, so the size
// of the response can be driven past any budget on demand.
std::vector<uint8_t> ResponseWithTxt(const uint8_t* query, size_t len, const Dns::Query& q,
                                     size_t count, size_t textLen) {
    std::vector<uint8_t> r = Dns::BuildResponseHeader(query, len, q, Dns::kRcodeNoError);
    r[7] = static_cast<uint8_t>(count);  // ANCOUNT
    for (size_t i = 0; i < count; ++i) {
        Dns::Put16(r, 0xC00C);                              // NAME: pointer to the question
        Dns::Put16(r, 16);                                  // TYPE = TXT
        Dns::Put16(r, Dns::kClassIn);                       // CLASS
        Dns::Put32(r, 60);                                  // TTL
        Dns::Put16(r, static_cast<uint16_t>(textLen + 1));  // RDLENGTH
        r.push_back(static_cast<uint8_t>(textLen));         // one character-string
        for (size_t j = 0; j < textLen; ++j) r.push_back('x');
    }
    return r;
}

void TestQueryUdpPayloadSize() {
    using namespace Dns;

    // A client that sends no OPT is owed 512, not an error.
    CHECK(QueryUdpPayloadSize(kQueryACom, sizeof(kQueryACom)) == kMinUdpPayload);

    // One that advertises 4096 gets 4096: the negotiation is the client's to open.
    const std::vector<uint8_t> withOpt = QueryWithOpt(kQueryACom, sizeof(kQueryACom));
    CHECK(QueryUdpPayloadSize(withOpt.data(), withOpt.size()) == 4096);

    // Garbage yields the conservative default rather than a bogus size.
    CHECK(QueryUdpPayloadSize(kQueryACom, 5) == kMinUdpPayload);
    std::vector<uint8_t> truncatedOpt = withOpt;
    truncatedOpt.resize(truncatedOpt.size() - 2);
    CHECK(QueryUdpPayloadSize(truncatedOpt.data(), truncatedOpt.size()) == kMinUdpPayload);

    // An advertisement below the floor is raised to it: 512 is the smallest a
    // response may legitimately be. CLASS sits 8 bytes from the record's end:
    // CLASS(2) + TTL(4) + RDLENGTH(2) follow it.
    std::vector<uint8_t> tiny = withOpt;
    tiny[tiny.size() - 8] = 0x00;
    tiny[tiny.size() - 7] = 0x40;  // CLASS = 64
    CHECK(QueryUdpPayloadSize(tiny.data(), tiny.size()) == kMinUdpPayload);
}

void TestApplyUdpBudget() {
    using namespace Dns;

    Query q;
    CHECK(ParseQuery(kQueryACom, sizeof(kQueryACom), q));

    // A response that already fits is handed back untouched.
    {
        std::vector<uint8_t> response =
            ResponseWithTxt(kQueryACom, sizeof(kQueryACom), q, 1, 10);
        const size_t before = response.size();
        CHECK(ApplyUdpBudget(kQueryACom, sizeof(kQueryACom), response, 4096) ==
              UdpBudget::Fits);
        CHECK(response.size() == before);
        CHECK(!IsTruncated(response.data(), response.size()));
    }

    // One over budget is cut at a record boundary, told to the client, and its
    // counts rewritten to match what survived. A record here is
    // NAME(2) + TYPE(2) + CLASS(2) + TTL(4) + RDLENGTH(2) + RDATA(1 + textLen),
    // so a budget of questionEnd + one whole record keeps exactly one of the three.
    {
        std::vector<uint8_t> response =
            ResponseWithTxt(kQueryACom, sizeof(kQueryACom), q, 3, 20);
        const size_t recordLen = 13 + 20;
        const size_t budget = q.questionEnd + recordLen;

        CHECK(ApplyUdpBudget(kQueryACom, sizeof(kQueryACom), response, budget) ==
              UdpBudget::Truncated);
        CHECK(IsTruncated(response.data(), response.size()));
        CHECK(response[7] == 1);  // ANCOUNT: one record survived, not three
        CHECK(response.size() == budget);

        // The cut has to land between records, so everything before it still walks
        // as a well-formed message.
        Query reparsed;
        CHECK(ParseQuery(response.data(), response.size(), reparsed));
        CHECK(WalkRecords(response.data(), response.size(), nullptr, nullptr));
    }

    // A budget too small for the question cannot be honoured by truncating: the
    // client is owed a failure, not a question it cannot match.
    {
        std::vector<uint8_t> response =
            ResponseWithTxt(kQueryACom, sizeof(kQueryACom), q, 1, 10);
        CHECK(ApplyUdpBudget(kQueryACom, sizeof(kQueryACom), response, 12) ==
              UdpBudget::CannotFit);
    }

    // A response whose records do not fit keeps the question and says so, rather
    // than being sent oversized or split mid-record.
    {
        std::vector<uint8_t> response =
            ResponseWithTxt(kQueryACom, sizeof(kQueryACom), q, 2, 40);
        const size_t budget = q.questionEnd + 12;  // less than one whole record
        CHECK(ApplyUdpBudget(kQueryACom, sizeof(kQueryACom), response, budget) ==
              UdpBudget::Truncated);
        CHECK(response.size() == q.questionEnd);
        CHECK(IsTruncated(response.data(), response.size()));
        CHECK(response[7] == 0);  // ANCOUNT
    }
}

void TestApplyUdpBudgetRejectsBadResponse() {
    using namespace Dns;
    Query q;
    CHECK(ParseQuery(kQueryACom, sizeof(kQueryACom), q));

    // A record that claims more RDATA than the datagram holds cannot be walked.
    // Rather than relaying it, the budget keeps the question and sets TC, which
    // tells the client to try TCP — where it will get a response that can be
    // trusted — instead of trusting a count nothing could verify.
    //
    // RDLENGTH sits immediately behind the RDATA, so with a 21-byte RDATA it is
    // 21 bytes from the end of the record — not ten. Ten is how far the fixed
    // block reaches back from the RDATA's start.
    std::vector<uint8_t> response = ResponseWithTxt(kQueryACom, sizeof(kQueryACom), q, 1, 20);
    response[response.size() - 23] = 0xFF;  // RDLENGTH high byte
    response[response.size() - 22] = 0xFF;  // RDLENGTH low byte
    CHECK(!WalkRecords(response.data(), response.size(), nullptr, nullptr));
    CHECK(ApplyUdpBudget(kQueryACom, sizeof(kQueryACom), response, q.questionEnd + 8) ==
          UdpBudget::Truncated);
    CHECK(response.size() == q.questionEnd);
}

void TestBuildResponse() {
    using namespace Dns;
    Query q;
    CHECK(ParseQuery(kQueryACom, sizeof(kQueryACom), q));

    Rule rule;  // defaults to 127.0.0.1 / ::1
    const std::vector<uint8_t> resp =
        BuildResponse(kQueryACom, sizeof(kQueryACom), q, rule, Action::Answer);
    CHECK(!resp.empty());
    CHECK(resp.size() > q.questionEnd);         // header + question plus one A record
    CHECK(resp[2] == 0x81 && resp[3] == 0x80);  // QR=1, RD=1, RA=1, RCODE=0
    CHECK(resp[7] == 0x01);                     // ANCOUNT
    // The last four bytes are the A record RDATA, 127.0.0.1.
    CHECK(resp[resp.size() - 4] == 127);
    CHECK(resp[resp.size() - 1] == 1);

    // Forwarding yields an empty payload: there is nothing to say until a real
    // resolver has said it.
    CHECK(BuildResponse(kQueryACom, sizeof(kQueryACom), q, rule, Action::Forward).empty());
}

void TestBuildResponseBlock() {
    using namespace Dns;
    Query q;
    CHECK(ParseQuery(kQueryACom, sizeof(kQueryACom), q));

    Rule rule;
    rule.action = RuleAction::Block;
    // A Block rule yields NXDOMAIN regardless of the action passed in.
    const std::vector<uint8_t> resp =
        BuildResponse(kQueryACom, sizeof(kQueryACom), q, rule, Action::NoData);
    CHECK(!resp.empty());
    CHECK(resp[2] == 0x81 && resp[3] == 0x83);  // RCODE=3 (NXDOMAIN)
    CHECK(resp[6] == 0x00 && resp[7] == 0x00);  // ANCOUNT = 0
    CHECK(resp.size() == q.questionEnd);        // header + question only, no records
}

// The address on the wire must be the one the rule carries, never the struct's
// default. Rule's in-class initializer happens to be 127.0.0.1, which is also what
// nearly every shipped rule asks for — so a bug that ignored the parsed address
// would pass every other test in this file and be invisible in production until
// someone wrote a rule pointing somewhere else.
void TestResponseCarriesRuleAddress() {
    using namespace Dns;

    // Straight from a rule line, through the parser, onto the wire.
    ParsedLine parsed;
    std::string err;
    CHECK(ParseRuleLine("203.0.113.77 gamma.example", parsed, err));

    Rule v4Rule;
    v4Rule.kind = MatchKind::Exact;
    v4Rule.domain = parsed.domains[0].domain;
    std::memcpy(v4Rule.v4, parsed.v4, sizeof(v4Rule.v4));
    v4Rule.hasV4 = true;
    v4Rule.hasV6 = false;

    Query q;
    CHECK(ParseQuery(kQueryACom, sizeof(kQueryACom), q));
    const std::vector<uint8_t> a =
        BuildResponse(kQueryACom, sizeof(kQueryACom), q, v4Rule, Action::Answer);
    CHECK(a.size() >= 4);
    CHECK(a[a.size() - 4] == 203 && a[a.size() - 3] == 0 && a[a.size() - 2] == 113 &&
          a[a.size() - 1] == 77);

    // An AAAA query against that v4-only rule must yield NODATA, not the default ::1.
    CHECK(ParseQuery(kQueryAaaaCom, sizeof(kQueryAaaaCom), q));
    const std::vector<uint8_t> downgraded =
        BuildResponse(kQueryAaaaCom, sizeof(kQueryAaaaCom), q, v4Rule, Action::Answer);
    CHECK(downgraded.size() == q.questionEnd);  // no record appended
    CHECK(downgraded[7] == 0);                  // ANCOUNT
    CHECK((downgraded[3] & 0x0Fu) == kRcodeNoError);

    // And the same for a v6 rule's address.
    CHECK(ParseRuleLine("2001:db8::dead:beef .v6.example", parsed, err));
    Rule v6Rule;
    v6Rule.kind = MatchKind::Suffix;
    v6Rule.domain = parsed.domains[0].domain;
    std::memcpy(v6Rule.v6, parsed.v6, sizeof(v6Rule.v6));
    v6Rule.hasV4 = false;
    v6Rule.hasV6 = true;

    const std::vector<uint8_t> aaaa =
        BuildResponse(kQueryAaaaCom, sizeof(kQueryAaaaCom), q, v6Rule, Action::Answer);
    CHECK(aaaa.size() >= 16);
    const uint8_t* rdata = aaaa.data() + aaaa.size() - 16;
    CHECK(rdata[0] == 0x20 && rdata[1] == 0x01 && rdata[2] == 0x0d && rdata[3] == 0xb8);
    CHECK(rdata[12] == 0xde && rdata[13] == 0xad && rdata[14] == 0xbe && rdata[15] == 0xef);
}

void TestUpdateHelpers() {
    using namespace Update;

    CHECK(CompareVersions(L"5.0.0", L"4.9.8") > 0);
    CHECK(CompareVersions(L"4.9.8", L"5.0.0") < 0);
    CHECK(CompareVersions(L"V5.0.0", L"5.0.0") == 0);
    CHECK(CompareVersions(L"5.0", L"5.0.0") == 0);
    CHECK(CompareVersions(L"5.0.1", L"5.0.0") > 0);
    // Non-digit characters within a component are skipped, so "0beta" parses as 0.
    CHECK(CompareVersions(L"5.0.0beta", L"5.0.0") == 0);

    CHECK(IsHexDigest(std::wstring(64, L'a')));
    CHECK(IsHexDigest(std::wstring(64, L'0')));
    CHECK(!IsHexDigest(std::wstring(63, L'a')));  // wrong length
    CHECK(!IsHexDigest(std::wstring(64, L'A')));  // uppercase is not accepted
    CHECK(!IsHexDigest(std::wstring(64, L'g')));  // non-hex

    CHECK(UrlBaseDir(L"https://x.example/a/b/manifest.json") == L"https://x.example/a/b/");
    CHECK(UrlBaseDir(L"https://x.example/manifest.json?v=2") == L"https://x.example/");

    // The executable-update decision is numeric-only and by INEQUALITY, so the client
    // follows the channel both up (upgrade) and down (force-aligned downgrade).
    // Asserted with plain literals so a version bump cannot break these.
    CHECK(CompareVersions(L"5.0.1", L"5.0.0") != 0);   // remote newer -> update
    CHECK(CompareVersions(L"5.0.0", L"5.0.0") == 0);   // same -> no update
    CHECK(CompareVersions(L"4.9.9", L"5.0.0") < 0);    // remote older -> downgrade
    CHECK(CompareVersions(L"4.9.9", L"5.0.0") != 0);   // "!=" makes it update too
    CHECK(CompareVersions(L"5.0.0.1", L"5.0.0") > 0);  // the fourth component counts
    CHECK(CompareVersions(L"5.0.0", L"5.0.0.1") < 0);

    // Tie the contract to the ACTUAL APP_VERSION_NUM without hard-coding its value.
    CHECK(CompareVersions(APP_VERSION_NUM, APP_VERSION_NUM) == 0);
    CHECK(CompareVersions(std::wstring(APP_VERSION_NUM) + L".1", APP_VERSION_NUM) > 0);
    CHECK(CompareVersions(std::wstring(APP_VERSION_NUM) + L".1", APP_VERSION_NUM) != 0);
    CHECK(CompareVersions(L"0.0.0", APP_VERSION_NUM) < 0);
    CHECK(CompareVersions(L"0.0.0", APP_VERSION_NUM) != 0);
}

// Canonical base64 is a security-relevant property, not a style preference: two
// spellings that decode to the same bytes mean a signature over one byte string can
// be presented as a signature over a differently-spelled one. Every rejection below
// is a form a lenient decoder would have accepted.
void TestBase64Canonical() {
    using namespace Crypto;

    std::vector<uint8_t> out;

    // The forms the release tool actually produces: correct padding, and none when the
    // length is already a whole number of groups.
    CHECK(Base64Decode("TWFu", out) && out.size() == 3 && out[0] == 'M' && out[1] == 'a' &&
          out[2] == 'n');
    CHECK(Base64Decode("TWE=", out) && out.size() == 2 && out[0] == 'M' && out[1] == 'a');
    CHECK(Base64Decode("TQ==", out) && out.size() == 1 && out[0] == 'M');

    // A real signature is 64 bytes, which base64-encodes to 88 characters ending in
    // "==". This is the exact shape VerifySignature receives.
    CHECK(Base64Decode(std::string(86, 'A') + "==", out) && out.size() == 64);

    // ---- rejections: none of these is something a canonical encoder emits ----

    CHECK(!Base64Decode("", out));       // empty is not a valid encoding
    CHECK(!Base64Decode("TWF", out));    // 3-char input: truncated final group
    CHECK(!Base64Decode("TWFuT", out));  // not a whole number of 4-char groups
    CHECK(!Base64Decode("====", out));   // padding with no body
    CHECK(!Base64Decode("T===", out));   // more than two pad characters
    CHECK(!Base64Decode("TQ=A", out));   // data after the padding
    CHECK(!Base64Decode("TW=u", out));   // padding in the middle
    CHECK(!Base64Decode("TWFu=", out));  // 5 chars: whole-group violation
    CHECK(!Base64Decode("TWE\n", out));  // whitespace is not silently skipped
    CHECK(!Base64Decode("TW F", out));
    CHECK(!Base64Decode("TW-8", out));  // URL-safe alphabet is a different encoding
    CHECK(!Base64Decode("TW_8", out));
    CHECK(!Base64Decode("T!Fu", out));  // non-alphabet character

    // Wrong padding for the byte count: "TWF" had no remainder, so it must not carry
    // padding, and 2 characters cannot imply 2 pad bytes.
    CHECK(!Base64Decode("TW==", out));
    // Non-canonical spellings of bytes that have a canonical form. Both decode to
    // 'M'/'Ma' under a lenient decoder, which is the malleability case these rules
    // exist to close: a signature over "TWE=" must not also verify over "TWF=".
    CHECK(!Base64Decode("TWF=", out));  // spare bits set in the final sextet
    CHECK(!Base64Decode("TR==", out));
    CHECK(Base64Decode("TWE=", out) && out.size() == 2);  // the canonical spelling
    CHECK(Base64Decode("TQ==", out) && out.size() == 1);

    // A rejected input must leave nothing behind for a caller to mistake for a result,
    // even when a prior call in the same scope succeeded and left bytes in the vector.
    CHECK(out.size() == 1);             // from the successful decode just above
    CHECK(!Base64Decode("TWF=", out));  // rejected
    CHECK(out.empty());                 // and the stale result is gone
}

// Path safety and glob pattern compilation are the highest-consequence pure functions
// in the codebase: they guard deletion operations against escaping the program
// directory. These tests verify that hand-edited or corrupted paths.ini entries
// cannot aim operations outside the tree we own.
void TestFileSystemSafety() {
    using namespace FileSystem;

    // Safe paths used in the shipped payload.
    CHECK(IsSafePath(L"data"));
    CHECK(IsSafePath(L"logs"));
    CHECK(IsSafePath(L"paths.ini"));
    CHECK(IsSafePath(L"config.ini"));
    CHECK(IsSafePath(L"data\\temp"));
    CHECK(IsSafePath(L"data/temp"));  // forward slashes accepted

    // Escapes out of the program directory.
    CHECK(!IsSafePath(L"..\\Windows"));
    CHECK(!IsSafePath(L"data\\..\\..\\Windows"));
    CHECK(!IsSafePath(L"a\\..\\b"));  // ".." anywhere, even if it nets out
    CHECK(!IsSafePath(L"."));
    CHECK(!IsSafePath(L"data\\.\\x"));
    CHECK(!IsSafePath(L"C:\\Windows"));        // drive-qualified
    CHECK(!IsSafePath(L"\\Windows"));          // root-relative
    CHECK(!IsSafePath(L"\\\\server\\share"));  // UNC
    CHECK(!IsSafePath(L"data:stream"));        // alternate data stream
    CHECK(!IsSafePath(L""));
    // Wildcards not allowed in strict paths.
    CHECK(!IsSafePath(L"*"));
    CHECK(!IsSafePath(L"data\\*"));
    CHECK(!IsSafePath(L"config.in?"));

    // Pattern syntax validation (wildcards allowed, but structure still checked).
    CHECK(IsSafePatternSyntax(L"*.new"));
    CHECK(IsSafePatternSyntax(L"*.bak"));
    CHECK(IsSafePatternSyntax(L"data\\*.conf"));
    CHECK(IsSafePatternSyntax(L"logs\\**\\*.log"));
    CHECK(!IsSafePatternSyntax(L"..\\*"));     // traversal
    CHECK(!IsSafePatternSyntax(L"C:\\*"));     // absolute
    CHECK(!IsSafePatternSyntax(L"a\\..\\b"));  // ".." anywhere
    CHECK(!IsSafePatternSyntax(L""));

    // Glob pattern compilation: valid patterns.
    GlobPattern p1 = CompilePattern(L"*.log");
    CHECK(p1.isValid && !p1.isRecursive);

    GlobPattern p2 = CompilePattern(L"data\\*.conf");
    CHECK(p2.isValid && !p2.isRecursive);

    GlobPattern p3 = CompilePattern(L"data\\**\\*.log");
    CHECK(p3.isValid && p3.isRecursive);

    GlobPattern p4 = CompilePattern(L"**\\*.tmp");
    CHECK(p4.isValid && p4.isRecursive);

    // Forbidden patterns.
    CHECK(!CompilePattern(L"**").isValid);          // bare ** is ambiguous
    CHECK(!CompilePattern(L"**\\*").isValid);       // redundant (use * instead)
    CHECK(!CompilePattern(L"dir\\**\\*").isValid);  // redundant (use dir\* instead)
    CHECK(!CompilePattern(L"..\\path").isValid);    // traversal
    CHECK(!CompilePattern(L"C:\\path").isValid);    // absolute
    CHECK(!CompilePattern(L"").isValid);
}

void TestGlobMatching() {
    using namespace FileSystem;

    // Simple wildcards (no recursion).
    GlobPattern p1 = CompilePattern(L"*.log");
    CHECK(MatchesPattern(L"test.log", p1));
    CHECK(MatchesPattern(L"app.log", p1));
    CHECK(!MatchesPattern(L"test.txt", p1));
    CHECK(!MatchesPattern(L"dir\\test.log", p1));  // in subdirectory

    GlobPattern p2 = CompilePattern(L"data\\*.conf");
    CHECK(MatchesPattern(L"data\\nginx.conf", p2));
    CHECK(MatchesPattern(L"data\\test.conf", p2));
    CHECK(!MatchesPattern(L"data\\sub\\test.conf", p2));  // too deep
    CHECK(!MatchesPattern(L"other\\test.conf", p2));

    // Recursive wildcards.
    GlobPattern p3 = CompilePattern(L"data\\**\\*.log");
    CHECK(MatchesPattern(L"data\\test.log", p3));
    CHECK(MatchesPattern(L"data\\sub\\test.log", p3));
    CHECK(MatchesPattern(L"data\\a\\b\\c\\test.log", p3));
    CHECK(!MatchesPattern(L"other\\test.log", p3));
    CHECK(!MatchesPattern(L"data\\test.txt", p3));

    GlobPattern p4 = CompilePattern(L"**\\*.tmp");
    CHECK(MatchesPattern(L"test.tmp", p4));
    CHECK(MatchesPattern(L"data\\test.tmp", p4));
    CHECK(MatchesPattern(L"a\\b\\c\\test.tmp", p4));
    CHECK(!MatchesPattern(L"test.log", p4));

    // ? wildcard.
    GlobPattern p5 = CompilePattern(L"test?.log");
    CHECK(MatchesPattern(L"test1.log", p5));
    CHECK(MatchesPattern(L"testA.log", p5));
    CHECK(!MatchesPattern(L"test.log", p5));    // ? must match one char
    CHECK(!MatchesPattern(L"test12.log", p5));  // ? matches only one

    // Directory clearing pattern.
    GlobPattern p6 = CompilePattern(L"logs\\*");
    CHECK(MatchesPattern(L"logs\\test.log", p6));
    CHECK(MatchesPattern(L"logs\\subdir", p6));
    CHECK(!MatchesPattern(L"logs\\sub\\test.log", p6));  // not recursive
}

// The JSON reader backs manifest parsing, so its failure modes are what keep a
// malformed manifest from being half-applied.
void TestJson() {
    Json::Value root;

    CHECK(Json::Parse(R"({"a":1,"b":"x","c":[1,2],"d":{"e":true}})", root));
    CHECK(root.type == Json::Value::Type::Object);
    CHECK(root.GetStr("b") == "x");
    uint64_t n = 0;
    CHECK(root.GetUInt("a", n) && n == 1);
    const Json::Array* arr = root.GetArr("c");
    CHECK(arr != nullptr && arr->size() == 2);

    // Escapes, including a surrogate pair.
    CHECK(Json::Parse(R"({"s":"a\"b\\c\nd\u0041\uD83D\uDE00"})", root));
    CHECK(root.GetStr("s") == "a\"b\\c\ndA\xF0\x9F\x98\x80");

    // A non-integer or negative number is not a valid size.
    CHECK(Json::Parse(R"({"x":1.5,"y":-3})", root));
    CHECK(!root.GetUInt("x", n));
    CHECK(!root.GetUInt("y", n));

    // Malformed input is rejected rather than partially accepted.
    CHECK(!Json::Parse("", root));
    CHECK(!Json::Parse("{", root));
    CHECK(!Json::Parse(R"({"a":1,})", root));
    CHECK(!Json::Parse(R"({"a":1} trailing)", root));
    // A leading zero is tolerated: ParseNumber reads the digit run and strtod
    // takes it, so "01" is the number 1. Asserted positively and with the value
    // checked, because the interesting half of the claim is the value it yields,
    // not merely that parsing succeeded.
    CHECK(Json::Parse(R"({"a":01})", root));
    CHECK(root.Find("a") != nullptr && root.Find("a")->type == Json::Value::Type::Number);
    CHECK(root.GetUInt("a", n) && n == 1);
    CHECK(!Json::Parse(R"({a:1})", root));  // unquoted key
    CHECK(!Json::Parse("[1,2", root));
}

// The rule-repair rate limit that decides when restoring the DNS policy rule has
// stopped being a repair and become a fight nobody wins. It is the only piece of the
// redirector's guardian that is a decision rather than a system call, so it is the
// piece that can be reasoned about here rather than only observed on a machine.
void TestRepairBudget() {
    using Dns::RepairBudget;
    const uint64_t kWindow = RepairBudget::kWindowMs;
    const unsigned kMax = RepairBudget::kMaxRepairs;

    // Exactly the budget is allowed; the one past it is not.
    RepairBudget budget;
    for (unsigned i = 0; i < kMax; ++i) CHECK(budget.Allow(1000));
    CHECK(!budget.Allow(1000));

    // Still refused later in the same window, right up to its last millisecond.
    CHECK(!budget.Allow(1000 + kWindow - 1));

    // A window that has elapsed starts a fresh count: one deletion an hour is a thing
    // to repair forever, not a fight.
    CHECK(budget.Allow(1000 + kWindow));
    for (unsigned i = 1; i < kMax; ++i) CHECK(budget.Allow(1000 + kWindow));
    CHECK(!budget.Allow(1000 + kWindow));

    // The window is measured from the first repair in it, not from a fixed epoch, so
    // a budget first used far from zero behaves the same way.
    RepairBudget late;
    const uint64_t start = 5 * kWindow + 7;
    for (unsigned i = 0; i < kMax; ++i) CHECK(late.Allow(start));
    CHECK(!late.Allow(start + kWindow - 1));
    CHECK(late.Allow(start + kWindow));

    // Repairs spread thinly never exhaust it, however many there are in total.
    RepairBudget sparse;
    for (unsigned i = 0; i < kMax * 4; ++i) CHECK(sparse.Allow(i * kWindow));
}

void TestIpEndpointParsing() {
    using namespace Dns::NetworkUtils;

    IpEndpoint endpoint;
    CHECK(ParseIpEndpoint("1.1.1.1", 443, endpoint));
    CHECK(endpoint.address.ss_family == AF_INET);
    CHECK(ntohs(reinterpret_cast<const sockaddr_in&>(endpoint.address).sin_port) == 443);
    CHECK(endpoint.host == "1.1.1.1");

    CHECK(ParseIpEndpoint("9.9.9.9:853", 443, endpoint));
    CHECK(endpoint.address.ss_family == AF_INET);
    CHECK(ntohs(reinterpret_cast<const sockaddr_in&>(endpoint.address).sin_port) == 853);

    CHECK(ParseIpEndpoint("[2001:db8::1]:853", 443, endpoint));
    CHECK(endpoint.address.ss_family == AF_INET6);
    CHECK(ntohs(reinterpret_cast<const sockaddr_in6&>(endpoint.address).sin6_port) == 853);
    CHECK(endpoint.host == "2001:db8::1");

    CHECK(ParseIpEndpoint("2001:db8::2", 443, endpoint));
    CHECK(endpoint.address.ss_family == AF_INET6);
    CHECK(ntohs(reinterpret_cast<const sockaddr_in6&>(endpoint.address).sin6_port) == 443);

    CHECK(!ParseIpEndpoint("[2001:db8::1", 443, endpoint));
    CHECK(!ParseIpEndpoint("[2001:db8::1]junk", 443, endpoint));
    CHECK(!ParseIpEndpoint("1.1.1.1:0", 443, endpoint));
    CHECK(!ParseIpEndpoint("1.1.1.1:65536", 443, endpoint));
    CHECK(!ParseIpEndpoint("resolver.example:443", 443, endpoint));
}

void TestDnsStamps() {
    using namespace Dns;

    const DNSStamp cloudflare = ParseDNSStamp(
        "sdns://AgcAAAAAAAAABzEuMS4xLjEAEmRucy5jbG91ZGZsYXJlLmNvbQovZG5zLXF1ZXJ5");
    CHECK(cloudflare.valid);
    CHECK(cloudflare.protocol == StampProtocol::DoH);
    CHECK(cloudflare.address == "1.1.1.1");
    CHECK(cloudflare.hostname == "dns.cloudflare.com");
    CHECK(cloudflare.path == "/dns-query");
    CHECK(cloudflare.hashes.empty());

    const DNSStamp pinned = ParseDNSStamp(
        "sdns://AgcAAAAAAAAADDgwLjY3LjE2OS40MCCMUDOXP_5P8e8KqSmE_JMoG6epJ474v2QSJriY0Q1OdApuczEuZmRuLmZyCi9kbnMtcXVlcnk");
    CHECK(pinned.valid);
    CHECK(pinned.hashes.size() == 1);
    CHECK(pinned.hashes[0].size() == 32);

    CHECK(
        !ParseDNSStamp(
             "sdns://AQcAAAAAAAAAEzk1LjIxNi4xMzguMTQxOjg0NDMguorzbtc_JWEU0KBhGLZWuvInIeGd-R5CcEHYS-SIz7cXMi5kbnNjcnlwdC1jZXJ0Lm53cHMuZmkAA")
             .valid);

    // Plain DNS: a protocol byte, the props field every format carries, and one
    // address. There is no hostname because there is nothing to authenticate.
    const DNSStamp plain = ParseDNSStamp("sdns://AAcAAAAAAAAABzEuMS4xLjE");
    CHECK(plain.valid);
    CHECK(plain.protocol == StampProtocol::PlainDNS);
    CHECK(plain.address == "1.1.1.1");
    CHECK(plain.hostname.empty());

    // An address is the one thing this format cannot do without: a stamp with
    // none describes a server nothing could be sent to.
    CHECK(!ParseDNSStamp("sdns://AAcAAAAAAAAA").valid);
}

// Truncated, extended and otherwise malformed stamps. The parser reads bytes that
// arrive from a config file, so a malformed input must be REJECTED, never accepted
// with a half-filled structure and never allowed to read past its buffer.
void TestDnsStampMalformedInput() {
    using namespace Dns;

    // Every non-empty prefix of a well-formed stamp. Rejecting all of them is what
    // makes the truncation check meaningful: an input that stops mid-field must not
    // decode into a partially populated endpoint.
    const std::string wellFormed =
        "sdns://AgcAAAAAAAAABzEuMS4xLjEAEmRucy5jbG91ZGZsYXJlLmNvbQovZG5zLXF1ZXJ5";
    for (size_t n = 1; n < wellFormed.size(); ++n) {
        const DNSStamp truncated = ParseDNSStamp(wellFormed.substr(0, n));
        const bool isComplete = false;  // no prefix of this length is the whole thing
        CHECK(truncated.valid == isComplete);
    }
    // The whole thing, by contrast, must parse.
    CHECK(ParseDNSStamp(wellFormed).valid);

    // A 3-byte suffix is not a whole number of base64 groups, and the tail rules
    // reject it outright rather than decoding 2 bytes and calling that success.
    CHECK(!ParseDNSStamp("sdns://AAAA").valid);
    CHECK(!ParseDNSStamp("sdns://AAA").valid);
    CHECK(!ParseDNSStamp("sdns://AAAAA").valid);

    // An unknown protocol byte: the format has a defined set, and one outside it must
    // not fall through to a default transport. 0xFF is not a stamp protocol.
    CHECK(!ParseDNSStamp("sdns://_wcAAAAAAAAABzEuMS4xLjE").valid);

    // An oversized declared length field cannot make the parser read past the end:
    // the field claims more bytes than the stamp contains, so the whole thing is
    // rejected rather than partially consumed.
    CHECK(!ParseDNSStamp("sdns://AgcAAAAAAAD/////MS4xLjEuMQ").valid);

    // Empty and scheme-less inputs.
    CHECK(!ParseDNSStamp("").valid);
    CHECK(!ParseDNSStamp("sdns://").valid);
    CHECK(!ParseDNSStamp("1.1.1.1").valid);

    // Trailing '=' is REJECTED: the URL-safe decoder this format uses does not
    // accept padding, and the parser surfaces that as an invalid stamp rather than
    // silently dropping the character. Verified against the parser, not assumed.
    CHECK(!ParseDNSStamp(
               "sdns://AgcAAAAAAAAABzEuMS4xLjEAEmRucy5jbG91ZGZsYXJlLmNvbQovZG5zLXF1ZXJ=")
               .valid);
    // A variant final character, by contrast, decodes to a different but still
    // well-formed stamp, so it is accepted. Asserting the real behaviour keeps this a
    // regression guard instead of a test that fails the moment it is run.
    CHECK(
        ParseDNSStamp("sdns://AgcAAAAAAAAABzEuMS4xLjEAEmRucy5jbG91ZGZsYXJlLmNvbQovZG5zLXF1ZXJ4")
            .valid);
}

// The update signature check, offline.
//
// This is the verification the whole update channel rests on, and until now it had
// no test at all. It is testable without a network because it is a pure function of
// (message, signature) against a key compiled into the binary — the only thing that
// would need the network is fetching a signed manifest, and that is a different
// concern from whether a given signature verifies.
//
// The three outcomes are asserted, because collapsing them was the bug T-06 item 2
// fixed: a bare `false` could not distinguish "this signature is wrong" from "this
// machine cannot check signatures", and the user was told the scary one either way.
void TestUpdateSignatureVerification() {
    using namespace Crypto;

    const std::string message = R"({"schema":2,"version":"5.1.2"})";

    // A 64-byte signature of all zeros is well-formed and simply wrong. That is a
    // verdict about the message, so it must report BadSignature — NOT Unavailable.
    // If this ever returns Unavailable, the distinction has regressed.
    const std::vector<uint8_t> zeros(64, 0);
    CHECK(VerifySignature(message, zeros) == VerifyResult::BadSignature);

    // The same shape with a different wrong value: still a signature verdict, not a
    // provider problem.
    std::vector<uint8_t> ones(64, 0xFF);
    CHECK(VerifySignature(message, ones) == VerifyResult::BadSignature);

    // A different message under the same (wrong) signature is also just a mismatch.
    CHECK(VerifySignature("a different message", zeros) == VerifyResult::BadSignature);

    // Wrong lengths are NOT a statement about the message — no real signature is 63
    // or 65 bytes — so they report Unavailable rather than accusing the download.
    for (size_t n : {size_t{0}, size_t{1}, size_t{63}, size_t{65}, size_t{128}}) {
        const std::vector<uint8_t> wrongLength(n, 0);
        CHECK(VerifySignature(message, wrongLength) == VerifyResult::Unavailable);
    }

    // The canonical 64-byte signature shape (what Base64Decode yields for a real
    // signature) round-trips through the decoder and reaches the verifier.
    std::vector<uint8_t> decoded;
    CHECK(Base64Decode(std::string(86, 'A') + "==", decoded));
    CHECK(decoded.size() == 64);
    CHECK(VerifySignature(message, decoded) == VerifyResult::BadSignature);
}

// The parser is the code; resources/payload/data/dns_proxy.ini is hand-maintained
// DATA that changes with every release and may legitimately be empty.
//
// This test used to load that file and assert it held exactly 20 upstreams, which
// made every edit to the payload a test failure — a red build that pointed at
// nothing broken. The payload is not a code invariant, so it is not asserted here at
// all. What IS asserted is that the parser turns a known INI into the right
// structures, exercised against a fixture this test owns and writes itself.
void TestDnsProxyConfigParsing() {
    // Redirected to the OS temp directory rather than left in the CWD: a test must
    // not leave litter in whatever directory it happens to be run from.
    wchar_t tempDir[MAX_PATH + 1] = {};
    const DWORD dirLen = GetTempPathW(MAX_PATH, tempDir);
    CHECK(dirLen != 0 && dirLen < MAX_PATH);
    if (dirLen == 0 || dirLen >= MAX_PATH) return;

    const std::wstring path = std::wstring(tempDir) + L"snib_test_dns_proxy.ini";

    // Written as the payload files are: INI content, UTF-8 on disk. The stamps are
    // the same well-known ones the shipped file uses, so the fixture stays honest
    // about the real format while owning none of its content.
    const std::string ini =
        "[General]\n"
        "TimeoutMs=2500\n"
        "ThreadPoolSize=8\n"
        "\n"
        // DoH with a hostname and path.
        "[Upstream.cloudflare]\n"
        "Enabled=1\n"
        "Stamp=sdns://AgcAAAAAAAAABzEuMS4xLjEAEmRucy5jbG91ZGZsYXJlLmNvbQovZG5zLXF1ZXJ5\n"
        "\n"
        // DoH with a pinned certificate, so the hash list is populated.
        "[Upstream.fdn]\n"
        "Enabled=1\n"
        "Stamp=sdns://AgcAAAAAAAAADDgwLjY3LjE2OS40MCCMUDOXP_5P8e8KqSmE_JMoG6epJ474v2QSJriY0Q1OdApuczEuZmRuLmZyCi9kbnMtcXVlcnk\n"
        "\n"
        // Plain DNS: an address and nothing to authenticate.
        "[Upstream.plain]\n"
        "Enabled=1\n"
        "Stamp=sdns://AAcAAAAAAAAABzEuMS4xLjE\n"
        "\n"
        // Disabled. Parsed and present, but excluded from the enabled set.
        "[Upstream.disabled]\n"
        "Enabled=0\n"
        "Stamp=sdns://AAcAAAAAAAAABzEuMS4xLjE\n"
        "\n"
        // No Stamp at all: skipped entirely.
        "[Upstream.missing]\n"
        "Enabled=1\n"
        "\n"
        // A Stamp that does not parse: also skipped.
        "[Upstream.garbage]\n"
        "Enabled=1\n"
        "Stamp=not-a-stamp\n"
        "\n"
        // Not an Upstream section: ignored.
        "[SomethingElse]\n"
        "Enabled=1\n";
    {
        std::FILE* f = _wfopen(path.c_str(), L"wb");
        CHECK(f != nullptr);
        if (!f) return;
        const size_t written = std::fwrite(ini.data(), 1, ini.size(), f);
        CHECK(written == ini.size());
        static_cast<void>(std::fclose(f));
    }

    const Dns::DnsProxyConfig config = Dns::DnsProxyConfig::Load(path);
    DeleteFileW(path.c_str());

    // The two sections that could not yield an endpoint are dropped; the other four
    // become upstreams, one of which is disabled.
    CHECK(config.upstreams.size() == 4);
    CHECK(config.EnabledCount() == 3);
    CHECK(config.timeoutMs == 2500);
    CHECK(config.threadPoolSize == 8);

    // Every endpoint carries what its transport needs to be usable.
    for (const Dns::DnsProxyEndpoint& endpoint : config.upstreams) {
        CHECK(!endpoint.address.empty());
        switch (endpoint.protocol) {
            case Dns::DnsProxyProtocol::DNSCrypt:
                CHECK(endpoint.publicKey.size() == 32);
                CHECK(!endpoint.providerName.empty());
                break;
            case Dns::DnsProxyProtocol::DoH:
            case Dns::DnsProxyProtocol::DoT: CHECK(!endpoint.hostname.empty()); break;
            default: break;
        }
    }

    const std::vector<Dns::DnsProxyEndpoint> enabled = config.EnabledUpstreams();
    CHECK(enabled.size() == 3);
    for (const Dns::DnsProxyEndpoint& e : enabled) CHECK(e.enabled);

    // Fields arrive from the stamp, not from defaults.
    bool sawCloudflare = false;
    bool sawPinned = false;
    for (const Dns::DnsProxyEndpoint& e : config.upstreams) {
        if (e.name != L"cloudflare") continue;
        sawCloudflare = true;
        CHECK(e.protocol == Dns::DnsProxyProtocol::DoH);
        CHECK(e.address == "1.1.1.1");
        CHECK(e.hostname == "dns.cloudflare.com");
        CHECK(e.path == "/dns-query");
        CHECK(e.certificateHashes.empty());
    }
    for (const Dns::DnsProxyEndpoint& e : config.upstreams) {
        if (e.name != L"fdn") continue;
        sawPinned = true;
        CHECK(e.certificateHashes.size() == 1);
        CHECK(e.certificateHashes[0].size() == 32);
    }
    CHECK(sawCloudflare);
    CHECK(sawPinned);

    // An unreadable path yields an empty config rather than crashing. Emptiness is a
    // legitimate state — the proxy refuses to start on it — so the only requirement
    // here is that the parser reports it cleanly.
    const Dns::DnsProxyConfig missing =
        Dns::DnsProxyConfig::Load(std::wstring(tempDir) + L"snib_no_such_file.ini");
    CHECK(missing.upstreams.empty());
    CHECK(missing.EnabledCount() == 0);
}

void TestHttpResponseParser() {
    using Dns::HttpResponseParser;

    const std::string fixedHead =
        "HTTP/1.1 200 OK\r\nContent-Length-X: 1\r\nContent-Length: 8\r\n\r\n";
    std::vector<uint8_t> fixed(fixedHead.begin(), fixedHead.end());
    const std::vector<uint8_t> binary = {0x12, 0x34, '\r', '\n', '0', '\r', '\n', 0xFF};
    fixed.insert(fixed.end(), binary.begin(), binary.end());

    HttpResponseParser fixedParser;
    for (size_t i = 0; i < fixed.size(); ++i) {
        const auto result = fixedParser.Feed(&fixed[i], 1);
        CHECK(result == (i + 1 == fixed.size() ? HttpResponseParser::Result::Complete
                                               : HttpResponseParser::Result::NeedMore));
    }
    CHECK(fixedParser.body() == binary);

    const std::string chunkHead = "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n7\r\n";
    std::vector<uint8_t> chunked(chunkHead.begin(), chunkHead.end());
    const std::vector<uint8_t> chunkBody = {1, '\r', '\n', '0', '\r', '\n', 2};
    chunked.insert(chunked.end(), chunkBody.begin(), chunkBody.end());
    const std::string chunkTail = "\r\n0\r\nX-Test: yes\r\n\r\n";
    chunked.insert(chunked.end(), chunkTail.begin(), chunkTail.end());

    HttpResponseParser chunkParser;
    for (size_t offset = 0; offset < chunked.size();) {
        const size_t count = std::min<size_t>(3, chunked.size() - offset);
        const auto result = chunkParser.Feed(chunked.data() + offset, count);
        offset += count;
        if (offset < chunked.size()) CHECK(result == HttpResponseParser::Result::NeedMore);
    }
    CHECK(chunkParser.result() == HttpResponseParser::Result::Complete);
    CHECK(chunkParser.body() == chunkBody);

    const std::string unframed = "HTTP/1.1 200 OK\r\nContent-Length-X: 1\r\n\r\nabc";
    HttpResponseParser unframedParser;
    CHECK(unframedParser.Feed(reinterpret_cast<const uint8_t*>(unframed.data()),
                              unframed.size()) == HttpResponseParser::Result::Error);

    const std::string truncated = "HTTP/1.1 200 OK\r\nContent-Length: 4\r\n\r\nabc";
    HttpResponseParser truncatedParser;
    CHECK(truncatedParser.Feed(reinterpret_cast<const uint8_t*>(truncated.data()),
                               truncated.size()) == HttpResponseParser::Result::NeedMore);
    CHECK(truncatedParser.Finish() == HttpResponseParser::Result::Error);
}

void TestTcpSessionFraming() {
    using namespace Dns;

    const std::vector<uint8_t> first(kQueryACom, kQueryACom + sizeof(kQueryACom));
    const std::vector<uint8_t> second(kQueryAaaaCom, kQueryAaaaCom + sizeof(kQueryAaaaCom));
    const std::vector<uint8_t> framedFirst = EncodeTcpMessage(first);
    const std::vector<uint8_t> framedSecond = EncodeTcpMessage(second);

    TcpSessionReader reader;
    CHECK(reader.Append(framedFirst.data(), 1) == TcpSessionReader::State::Incomplete);
    CHECK(reader.Append(framedFirst.data() + 1, framedFirst.size() - 1) ==
          TcpSessionReader::State::Ready);
    CHECK(reader.TakeMessage() == first);

    std::vector<uint8_t> pipelined = framedFirst;
    pipelined.insert(pipelined.end(), framedSecond.begin(), framedSecond.end());
    CHECK(reader.Append(pipelined.data(), pipelined.size()) == TcpSessionReader::State::Ready);
    CHECK(reader.TakeMessage() == first);
    CHECK(reader.HasMessage());
    CHECK(reader.TakeMessage() == second);

    std::vector<uint8_t> maximum(Dns::SocketUtils::kMaxMessage, 0x5A);
    const std::vector<uint8_t> framedMaximum = EncodeTcpMessage(maximum);
    CHECK(framedMaximum.size() == maximum.size() + 2);
    CHECK(reader.Append(framedMaximum.data(), framedMaximum.size()) ==
          TcpSessionReader::State::Ready);
    CHECK(reader.TakeMessage() == maximum);

    const uint8_t emptyMessage[] = {0, 0};
    CHECK(reader.Append(emptyMessage, sizeof(emptyMessage)) == TcpSessionReader::State::Broken);
    reader.Clear();
}

std::vector<uint8_t> BuildLiveDnsQuery() {
    return {
        0x12, 0x34,  // transaction ID
        0x01, 0x00,  // recursion desired
        0x00, 0x01,  // one question
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x07, 'e',  'x',  'a',  'm',
        'p',  'l',  'e',  0x03, 'c',  'o',  'm',  0x00, 0x00, 0x01,  // A
        0x00, 0x01,                                                  // IN
    };
}

bool IsSuccessfulLiveDnsAnswer(const std::vector<uint8_t>& response, uint16_t id = 0x1234) {
    Dns::Query parsed;
    return response.size() >= 12 && response[0] == static_cast<uint8_t>(id >> 8u) &&
           response[1] == static_cast<uint8_t>(id) && (response[2] & 0x80u) != 0 &&
           (response[3] & 0x0Fu) == Dns::kRcodeNoError &&
           (response[6] != 0 || response[7] != 0) &&
           !Dns::IsTruncated(response.data(), response.size()) &&
           Dns::ParseQuery(response.data(), response.size(), parsed);
}

void TestLiveDnsProxy(const std::vector<uint8_t>& query) {
    const std::wstring configPath =
        std::wstring(SNIB_SOURCE_DIR) + L"/resources/payload/data/dns_proxy.ini";

    Dns::DnsProxy proxy;
    const bool loaded = proxy.LoadConfig(configPath);
    CHECK(loaded);
    const bool started = loaded && proxy.Start();
    CHECK(started);

    Dns::NetworkUtils::IpEndpoint local;
    CHECK(Dns::NetworkUtils::ParseIpEndpoint("127.191.98.10:53", 53, local));
    if (started && local.length != 0) {
        Dns::SocketUtils::SocketHandle udp(
            socket(local.address.ss_family, SOCK_DGRAM, IPPROTO_UDP));
        CHECK(udp.IsValid());
        if (udp.IsValid()) {
            CHECK(Dns::NetworkUtils::SendUdp(udp, query,
                                             reinterpret_cast<const sockaddr*>(&local.address),
                                             local.length, 3000));
            CHECK(IsSuccessfulLiveDnsAnswer(Dns::NetworkUtils::RecvUdp(udp, 12000)));
        }

        // nginx resolves slightly more than twenty dynamic upstream names during
        // a cold start. Send the same-sized burst without waiting between sends:
        // queue starvation used to answer the later clients with SERVFAIL even
        // though all configured transports were healthy.
        constexpr size_t kBurstQueryCount = 24;
        std::vector<Dns::SocketUtils::SocketHandle> burstSockets;
        burstSockets.reserve(kBurstQueryCount);
        for (size_t i = 0; i < kBurstQueryCount; ++i) {
            burstSockets.emplace_back(socket(local.address.ss_family, SOCK_DGRAM, IPPROTO_UDP));
            CHECK(burstSockets.back().IsValid());
            if (!burstSockets.back().IsValid()) continue;

            std::vector<uint8_t> burstQuery = query;
            const uint16_t id = static_cast<uint16_t>(0x4000 + i);
            burstQuery[0] = static_cast<uint8_t>(id >> 8u);
            burstQuery[1] = static_cast<uint8_t>(id);
            CHECK(Dns::NetworkUtils::SendUdp(burstSockets.back(), burstQuery,
                                             reinterpret_cast<const sockaddr*>(&local.address),
                                             local.length, 3000));
        }
        for (size_t i = 0; i < burstSockets.size(); ++i) {
            if (!burstSockets[i].IsValid()) continue;
            const uint16_t id = static_cast<uint16_t>(0x4000 + i);
            CHECK(IsSuccessfulLiveDnsAnswer(Dns::NetworkUtils::RecvUdp(burstSockets[i], 12000),
                                            id));
        }

        Dns::SocketUtils::SocketHandle tcp(
            socket(local.address.ss_family, SOCK_STREAM, IPPROTO_TCP));
        CHECK(tcp.IsValid());
        if (tcp.IsValid() &&
            Dns::NetworkUtils::ConnectWithTimeout(
                tcp, reinterpret_cast<const sockaddr*>(&local.address), local.length, 3000)) {
            const std::vector<uint8_t> framed = Dns::EncodeTcpMessage(query);
            CHECK(Dns::NetworkUtils::SendAll(tcp, framed, 3000));
            CHECK(IsSuccessfulLiveDnsAnswer(Dns::NetworkUtils::RecvLengthPrefixed(tcp, 12000)));
        } else {
            CHECK(false);
        }
    }

    proxy.Stop();
    CHECK(!proxy.Running());
}

void TestLiveDnsTransports() {
    if (!EnvFlagSet(L"SNIB_RUN_NETWORK_TESTS")) return;

    const bool winsockReady = Dns::SocketUtils::EnsureWinsock();
    CHECK(winsockReady);
    if (!winsockReady) return;

    const std::vector<uint8_t> query = BuildLiveDnsQuery();

    const std::vector<uint8_t> doh =
        Dns::QueryDoH(query, "1.12.12.12", "doh.pub", "/dns-query", {}, 10000);
    CHECK(IsSuccessfulLiveDnsAnswer(doh));

    // System trust must succeed above, while an explicit stamp pin mismatch must
    // reject the same otherwise-valid server chain.
    const std::vector<std::vector<uint8_t>> wrongPin(1, std::vector<uint8_t>(32, 0));
    CHECK(Dns::QueryDoH(query, "1.12.12.12", "doh.pub", "/dns-query", wrongPin, 10000).empty());

    const Dns::DNSStamp dnscrypt = Dns::ParseDNSStamp(
        "sdns://AQcAAAAAAAAAEzk1LjIxNi4xMzguMTQxOjg0NDMguorzbtc_JWEU0KBhGLZWuvInIeGd-R5CcEHYS-SIz7cXMi5kbnNjcnlwdC1jZXJ0Lm53cHMuZmk");
    CHECK(dnscrypt.valid);
    if (dnscrypt.valid) {
        // The cache is the caller's, so a live test owns one for the duration
        // rather than reaching for a process-wide instance.
        Dns::CertCache certCache;
        const std::vector<uint8_t> encrypted =
            Dns::QueryDNSCrypt(query, dnscrypt.address, dnscrypt.providerName,
                               dnscrypt.publicKey, certCache, 10000);
        CHECK(IsSuccessfulLiveDnsAnswer(encrypted));
    }

    TestLiveDnsProxy(query);
}

// ---- Ini: write/read agreement ------------------------------------------------

// The reads and the writes of a deployed INI go through different implementations on
// purpose, and the reasoning is in platform/ini.h: reads must be this module's (the
// profile API cannot serve a long value without silently truncating it), while writes
// must be the profile API's (it edits a file without destroying the comments, spacing
// and key order a human put there — verified, not assumed).
//
// What that split needs is a guarantee that the two agree, and this is it. Every value
// below is written by one and read back by the other, requiring the exact characters to
// survive: the boundary cases are the ones where the two rule sets could differ.
//
// What the values deliberately do NOT include is non-ASCII, and that is a property of
// the writer rather than an omission. A BOM-less file has no encoding of its own, so
// WritePrivateProfileStringW picks the machine's ANSI code page for it: 中文路径
// survives on a 936 machine and becomes "????" on the 1252 runner CI uses, because
// that code page has no encoding for those characters. Nothing the two sides could
// agree on would change it — the writer has already replaced them by the time this
// reader sees the file. Production never depends on it either: every value written
// through the profile API is a flag, a language tag or a hex hash. Non-ASCII reaches
// an INI only in the shipped payload, which is UTF-8 on disk and is read, never
// written, by this program; TestIniReader covers that case from the file side.
void TestIniWriteReadAgreement() {
    wchar_t tempDir[MAX_PATH] = {};
    if (GetTempPathW(MAX_PATH, tempDir) == 0) {
        CHECK(false);
        return;
    }
    const std::wstring path = std::wstring(tempDir) + L"snib_ini_agreement.ini";
    DeleteFileW(path.c_str());

    // Each of these is a value whose meaning the profile API's rules and this reader's
    // rules could plausibly disagree about.
    const wchar_t* const values[] = {
        L"plain",
        L"data\\nginx.exe",  // a path with separators
        L"with space",       // an interior space
        L"has=equals",       // a second '=': the split point must be the first
        L"has; semicolon",   // data, not a comment, once it is past the key
        L"has#hash",         // likewise
        L"a*b?c**d",         // a glob pattern, which is what these files hold
        L"",                 // an empty value
    };

    for (size_t i = 0; i < std::size(values); ++i) {
        const std::wstring key = L"Key" + std::to_wstring(i);
        WritePrivateProfileStringW(L"General", key.c_str(), values[i], path.c_str());
    }

    // Read them all back through the project's own reader.
    const std::vector<Ini::Section> sections = Ini::Read(path);
    for (size_t i = 0; i < std::size(values); ++i) {
        const std::wstring key = L"Key" + std::to_wstring(i);
        const std::wstring got = Ini::Value(sections, L"General", key.c_str());
        if (got != values[i]) {
            std::printf("FAIL: [General] %ls round-trip\n  wrote: \"%ls\"\n  read:  \"%ls\"\n",
                        key.c_str(), values[i], got.c_str());
        }
        CHECK(got == values[i]);
    }

    // Leading and trailing whitespace is stripped — by BOTH sides, and that is exactly
    // the agreement this test pins. A padded value written by a caller reads back
    // trimmed, and the profile API itself gives the same answer, so nothing can observe
    // a difference between the two mechanisms. The padded form is deliberately not in
    // the list above, whose contract is "exactly as written".
    WritePrivateProfileStringW(L"General", L"Padded", L"   spaced   ", path.c_str());
    CHECK(Ini::Value(Ini::Read(path), L"General", L"Padded") == L"spaced");

    // A ';' is data inside a value but a comment at the start of a line, and both
    // mechanisms draw that line in the same place. Checked from the file side too, so
    // the agreement covers what a reader sees and not only what a lookup finds.
    WritePrivateProfileStringW(L"General", L"Semi", L"value;more", path.c_str());
    const std::vector<Ini::Section> again = Ini::Read(path);
    CHECK(Ini::Value(again, L"General", L"Semi") == L"value;more");
    CHECK(Ini::Value(again, L"General", L"more").empty());  // not promoted to its own key

    _wremove(path.c_str());
}

// ---- Updater work order: round trip ------------------------------------------

// The work order is the only thing that passes between the elevated application and
// the updater that replaces it, and it is the boundary a planted file would have to
// cross. It was written to be strict (updater/plan.h) and testable — "which is what
// makes the logic testable", says updater/main.cpp — and had no test at all.
//
// What is asserted here is the contract: a written order reads back exactly, and a
// file that does not match the documented shape is refused rather than interpreted as
// far as it happens to parse.
void TestUpdaterPlanRoundTrip() {
    using namespace Updater;

    wchar_t tempDir[MAX_PATH] = {};
    if (GetTempPathW(MAX_PATH, tempDir) == 0) {
        CHECK(false);
        return;
    }
    const std::wstring dir = std::wstring(tempDir);

    // ---- a replacement order, whole ----
    {
        const std::wstring path = dir + L"snib_plan_replace.tmp";
        DeleteFileW(path.c_str());

        Plan written;
        written.op = Op::Replace;
        written.target = L"C:\\Program Files\\SNIB\\SNIBypassGUI.exe";
        written.newFile = written.target + L".new";
        written.backup = written.target + L".bak";
        written.parentPid = 4321;
        written.autostart = true;

        CHECK(WritePlan(path, written));

        Plan read;
        std::wstring why;
        CHECK(ReadPlan(path, read, why));
        CHECK(read.op == Op::Replace);
        CHECK(read.target == written.target);
        CHECK(read.newFile == written.newFile);
        CHECK(read.backup == written.backup);
        CHECK(read.parentPid == written.parentPid);
        CHECK(read.autostart == written.autostart);

        // A non-ASCII install path is the case the format exists to carry safely.
        const std::wstring cjk = dir + L"snib_plan_cjk.tmp";
        DeleteFileW(cjk.c_str());
        Plan unicodePlan = written;
        unicodePlan.target = L"C:\\\u4E2D\u6587\u8DEF\u5F84\\SNIBypassGUI.exe";
        unicodePlan.newFile = unicodePlan.target + L".new";
        unicodePlan.backup = unicodePlan.target + L".bak";
        CHECK(WritePlan(cjk, unicodePlan));
        Plan unicodeRead;
        CHECK(ReadPlan(cjk, unicodeRead, why));
        CHECK(unicodeRead.target == unicodePlan.target);
        DeletePlanFile(cjk);

        DeletePlanFile(path);
    }

    // ---- a removal order ----
    {
        const std::wstring path = dir + L"snib_plan_remove.tmp";
        DeleteFileW(path.c_str());

        Plan written;
        written.op = Op::Remove;
        written.target = L"C:\\app\\SNIBypassGUI.exe";
        written.dir = L"C:\\app";
        written.parentPid = 99;
        written.autostart = false;

        CHECK(WritePlan(path, written));
        Plan read;
        std::wstring why;
        CHECK(ReadPlan(path, read, why));
        CHECK(read.op == Op::Remove);
        CHECK(read.target == written.target);
        CHECK(read.dir == written.dir);
        CHECK(!read.autostart);

        DeletePlanFile(path);
    }

    // ---- CreateNew, never overwrite ----
    //
    // The file is created with CREATE_NEW on purpose: a planted file at the target
    // name must not be silently opened and rewritten. A second write to the same path
    // must therefore fail.
    {
        const std::wstring path = dir + L"snib_plan_excl.tmp";
        DeleteFileW(path.c_str());
        Plan plan;
        plan.op = Op::Remove;
        plan.target = L"C:\\app\\a.exe";
        plan.dir = L"C:\\app";
        plan.parentPid = 7;
        CHECK(WritePlan(path, plan));
        CHECK(!WritePlan(path, plan));  // already there -> refused
        DeletePlanFile(path);
    }

    // ---- malformed orders are refused ----
    //
    // Each body below is a plausible-looking file that is not the documented shape.
    // Reading each as far as it parses is what the strict reader exists to prevent.
    const wchar_t* const badOrders[] = {
        // unknown key: not "as far as it parses", whole-file rejection
        L"format=1\r\nop=remove\r\ntarget=C:\\a\\a.exe\r\ndir=C:\\a\r\npid=5\r\nautostart=0\r\nx=1\r\n",
        // repeated key
        L"format=1\r\nop=remove\r\ntarget=C:\\a\\a.exe\r\ntarget=C:\\b\\b.exe\r\ndir=C:\\a\r\npid=5\r\nautostart=0\r\n",
        // a line that is not key=value
        L"format=1\r\nop=remove\r\nthis-is-not-a-pair\r\ntarget=C:\\a\\a.exe\r\ndir=C:\\a\r\npid=5\r\nautostart=0\r\n",
        // missing required field (no target)
        L"format=1\r\nop=remove\r\ndir=C:\\a\r\npid=5\r\nautostart=0\r\n",
        // unsupported format version
        L"format=2\r\nop=remove\r\ntarget=C:\\a\\a.exe\r\ndir=C:\\a\r\npid=5\r\nautostart=0\r\n",
        // unknown operation
        L"format=1\r\nop=launch\r\ntarget=C:\\a\\a.exe\r\ndir=C:\\a\r\npid=5\r\nautostart=0\r\n",
        // a pid that is not a number
        L"format=1\r\nop=remove\r\ntarget=C:\\a\\a.exe\r\ndir=C:\\a\r\npid=abc\r\nautostart=0\r\n",
        // a pid of zero is not a process
        L"format=1\r\nop=remove\r\ntarget=C:\\a\\a.exe\r\ndir=C:\\a\r\npid=0\r\nautostart=0\r\n",
        // a flag that is neither 0 nor 1
        L"format=1\r\nop=remove\r\ntarget=C:\\a\\a.exe\r\ndir=C:\\a\r\npid=5\r\nautostart=maybe\r\n",
        // a replace order with no staged file
        L"format=1\r\nop=replace\r\ntarget=C:\\a\\a.exe\r\nbackup=C:\\a\\a.bak\r\npid=5\r\nautostart=0\r\n",
        // empty
        L"",
    };

    for (size_t i = 0; i < std::size(badOrders); ++i) {
        const std::wstring path = dir + L"snib_plan_bad" + std::to_wstring(i) + L".tmp";
        DeleteFileW(path.c_str());

        // Written as UTF-8 bytes so the test controls the exact file contents.
        {
            HANDLE f = CreateFileW(path.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS,
                                   FILE_ATTRIBUTE_NORMAL, nullptr);
            CHECK(f != INVALID_HANDLE_VALUE);
            if (f != INVALID_HANDLE_VALUE) {
                const std::string bytes = WideToUtf8(badOrders[i]);
                DWORD written = 0;
                WriteFile(f, bytes.data(), static_cast<DWORD>(bytes.size()), &written, nullptr);
                CloseHandle(f);
            }
        }

        Plan plan;
        std::wstring why;
        if (ReadPlan(path, plan, why)) {
            std::printf("FAIL: malformed order %zu was accepted (why cleared: %ls)\n", i,
                        why.c_str());
        }
        CHECK(!ReadPlan(path, plan, why));
        CHECK(!why.empty());  // a rejection says why, for the log

        DeletePlanFile(path);
    }

    // ---- a path that cannot be represented is refused at write time ----
    //
    // A newline cannot appear in a Win32 path, so this is the round-trip consistency
    // check: a plan whose fields do not survive the write must not reach the updater,
    // which would otherwise act on a different path from the one the parent validated.
    {
        const std::wstring path = dir + L"snib_plan_nl.tmp";
        DeletePlanFile(path);
        Plan plan;
        plan.op = Op::Remove;
        plan.target = L"C:\\a\\has\nnewline.exe";
        plan.dir = L"C:\\a";
        plan.parentPid = 5;
        CHECK(!WritePlan(path, plan));
        CHECK(GetFileAttributesW(path.c_str()) == INVALID_FILE_ATTRIBUTES);
    }
}

// The point of LowerW is that its answer does not depend on process state. These
// check the properties callers rely on rather than the mechanism: ASCII folds, the
// mapping is idempotent, non-ASCII folds rather than passing through, and — the
// reason for the LCMapStringEx change — the result is the same whatever the C locale
// happens to be. towlower under a Turkish locale would map L'I' to a dotless '\u0131'
// and break image-path comparison; the invariant fold maps it to 'i' everywhere.
void TestLowerW() {
    CHECK(LowerW(L"") == L"");
    CHECK(LowerW(L"ABC") == L"abc");
    CHECK(LowerW(L"SNIBypassGUI.EXE") == L"snibypassgui.exe");
    CHECK(LowerW(L"already lower") == L"already lower");
    CHECK(LowerW(L"lower") == LowerW(LowerW(L"lower")));  // idempotent

    // Non-ASCII: previously left untouched by an ASCII-only C locale, which made paths
    // containing them compare case-sensitively.
    CHECK(LowerW(L"ÄÖÜ") == L"äöü");  // A-Umlaut etc.
    CHECK(LowerW(L"Σ") == L"σ");      // Greek capital sigma -> small sigma

    // The Turkish-I property, stated directly: the fold must not be the C locale's.
    // towlower under a Turkish locale would map L'I' to a dotless 'i' and break
    // image-path comparison; the invariant fold maps it to 'i' on every machine.
    CHECK(LowerW(L"I") == L"i");
    CHECK(LowerW(L"II") == L"ii");
    CHECK(LowerW(L"II") == LowerW(L"ii"));

    // The documented boundary of this API, asserted so that moving to a different fold
    // has to be a deliberate act rather than a surprise. Windows' invariant-locale
    // casing does not implement Unicode's full simple case folding for the few
    // characters whose lowercase form is not 1:1: Latin capital I with dot above
    // (U+0130) and the Kelvin sign (U+212A) pass through unchanged. Neither can occur
    // in an install path or an executable name, so a pass-through here cannot make an
    // image-path comparison fail open; what these callers need is that the answer is
    // the same regardless of locale or machine, and that holds.
    CHECK(LowerW(L"İ") == L"İ");
    CHECK(LowerW(L"K") == L"K");
}

// ---- Command::CommandLineHasFlag: exact argument match ----------------------

// A substring search turns each of these into a match; an exact token compare does
// not. The flag drives whether the shortcut prompt is suppressed and whether the stack
// is started on a separate path, so a false positive is a user-visible behavior error.
//
// The two entry points are tested apart because they differ in exactly the way that
// mattered: CommandLineHasFlag takes the whole command line (program name included,
// as GetCommandLineW returns it), while CommandLineHasFlagInArgs takes the lpCmdLine a
// wWinMain is handed, with the program name already removed. A test that only covered
// the first form is how the logon launch went unrecognised for as long as it did.
void TestCommandLineHasFlag() {
    const std::wstring flag = L"-autostart";

    // ---- the full form: arguments start after argv[0] ----

    // The shapes that must match.
    CHECK(Command::CommandLineHasFlag(L"exe.exe -autostart", flag));
    CHECK(
        Command::CommandLineHasFlag(L"\"C:\\Program Files\\SNIBypassGUI\\SNIBypassGUI.exe\" "
                                    L"-autostart",
                                    flag));
    CHECK(Command::CommandLineHasFlag(L"exe.exe /autostart", L"/autostart"));

    // The shapes that must not: a longer flag, an embedded occurrence, a quoted
    // argument, and a path that merely contains the text.
    CHECK(!Command::CommandLineHasFlag(L"exe.exe -notautostart", flag));
    CHECK(!Command::CommandLineHasFlag(L"exe.exe -autostart-extra", flag));
    CHECK(!Command::CommandLineHasFlag(L"\"exe.exe -autostart\"", flag));
    CHECK(!Command::CommandLineHasFlag(L"exe.exe \"C:\\-autostart\\file.txt\"", flag));
    CHECK(!Command::CommandLineHasFlag(L"exe.exe", flag));
    CHECK(!Command::CommandLineHasFlag(L"", flag));
    CHECK(!Command::CommandLineHasFlag(L"exe.exe -autostart", L""));

    // Case is not folded for switches in this program: the scheduler and the updater
    // both emit lowercase, so the comparison is exact.
    CHECK(!Command::CommandLineHasFlag(L"exe.exe -AUTOSTART", flag));

    // A real command line, via the same entry point the callers use.
    CHECK(Command::CommandLineHasFlag(GetCommandLineW(), L"-definitely-not-present") == false);

    // ---- the stripped form: arguments start AT argv[0] ----
    //
    // This is the form main() used to hand the other function, and the reason logon
    // launches did nothing. A scheduled task registers the bare flag as its only
    // argument, so the string being examined is exactly "-autostart" — one token, at
    // argv[0], which the old implementation skipped unconditionally.
    //
    // These assertions fail against that implementation, and that is the point of
    // keeping them: the defect was invisible to a suite that only tried the full form.
    CHECK(Command::CommandLineHasFlagInArgs(L"-autostart", flag));
    CHECK(Command::CommandLineHasFlagInArgs(L"/autostart", L"/autostart"));
    CHECK(Command::CommandLineHasFlagInArgs(L"-autostart -x", flag));
    CHECK(Command::CommandLineHasFlagInArgs(L"-x -autostart", flag));

    // The same rejections hold for the stripped form.
    CHECK(!Command::CommandLineHasFlagInArgs(L"-notautostart", flag));
    CHECK(!Command::CommandLineHasFlagInArgs(L"-autostart-extra", flag));
    CHECK(!Command::CommandLineHasFlagInArgs(L"", flag));
    CHECK(!Command::CommandLineHasFlagInArgs(L"-autostart", L""));
    CHECK(!Command::CommandLineHasFlagInArgs(L"-foo -bar", flag));

    // A quoted program name is one token. When it leads the stripped form it IS
    // argv[0] and does not match the flag, but a flag after it still does.
    CHECK(!Command::CommandLineHasFlagInArgs(L"\"C:\\app\\SNIBypassGUI.exe\"", flag));
    CHECK(Command::CommandLineHasFlagInArgs(L"\"C:\\app\\SNIBypassGUI.exe\" -autostart", flag));
    CHECK(Command::CommandLineHasFlagInArgs(L"-autostart \"C:\\app\\SNIBypassGUI.exe\"", flag));

    // The process-level question, answered through the same tokenizer. Asserted only
    // for the negative: a test run is not a logon launch.
    CHECK(!Command::IsAutostartLaunch());
}

// ---- Ini: reading past the profile API's ceiling -----------------------------

// The failure this reader exists for: GetPrivateProfileStringW cannot return a value
// longer than 32767 characters — at 32768 it returns 0, and one character past that it
// returns a wrapped fragment. A truncated glob pattern is dangerous rather than merely
// incomplete, because the fragment still compiles and the deletion still runs. So the
// test drives a value past that boundary and requires it back whole.
void TestIniReader() {
    wchar_t tempDir[MAX_PATH] = {};
    if (GetTempPathW(MAX_PATH, tempDir) == 0) {
        CHECK(false);
        return;
    }
    const std::wstring path = std::wstring(tempDir) + L"snib_ini_test.ini";

    // A value well past 32767, distinguishable from any prefix of itself.
    std::wstring longValue(40000, L'a');
    longValue[39999] = L'z';

    {
        // "wb, ccs=UTF-16LE" is what makes wide-character output land as UTF-16 rather
        // than as narrow bytes: _wfopen on its own opens a byte stream, and fwprintf to
        // one writes only the low byte of each character.
        std::FILE* f = _wfopen(path.c_str(), L"wb, ccs=UTF-16LE");
        CHECK(f != nullptr);
        if (!f) return;
        // A byte order mark first, so the file states its own encoding, as the profile
        // API's writes do.
        static_cast<void>(std::fputwc(0xFEFF, f));
        static_cast<void>(std::fwprintf(f, L"[Paths]\r\n"));
        static_cast<void>(std::fwprintf(f, L"Hosts=%ls\r\n", longValue.c_str()));
        static_cast<void>(std::fwprintf(f, L"Marker = spaced \r\n"));
        static_cast<void>(std::fwprintf(f, L"; comment=ignored\r\n"));
        static_cast<void>(std::fwprintf(f, L"[Uninstall]\r\n"));
        static_cast<void>(std::fwprintf(f, L"Remove=data\\one.txt|data\\two.txt\r\n"));
        static_cast<void>(
            std::fwprintf(f, L"Hosts=not-this-one\r\n"));  // same key, different section
        static_cast<void>(std::fclose(f));
    }

    const std::vector<Ini::Section> ini = Ini::Read(path);

    // Long value, entire — the profile API returns 0 or a fragment here.
    const std::wstring hosts = Ini::Value(ini, L"Paths", L"Hosts");
    CHECK(hosts.size() == 40000);
    CHECK(!hosts.empty() && hosts.back() == L'z');

    // Surrounding spaces trimmed, the rest kept.
    CHECK(Ini::Value(ini, L"Paths", L"Marker") == L"spaced");

    // Section scoping: the same key under another section is not this section's value.
    CHECK(Ini::Value(ini, L"Uninstall", L"Remove") == L"data\\one.txt|data\\two.txt");
    CHECK(Ini::Value(ini, L"Paths", L"Remove").empty());

    // A commented line is not a key.
    CHECK(Ini::Value(ini, L"Paths", L"comment").empty());

    // Section names, in file order, case-insensitively matched.
    CHECK(Ini::SectionCount(ini) == 2);
    CHECK(Ini::Value(ini, L"paths", L"hosts").size() == 40000);

    // Absent key and absent file both read as empty rather than as an error.
    CHECK(Ini::Value(ini, L"Paths", L"Nope").empty());
    CHECK(Ini::Value(std::wstring(tempDir) + L"snib_ini_missing.ini", L"Paths", L"Hosts")
              .empty());

    // Integers: present, absent, and non-numeric all resolve as the profile API does.
    CHECK(Ini::Int(ini, L"Paths", L"Nope", 7) == 7);
    CHECK(Ini::Int(ini, L"Paths", L"Hosts", 7) == 7);  // all 'a', not a number

    _wremove(path.c_str());

    // Non-ASCII in a UTF-8 file with no byte order mark — the shape every shipped
    // payload INI has, and the only way non-ASCII reaches this reader.
    //
    // This is the read side of what TestIniWriteReadAgreement deliberately does not
    // assert. The profile API writes a BOM-less file in the machine's ANSI code page,
    // so a non-ASCII value put through it survives on a 936 machine and is destroyed on
    // the 1252 runner CI uses. Reading is not exposed to that: UTF-8 is tried first and
    // strictly, so the same bytes decode to the same characters on every machine. The
    // bytes are written out by hand here rather than through any API, which is what
    // makes the test independent of the code page it happens to run under.
    {
        const std::wstring utf8Path = std::wstring(tempDir) + L"snib_ini_utf8.ini";
        const std::wstring installDir = L"C:\\\u4E2D\u6587\u8DEF\u5F84\\SNIB";

        HANDLE f = CreateFileW(utf8Path.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS,
                               FILE_ATTRIBUTE_NORMAL, nullptr);
        CHECK(f != INVALID_HANDLE_VALUE);
        if (f != INVALID_HANDLE_VALUE) {
            const std::string bytes =
                "[Paths]\r\nInstallDir=" + WideToUtf8(installDir) + "\r\n";
            DWORD written = 0;
            static_cast<void>(WriteFile(f, bytes.data(), static_cast<DWORD>(bytes.size()),
                                        &written, nullptr));
            CloseHandle(f);
        }

        const std::vector<Ini::Section> utf8 = Ini::Read(utf8Path);
        CHECK(Ini::Value(utf8, L"Paths", L"InstallDir") == installDir);

        _wremove(utf8Path.c_str());
    }
}

// ---- Http: cancellation -------------------------------------------------------

// A loopback server that sends a header and then dribbles the body out slowly, so
// a transfer can be cancelled while it is genuinely in flight.
//
// The dribble is the point. Cancelling a transfer that has already finished proves
// nothing, and cancelling one that has not started proves less: the case that
// matters is a read blocked inside WinHttpReadData, which is exactly what the
// download will be doing when the user clicks Cancel.
namespace {

struct LoopbackServer {
    SOCKET listener = INVALID_SOCKET;
    std::thread worker;
    uint16_t port = 0;

    bool Start() {
        listener = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
        if (listener == INVALID_SOCKET) return false;

        sockaddr_in addr = {};
        addr.sin_family = AF_INET;
        addr.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
        addr.sin_port = 0;  // let the system pick, so the test cannot collide
        if (bind(listener, reinterpret_cast<sockaddr*>(&addr), sizeof(addr)) != 0) return false;
        if (listen(listener, 1) != 0) return false;

        int len = sizeof(addr);
        if (getsockname(listener, reinterpret_cast<sockaddr*>(&addr), &len) != 0) return false;
        port = ntohs(addr.sin_port);
        return true;
    }

    // Serve one request: headers, then a body in slow pieces, then close.
    void ServeOnce(std::atomic<int>* piecesSent) {
        worker = std::thread([this, piecesSent] {
            SOCKET client = accept(listener, nullptr, nullptr);
            if (client == INVALID_SOCKET) return;

            // Read the request before answering, and close gracefully at the end.
            //
            // Both matter, and the first cost real debugging time: a server that
            // closes a socket while the client's request bytes are still unread
            // makes TCP send an RST rather than a FIN, and the RST discards the
            // response sitting in the client's receive buffer. WinHTTP reports that
            // as ERROR_WINHTTP_INVALID_SERVER_RESPONSE (12152) -- a verdict on the
            // response bytes, which were in fact perfect. The same bytes served over
            // a socket that had drained its request parsed fine.
            {
                char req[2048];
                static_cast<void>(recv(client, req, sizeof(req), 0));
            }

            // A Content-Length far larger than what will actually be sent, which is
            // the point: the transfer cannot complete on its own while the test is
            // still deciding to cancel it.
            const char headers[] =
                "HTTP/1.1 200 OK\r\n"
                "Content-Type: application/octet-stream\r\n"
                "Content-Length: 1048576\r\n"
                "Connection: close\r\n"
                "\r\n";
            if (send(client, headers, static_cast<int>(sizeof(headers) - 1), 0) <= 0) {
                closesocket(client);
                return;
            }

            const char piece[4096] = {};
            for (int i = 0; i < 32; ++i) {
                // A failed send here is the expected outcome once the test cancels:
                // the client closed, so the socket is gone.
                if (send(client, piece, sizeof(piece), 0) <= 0) break;
                piecesSent->fetch_add(1);
                std::this_thread::sleep_for(std::chrono::milliseconds(60));
            }
            shutdown(client, SD_SEND);
            closesocket(client);
        });
    }

    void Stop() {
        if (worker.joinable()) worker.join();
        if (listener != INVALID_SOCKET) closesocket(listener);
        listener = INVALID_SOCKET;
    }
};

}  // namespace

void TestHttpCancellation() {
    if (!Dns::SocketUtils::EnsureWinsock()) {
        CHECK(false);
        return;
    }

    LoopbackServer server;
    CHECK(server.Start());
    if (server.listener == INVALID_SOCKET) return;

    std::atomic<int> piecesSent{0};
    server.ServeOnce(&piecesSent);

    const std::wstring url = L"http://127.0.0.1:" + std::to_wstring(server.port) + L"/body";

    Http::Request request;
    std::atomic<uint64_t> lastProgress{0};
    std::atomic<int> progressCalls{0};
    request.SetProgress([&](uint64_t received) {
        lastProgress.store(received);
        progressCalls.fetch_add(1);
    });

    Http::Result result = Http::Result::Failed;
    // Deliberately non-empty, so the assertion below is about Run() clearing it
    // rather than about a string that was never written.
    std::string body("sentinel");
    std::thread transfer([&] { result = request.Run(url, body); });

    // Wait until the transfer is genuinely mid-body before cancelling. Without this
    // the Stop() could land before the first read, and the test would be asserting
    // that cancelling a transfer that never started returns Cancelled -- true, but
    // not what needs proving.
    const auto deadline = std::chrono::steady_clock::now() + std::chrono::seconds(10);
    while (piecesSent.load() < 3 && std::chrono::steady_clock::now() < deadline)
        std::this_thread::sleep_for(std::chrono::milliseconds(10));

    CHECK(piecesSent.load() >= 3);  // the server did get going

    // Cancelled from a different thread than the one blocked in Run(), which is the
    // whole mechanism.
    request.Stop();
    transfer.join();
    server.Stop();

    CHECK(result == Http::Result::Cancelled);
    CHECK(progressCalls.load() > 0);  // progress was reported before the cancel
    // The partial body must NOT be handed back: a caller hashes what it gets, and a
    // truncated buffer would be reported as a hash mismatch, blaming the manifest.
    CHECK(body.empty());
}

// ---- Progress meter -----------------------------------------------------------

// The meter is driven by a synthetic clock here rather than by real time, which is
// the entire reason it takes its clock as a parameter. A test that slept for three
// seconds to fill the rate window would be slow and flaky; this one advances the
// clock by exactly the right amount and asserts exact numbers.
void TestProgressMeter() {
    Update::Meter meter;

    // Nothing planned: no percentage, no rate, no estimate. A window that shows
    // "0%" before it knows the total is showing an answer it does not have.
    Update::Meter::Snapshot empty = meter.Now();
    CHECK(empty.percent == 0);
    CHECK(empty.bytesPerSecond == 0.0);
    CHECK(empty.etaSeconds == -1);

    // 10 MB over 10 files.
    const uint64_t kTotal = 10'000'000;
    meter.Plan(kTotal, 10);
    meter.SetFile(2, L"data\\one.dat");

    Update::Meter::Snapshot planned = meter.Now();
    CHECK(planned.totalBytes == kTotal);
    CHECK(planned.totalFiles == 10);
    CHECK(planned.doneFiles == 2);
    CHECK(planned.currentFile == L"data\\one.dat");
    CHECK(planned.percent == 0);
    // Planned but no time has passed: still no honest estimate.
    CHECK(planned.etaSeconds == -1);

    // Half done, percent rounds to nearest.
    meter.AddBytes(kTotal / 2);
    CHECK(meter.Now().percent == 50);

    // The first sample starts the window. With fewer than two samples there is no
    // interval to divide by, so the rate stays absent rather than becoming infinite.
    meter.Sample(0.0);
    CHECK(meter.Now().bytesPerSecond == 0.0);

    // A steady 1 MB/s over the window: 100 ms ticks, 100 KB each, for 4 seconds so
    // the 3 s window fills.
    for (int i = 1; i <= 40; ++i) {
        meter.AddBytes(100'000);
        meter.Sample(i * 0.1);
    }
    Update::Meter::Snapshot running = meter.Now();
    // 1 MB/s within rounding: the window spans 3 s and holds 3 MB of arrivals.
    CHECK(running.bytesPerSecond > 950'000.0 && running.bytesPerSecond < 1'050'000.0);
    CHECK(running.etaSeconds > 0);

    // The estimate must be consistent with the rate it reports: remaining bytes at
    // the reported speed.
    const double expectedRemaining =
        static_cast<double>(kTotal - running.doneBytes) / running.bytesPerSecond;
    CHECK(running.etaSeconds >= static_cast<int64_t>(expectedRemaining) - 1);
    CHECK(running.etaSeconds <= static_cast<int64_t>(expectedRemaining) + 1);

    // Byte counting saturates at the total instead of overshooting it, so a manifest
    // whose sizes and body lengths disagree cannot produce "104%".
    meter.AddBytes(kTotal * 2);
    Update::Meter::Snapshot full = meter.Now();
    CHECK(full.doneBytes == kTotal);
    CHECK(full.percent == 100);
    CHECK(full.etaSeconds == -1);  // nothing left to estimate

    // The apply phase reports no rate and no estimate, because neither describes what
    // is happening any more.
    meter.SetApplying(true);
    Update::Meter::Snapshot applying = meter.Now();
    CHECK(applying.applying);
    CHECK(applying.bytesPerSecond == 0.0);
    CHECK(applying.etaSeconds == -1);
    // The percentage is still true, which is why it is not cleared with them.
    CHECK(applying.percent == 100);

    // A new plan clears the rate history. Without that, a stale retry whose total
    // changed would compute a rate across two different scales and report a figure
    // that never happened.
    meter.Plan(1000, 1);
    Update::Meter::Snapshot replanned = meter.Now();
    CHECK(replanned.doneBytes == 0);
    CHECK(replanned.percent == 0);
    CHECK(replanned.bytesPerSecond == 0.0);
    CHECK(replanned.etaSeconds == -1);
    CHECK(!replanned.applying);

    // A stalled transfer: samples keep arriving with no new bytes, so the rate decays
    // to zero and the estimate disappears rather than growing without bound or
    // freezing at its last value.
    meter.AddBytes(200);
    meter.Sample(0.0);
    meter.Sample(1.0);
    meter.Sample(2.0);
    meter.Sample(4.0);
    meter.Sample(6.0);
    Update::Meter::Snapshot stalled = meter.Now();
    CHECK(stalled.bytesPerSecond == 0.0);
    CHECK(stalled.etaSeconds == -1);

    // Formatting. Decimal units, one decimal below 100, none above.
    CHECK(Update::FormatBytes(0) == L"0 B");
    CHECK(Update::FormatBytes(999) == L"999 B");
    CHECK(Update::FormatBytes(1000) == L"1.0 KB" || Update::FormatBytes(1000) == L"1.00 KB");
    CHECK(Update::FormatBytes(12'400'000) == L"12.4 MB");
    CHECK(Update::FormatBytes(512'400'000) == L"512 MB");

    // A rate of zero has no text at all, so the caller omits the field rather than
    // printing "0 B/s".
    CHECK(Update::FormatRate(0.0).empty());
    CHECK(Update::FormatRate(-5.0).empty());
    CHECK(Update::FormatRate(4'600'000.0) == L"4.6 MB/s");

    // Durations: minutes unpadded, seconds always two digits, and hours only when
    // there are any.
    CHECK(Update::FormatDuration(-1).empty());
    CHECK(Update::FormatDuration(0) == L"0:00");
    CHECK(Update::FormatDuration(7) == L"0:07");
    CHECK(Update::FormatDuration(127) == L"2:07");
    CHECK(Update::FormatDuration(3723) == L"1:02:03");
}

}  // namespace

int main() {
    TestNormalizeDomain();
    TestSuffixMatch();
    TestExactMatch();
    TestParseRuleLine();
    TestMatchingSemantics();
    TestNamespaces();
    TestParseQueryAndAction();
    TestQueryUdpPayloadSize();
    TestApplyUdpBudget();
    TestApplyUdpBudgetRejectsBadResponse();
    TestBuildStatusResponse();
    TestBuildResponse();
    TestBuildResponseBlock();
    TestResponseCarriesRuleAddress();
    TestUpdateHelpers();
    TestBase64Canonical();
    TestLowerW();
    TestCommandLineHasFlag();
    TestIniReader();
    TestIniWriteReadAgreement();
    TestUpdaterPlanRoundTrip();
    TestFileSystemSafety();
    TestGlobMatching();
    TestJson();
    TestRepairBudget();
    TestIpEndpointParsing();
    TestDnsStamps();
    TestDnsStampMalformedInput();
    TestUpdateSignatureVerification();
    TestDnsProxyConfigParsing();
    TestHttpResponseParser();
    TestHttpCancellation();
    TestProgressMeter();
    TestTcpSessionFraming();
    TestLiveDnsTransports();

    if (g_failures) {
        std::printf("%d check(s) failed\n", g_failures);
        return 1;
    }
    std::printf("all core tests passed\n");
    return 0;
}
