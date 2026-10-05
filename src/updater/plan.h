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

// The updater's work order.
//
// A batch script cannot be used for this: batch is a command language, so every path
// written into one is re-parsed as syntax, and the script that resulted ran elevated.
// This module is the replacement — the work order is a plain data file in a directory
// only administrators and SYSTEM can write to, and the only thing that ever carries it
// is an explicit `CreateProcessW` argument that this program composes itself. Nothing
// on the path from work order to executed file passes through a shell, so a path
// containing &, |, ^, %, " or a newline is a path and stays one.
//
// Both sides of the transaction link this file. The application writes a plan for work
// that can only happen after it exits; the updater reads it back, with no shared runtime
// state between them beyond the bytes on disk and a process id. The updater is embedded
// in the application as a resource and extracted at the moment it is needed (see
// updater/module.cpp), so the two are built together and shipped as one file — which is
// why this format pins its keys as string literals anyway: the two sides are separate
// translation units with separate state, and a struct field rename must not silently
// change the on-disk contract.
namespace Updater {

enum class Op {
    // Replace the executable at `target` with the staged copy at `newFile`, keep the
    // previous one at `backup` until the swap is confirmed, then start the new one.
    Replace,
    // Delete the executable and remove its directory if that leaves it empty.
    Remove,
};

// The plan itself. The keys written to the file are string literals rather than
// derived from these members: the on-disk shape is a contract between two separate
// translation units, and renaming a field here must not silently change it.
struct Plan {
    Op op = Op::Replace;
    std::wstring target;     // the executable to replace (Replace) or delete (Remove)
    std::wstring newFile;    // staged replacement; Replace only
    std::wstring backup;     // where the previous executable is kept; Replace only
    std::wstring dir;        // install directory, for the empty-rmdir step; Remove only
    DWORD parentPid = 0;     // the process that will exit and release `target`
    bool autostart = false;  // relaunch with the logon flag
};

// A plan of at most this many bytes is accepted. The record is a few hundred bytes;
// the bound merely stops a malformed or hostile file from being read into memory.
constexpr DWORD kMaxPlanBytes = 16 * 1024;

// Append a random token to a fixed prefix, giving a file name an attacker cannot
// predict. Uses BCryptGenRandom; falls back to a locally mixed value when the provider
// is missing, which is less obscure but still not a name that can be planted in advance.
std::wstring RandomPlanFileName();

// The directory plans are written to: a per-machine location that only administrators
// and SYSTEM can create files in. Created on demand. Empty when it cannot be secured,
// in which case the caller must NOT fall back to a user-writable location.
std::wstring PlanDirectory();

// Serialize `plan` and create the file at `fullPath` with CREATE_NEW, so an existing
// file — or a reparse point planted in its place — is never opened or overwritten. The
// directory must already exist. True on success.
bool WritePlan(const std::wstring& fullPath, const Plan& plan);

// Read and validate the plan at `fullPath`. Every required field must be present, and
// duplicates, unknown keys and reparse points are rejected rather than tolerated. False
// on anything unexpected, with `why` set to a single English line for the log.
bool ReadPlan(const std::wstring& fullPath, Plan& plan, std::wstring& why);

// Remove the plan file. Named apart from the FileSystem module, which lives in the
// application and is deliberately not linked into the updater.
void DeletePlanFile(const std::wstring& fullPath);

}  // namespace Updater
