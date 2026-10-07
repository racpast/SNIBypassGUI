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
// See the license file in the project root for full terms and conditions.

#pragma once
#include <windows.h>

#include <shellapi.h>

#include <string>

namespace Command {

// Run command hidden with timeout. Returns exit code, -1 on launch fail, -2 on timeout.
// Output not captured (use supervised children for that).
int RunHidden(const std::wstring& cmdline, DWORD timeoutMs = 30000);

// Check if flag appears as complete argument in full command line (includes argv[0]).
// Use CommandLineHasFlagInArgs for lpCmdLine (program name already stripped).
bool CommandLineHasFlag(const std::wstring& fullCommandLine, const std::wstring& flag);

// Whether `flag` appears as a complete argument on a command line that has ALREADY
// had its program name removed — the `lpCmdLine` a wWinMain is handed.
//
// Separate from the function above rather than a flag on it, because the two strings
// differ in whether they carry a program name and guessing from the content would be
// a heuristic: a program name can be anything, including something that looks like the
// flag being searched for. Which form a caller holds is known at the call site, so it
// is stated there.
bool CommandLineHasFlagInArgs(const std::wstring& argsOnly, const std::wstring& flag);

// Whether this process was started as a logon launch.
//
// The single place that answers the question, so the program and the update helper
// cannot disagree about it. It reads GetCommandLineW() itself rather than taking a
// string, because that is the only form the answer is defined for and passing the
// wrong one in is the mistake this exists to make impossible.
bool IsAutostartLaunch();

// There is deliberately no "run a generated script" helper here.
//
// The self-update and self-removal helpers used to be batch scripts written into
// %TEMP% and started through cmd.exe, which meant every path handed to them was
// re-parsed as command language by an already-elevated shell. Both now go through the
// updater instead (src/updater/), which reads a work order and performs the moves with
// Win32 calls. Anything that would reintroduce a shell on a path assembled from
// runtime data belongs there too, not here.

}  // namespace Command
