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
#include <cstdint>
#include <functional>
#include <string>
#include <vector>

#include "update/http.h"

// The manifest is parsed by update/json.h; named here only so the note parser below
// can take one by reference without this header pulling in the JSON reader.
namespace Json {
struct Value;
}

// Update endpoints. Chunk URLs in the manifest are relative to the manifest's own
// directory, so the whole tree can be rehosted under any prefix without editing it.
#define UPDATE_MANIFEST_URL L"https://update.rpnet.cc/snibypassgui/manifest.json"
#define UPDATE_MANIFEST_SIG_URL UPDATE_MANIFEST_URL L".sig"

// Every updatable file — the executable and every data asset — is delivered as a
// list of content-addressed chunks, each small enough for the per-file host limit.
// The manifest describing them is signed with ECDSA P-256/SHA-256 and verified
// against the public key compiled into this binary, so integrity does not depend
// on TLS or on the host being uncompromised.
namespace Update {

struct Chunk {
    std::wstring name;  // path relative to the manifest's directory
    uint64_t size = 0;
    std::wstring sha256;  // lowercase hex
};

struct File {
    std::wstring path;  // install path relative to the program directory
    bool isExe = false;
    uint64_t size = 0;
    std::wstring sha256;  // lowercase hex of the whole file
    std::vector<Chunk> chunks;
};

struct Info {
    bool ok = false;
    std::wstring error;  // populated when ok == false
    std::wstring version;

    // What changed in this publish, for the current language, as items rather than
    // as a pre-formatted block.
    //
    // The manifest carries an array per language (see _load_release_notes in
    // tools/release.py, which normalizes the older bullet-prefixed string into one),
    // so the rendering decision — one line, or a bulleted list, and where it
    // truncates — belongs to whoever shows it. Keeping the text here with its
    // markers intact would move that decision into the changelog file, where it
    // would differ per entry and per language.
    std::vector<std::wstring> notes;

    uint64_t chunkSize = 0;
    bool reinstallRequired = false;  // we predate min_upgradable_from
    std::vector<File> files;
};

// What the manifest's version means relative to the running executable.
//
// This exists so the ordering question — "is the channel ahead of us, behind us, or
// the same as us?" — is answered in exactly one place. It used to be recomputed
// independently in the per-file update decision (client.cpp) and again in the tray's
// message selection, two evaluations of the same pair of operands with nothing
// keeping them in agreement.
//
// The distinction is not cosmetic. The executable-update decision is INEQUALITY, not
// "greater than": a force-aligned channel must be able to move clients DOWN as well
// as up, so Downgrade means "update, in that direction", not "refuse".
enum class UpdateKind {
    None,       // the channel serves the same version we run
    Upgrade,    // the channel is ahead of us
    Downgrade,  // the channel is behind us (a force-aligned rollback)
};

// Classify the manifest version against the running executable's version.
UpdateKind Classify(const Info& info);

// Fetch manifest.json and manifest.json.sig, verify the signature over the exact
// manifest bytes, then parse. Info.ok is false on any network, parse, signature or
// policy failure, with Info.error set to a localized reason.
Info FetchManifest();

// True if any manifest file differs from what is installed. `summary` gets one
// line per file that would change.
bool UpdateAvailable(const Info& info, std::wstring& summary);

// Optional progress hooks and the cancellation handle. Every callback fires on the
// thread that called PerformUpdate and may be left empty.
struct Progress {
    // Fired as each changed file BEGINS transferring: `done` is its 1-based index
    // among the `total` files that will change. Runs while the services are still
    // up — the download only writes <path>.new and never touches the live install.
    std::function<void(size_t done, size_t total, const std::wstring& path)> onFile;

    // Fired as bytes arrive, with the count for the WHOLE run so far, so the bar
    // describes the job rather than the current file. `path` is the file being
    // fetched. The overall figure is computed here because this is the layer that
    // knows every file's size; a consumer that tried to reconstruct it from the
    // per-file counts would have to guess the size of each file as it finished.
    std::function<void(const std::wstring& path, uint64_t bytesOverall)> onBytes;

    // Fired ONCE after every file is downloaded and verified, immediately before
    // the first byte of the live install is replaced. This is where the caller
    // stops the services: deferring the stop to here keeps the proxy up for the
    // whole download and only drops it for the seconds the apply takes. It never
    // fires if the download fails, so a mid-download failure leaves the services
    // untouched. Cancellation is refused from this point on.
    std::function<void()> onBeforeApply;

