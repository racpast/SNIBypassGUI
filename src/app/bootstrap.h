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

// First-run and payload-presence checks.
//
// The payload (paths.ini and accompanying data files) ships beside the executable
// in the distribution archive. The executable copes with it being absent in two
// distinct situations:
//
//   * the user extracted the whole archive       -> present, run normally
//   * the executable was run from inside an
//     archive viewer                             -> a helpful "extract first"
//                                                   refusal, never going online
//
// A real install with files deleted after the fact is handled by
// Controller::RepairIfNeeded(), which runs after the tray is up.
namespace Bootstrap {

// True if paths.ini is present beside the executable. paths.ini is the payload's
// root — everything the application reads is located through it — so its presence
// is the one stable contract regardless of what else the payload contains.
bool PayloadPresent();

// True if the executable is running from an archiver's scratch directory rather than
// a fixed install folder.
bool RunningFromArchiveTemp();

// Reconcile the desktop shortcut with the stored preference, prompting on a first
// run that has never been asked.
void SyncDesktopShortcut();

}  // namespace Bootstrap
