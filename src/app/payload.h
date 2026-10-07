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
#include <string>
#include <vector>

#include "dns/socket_utils.h"
#include "platform/ports.h"

// The payload's own description of itself.
//
// One file, beside the executable, that the payload owns and that outlives every
// update of the program: [Paths] says where each service's executable is,
// [Uninstall] and [Cache] say what may be deleted, [Directories] says what has to
// exist, and [Resolver] says where the local DNS server listens. Because the exe
// only ever executes what this file declares, the layout under the program
// directory can be restructured in a later release — or by the user — without
// rebuilding anything.
//
// It is named for what it is rather than for its first section. `paths.ini` was
// accurate when locations were all it held, and stopped being accurate the moment
// it also carried the resolver's endpoint; a file whose name describes one of its
// five sections is a name that misleads whoever opens it next.
//
// The name is a CONTRACT and not a preference: this file is what the updater and
// the repair path both look for to decide whether a payload is present, so it
// lives here once rather than as a literal in each of them. It also appears in the
// payload's own [Uninstall] Remove list — which is where an uninstall learns to
// delete it — so a rename is a change in three places, and this is the one that
// the program's code reads.
namespace Payload {

// The file's name, bare, as it sits beside the executable. Used to build its path
// and by the presence check.
inline constexpr wchar_t kFileName[] = L"meta.ini";

// Full path of the payload descriptor: ExeDir() + kFileName.
std::wstring ConfigPath();

// True if the descriptor exists beside the executable.
//
// This is the test for "is a payload installed at all". It is deliberately not a
// check for any particular directory the payload might contain: this file is the
// one thing the payload always has, and a data folder that a later release renames
// must not read as a broken install.
bool Present();

// Read [Resolver] from the payload descriptor at `path`.
//
// Either key may be absent, in which case that half falls back to the compiled-in
// default. A value that is present and unusable — a non-numeric port, an address
// that is not on loopback, an address that does not parse — is refused as a whole
// and the default endpoint is returned, with the reason in the log. A partial
// acceptance would be worse than either: an address from the file beside a port
// from the default is a configuration nobody wrote.
//
// Loopback only, and validated as such here. The NRPT rule this program installs
// routes names to this exact address, so a value the user could point at a real
// interface would send their queries to a server that is not the redirect they
// asked for — and a redirector that answers off-loopback is also a machine
// listening for DNS from anything that can reach it.
Dns::BindEndpoint LoadResolverEndpoint(const std::wstring& path);

// Read [Ports] Required from the payload descriptor.
//
// Each entry is `ADDRESS:PORT[/TRANSPORT][/MODE]`; see the section's own comment in
// meta.ini for the grammar, which is stated there because that is the file someone
// editing a claim is looking at.
//
// What this returns is the set of claims to test — the payload's own endpoints and
// nothing else. The DNS endpoints are not among them: the proxy's listeners come from
// dns_proxy.ini and the resolver's address from [Resolver], both of which the caller
// already reads, and repeating them here would be a second copy of a value that
// exists. The caller assembles the whole set.
//
// An entry that does not parse is skipped with the reason in the log, and a section
// that is absent or empty yields no claims — a payload that declares nothing is
// checked against whatever the caller adds, not against a guessed default.
std::vector<Ports::PortClaim> LoadPortClaims(const std::wstring& path);

}  // namespace Payload