    // The transfer object the download runs through, so the caller can cancel a
    // running update by calling request->Stop() from another thread. Owned by the
    // caller and outliving this call. May be null, in which case the download still
    // works but cannot be interrupted mid-transfer.
    Http::Request* request = nullptr;
};

// What an update run did.
//
// This used to be a bool, which had to carry three unrelated meanings at once — "an
// executable swap was scheduled", "nothing needed doing" and "it failed" all came
// back as false — and could not express the fourth state that a progress window with
// a Cancel button creates. Cancellation in particular must never be reported to the
// user as a failure: they asked for it.
enum class Outcome {
    Applied,    // changes were applied; the caller exits if an exe swap was scheduled
    UpToDate,   // nothing needed changing
    Failed,     // a real failure, already reported through the dialog
    Cancelled,  // the user stopped it; the install is untouched and nothing is reported
};

// Download and verify every changed file into <path>.new, then apply them all at
// once: assets are moved into place keeping a .bak, and the running executable is
// swapped by a wait-and-relaunch helper. Any failure before the apply leaves the
// install untouched; a failure during it rolls back from the .bak copies.
//
// The return value says WHAT happened. `exeSwapPending`, when given, says whether
// the caller MUST EXIT so the helper can replace the binary — a separate question,
// because a run that applied only assets returns Outcome::Applied and does not
// require an exit. Those two were one bool before, which is why a caller that only
// wanted "should I quit" could not be told anything else.
Outcome PerformUpdate(const Info& info, const Progress& progress = {},
                      bool* exeSwapPending = nullptr);

// Lowercase hex SHA-256 of a file. Empty on failure.
std::wstring Sha256File(const std::wstring& path);

// True if `f` differs from what is installed: the executable by version, data assets
// by content hash or by being absent.
//
// Exposed because a caller has to size the job before it starts — the progress window
// reports "done of total" bytes, and the total is the sum over exactly these files.
// This module cannot answer that for the caller without knowing which files will
// change, and a second implementation of the same rule is how the bar and the work end
// up disagreeing.
bool NeedsUpdate(const File& f, const Info& info);

// ---- Pure helpers (no I/O; exposed for unit testing) ------------------------

// The directory part of `url`, including the trailing '/'.
std::wstring UrlBaseDir(const std::wstring& url);

// Numeric-aware dot-separated version compare (tolerates a leading 'v'/'V' and
// ignores per-component suffixes). Returns <0, 0, >0 like strcmp. Drives both the
// executable-update decision and the min_upgradable_from floor. The executable
// updates whenever manifest.version != APP_VERSION_NUM (INEQUALITY, not ">"), so
// the client follows the channel DOWN as well as up — a force-aligned downgrade is
// a normal update. Only the strict-numeric APP_VERSION_NUM is ever passed here.
int CompareVersions(const std::wstring& a, const std::wstring& b);

// True if `s` is a 64-character lowercase hex SHA-256 digest.
bool IsHexDigest(const std::wstring& s);

// The release notes for `lang` out of a manifest's `notes` object, as items.
//
// Pure, and public for that reason: which language a reader gets, and how a value
// is turned into items, are decisions the tray's message depends on and nothing else
// can check. It was inline in FetchManifest, and that is where two defects lived
// unnoticed — see the implementation for both.
//
// Three shapes are accepted, because the field's shape is a contract between this
// client and whatever publish is currently being served, and the two get updated at
// different times:
//
//   {"en": ["a", "b"]}          the shape tools/release.py writes now
//   {"en": "a\nb"}              what every publish before it wrote, and what the
//   {"en": "• a\n• b"}          channel still serves until the next release
//
// A string is treated as one item per line, with a leading bullet marker removed —
// the marker belonged to a reader that showed the text verbatim, and the renderer
// that draws these items adds its own.
//
// `lang` is the manifest's key ("en" / "zh-CN"), not a Lang: this has no opinion
// about which language is wanted, only about what the manifest says. English is the
// fallback for a language the manifest does not carry, and only then — a value that
// is present, whatever its shape, is read.
std::vector<std::wstring> ParseNotes(const Json::Value& notes, const char* lang);

}  // namespace Update
