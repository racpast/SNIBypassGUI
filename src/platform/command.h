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
#include <windows.h>

#include <shellapi.h>

#include <string>

namespace Command {

// Run a command line hidden and wait for it. Returns the child's exit code, -1
// if it could not be started, or -2 if it was killed on timeout. When `out` is
// given, stdout and stderr are captured into it.
int RunHidden(const std::wstring& cmdline, std::wstring* out = nullptr,
              DWORD timeoutMs = 30000);

// Whether `flag` appears as a complete argument on `fullCommandLine`.
//
// `fullCommandLine` is the whole line, program name included, which is what
// GetCommandLineW returns. Tokenized with CommandLineToArgvW and compared exactly,
// never searched for as a substring: a substring test turns a path, a quoted
// argument, or the tail of a longer flag (`-notautostart`) into a match, which is how
// a launch that was not a logon launch ends up suppressing UI and splitting into a
// different mode.
//
// Every argument is examined INCLUDING argv[0], which the tokenizer makes the program
// name. So this is the right entry point for a line that still carries one, and the
// wrong one for the lpCmdLine a wWinMain receives — that string has the program name
// already removed, so its first argument sits at argv[0]. Use
// CommandLineHasFlagInArgs for it.
//
// The distinction is not cosmetic. This function used to be documented as accepting
// either form and skipped argv[0] unconditionally, which silently made it unable to
// find a flag that was the first argument of a stripped line — precisely the shape a
// scheduled `-autostart` launch arrives in, so logon launches were never recognised.
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
