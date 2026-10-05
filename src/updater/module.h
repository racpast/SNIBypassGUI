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
// See the LICENSE.md file in the project root for full license terms.

#pragma once
#include <windows.h>

#include <string>
#include <vector>

// Starting the embedded updater module.
//
// The module is not a file that ships; it is bytes inside this executable (see
// src/updater/ and app.rc). To run it, those bytes are written to a fresh file under
// the same sealed directory the work orders use, and that file is started. Both writes
// — module and plan — therefore go through one directory whose permissions are applied
// in exactly one place, in updater/plan.cpp.
namespace UpdaterModule {

// Extract the embedded module and start it with `args`, detached so it outlives this
// process, which is the entire point of it.
//
// `args` is a list of arguments, not a command line: each element is quoted for
// CreateProcessW here, so a caller cannot produce a malformed or ambiguous line by
// concatenating strings, and nothing is ever interpreted by a shell.
//
// Returns false if the module could not be extracted or started; the reason is logged.
bool Launch(const std::vector<std::wstring>& args);

// Delete module files left behind by earlier runs.
//
// Each run of the updater deletes the copy it was started from, so this is a
// backstop for the one case that cannot: a run killed before it could clean up. Files
// that are still executing cannot be removed and are skipped silently, which is what
// makes calling this unconditionally at startup safe.
void SweepStaleCopies();

}  // namespace UpdaterModule
