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

// APP_VERSION_NUM is the strict dotted numeric version (a.b.c[.d]) used for:
//   - Update trigger comparisons (online vs local)
//   - min_upgradable_from boundary checks
//   - Native FILEVERSION resource
// Never carries a pre-release suffix, so numeric ordering is well-defined.
// Bump APP_VERSION_NUM to trigger an executable update.
//
// For display strings (tray tooltip, about dialog), use GetVersionDisplayStr()
// which pulls from i18n key "version.display" with fallback to APP_VERSION_NUM.
#define APP_VERSION_NUM L"5.2.0"

// The oldest installed version that may be stepped forward IN PLACE through the
// signed update channel. Empty means "no floor": every install upgrades in place.
//
// Set this only for a release that is not compatible with older installs — a change
// to the on-disk layout, the config format, or the payload tree that an older
// `Runtime` cannot survive. Clients below the floor are told to reinstall by hand
// (msg.updReinstall) instead of being half-upgraded into a state that no version
// understands.
//
// It lives here, next to the version it constrains, because it IS version policy:
// it describes what a release accepts about where it starts, not something decided
// per deployment. tools/release.py reads this literal and writes it into the
// signed manifest, so what a reviewer sees in a diff is exactly what ships. There
// is deliberately no command-line override: a second source of truth would let the
// deployed value disagree with the committed one.
//
// Format when set: dotted-numeric, 2 to 4 components ("4.0", "4.0.0", "4.0.0.1").
// Must not exceed APP_VERSION_NUM — a floor above the version being published would
// tell every client, including a fresh install, to reinstall. Both rules are
// enforced at pack time.
#define APP_MIN_UPGRADABLE_FROM L""

#include <string>
std::wstring GetVersionDisplayStr();

#define APP_NAME L"SNIBypassGUI"

// Single-instance mutex. Deliberately in the Global\ namespace, and deliberately
// machine-wide rather than per-session: what a second copy would collide with is
// machine-scoped, not session-scoped. The policy rule (DnsPolicyConfig) is a
// machine-level registry key and the service ports are machine ports, so two sessions
// each running a stack would race the same rule and fight over the same listeners —
// the second one would silently strip the first one's DNS redirection without ever
// taking a lock from it.
//
// The consequence is that this name must NOT be read as "kill every copy on the
// machine". EnforceSingleInstance (main.cpp) filters by session: a copy in another
// session is another user's, keeps its lock and keeps running, and its launcher simply
// exits. Session isolation is enforced there, on the process scan, not by weakening
// this name.
#define APP_MUTEX_NAME L"Global\\SNIBypassGUI_SingleInstance_Mutex"

#define APP_HOMEPAGE L"https://github.com/racpast/SNIBypassGUI"

// Scheduled task used for autostart at logon.
#define APP_TASK_NAME L"SNIBypassGUI_Autostart"
