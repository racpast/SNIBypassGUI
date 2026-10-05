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

// config.ini beside the executable. Every setting lives under [General] and
// defaults to off/absent, so a missing file behaves like a fresh install.
std::wstring SettingsPath();

// Persistence for the logging switch. Callers outside logging.cpp should use
// LogSetEnabled/LogEnabled instead: those keep the in-memory flag a log call reads
// in step with the file, so writing a line never has to touch the disk to find out
// whether it is allowed to.
bool LoggingEnabled();
void SetLoggingEnabled(bool on);

// Whether the user has accepted the CURRENT text of the agreement, identified by the
// SHA-256 of the embedded document.
//
// What is stored is the hash of the text that was accepted, and nothing else — there
// is no separate "accepted" boolean. A boolean cannot answer the question that
// matters: "accepted WHICH text?". With one stored, every existing user counts as
// having agreed to whatever text a later release ships, including text they have
// never been shown.
//
// The key is new, so an older install's `EulaAccepted=1` is simply not read: an
// absent hash means not accepted, and the agreement is shown once more. There is no
// migration, deliberately — a migration would have to invent an answer to "which
// text did they accept?", and the honest answer for a pre-hash install is that we
// cannot know.
bool EulaAccepted(const std::string& textHash);
void SetEulaAccepted(const std::string& textHash);
bool AutoUpdateEnabled();
void SetAutoUpdateEnabled(bool on);

// Desktop-shortcut preference. Tri-state on purpose: "never asked" must stay
// distinct from "asked and declined", so a decline can be permanent while a
// "wanted" remains actionable on later launches.
enum class ShortcutPref { Unset, Declined, Wanted };
ShortcutPref GetShortcutPref();
void SetShortcutPref(ShortcutPref p);
