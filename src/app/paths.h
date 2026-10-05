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

// Locations derived from where the executable actually runs, so a copy moved to
// another folder resolves everything relative to its new home.
std::wstring ExePath();  // full path of this executable
std::wstring ExeDir();   // directory containing it, with a trailing backslash
std::wstring DataDir();  // ExeDir() + "data\"

// ExeDir() + `rel`, accepting forward or backward slashes in `rel`.
std::wstring PathUnder(const std::wstring& rel);

// The directory portion of `path`, INCLUDING the trailing separator, or empty when
// `path` has no separator at all.
//
// Empty rather than a fallback, because what a bare file name means differs by caller:
// a service executable launched with no directory in its path should run from ours, and
// a name being resolved relative to a data file belongs in that file's own directory.
// Each caller states which it wants; a helper that guessed would be wrong for one of
// them. This had been written twice under the same name with two different fallbacks.
//
// Both separators are accepted, matching PathUnder.
std::wstring DirPart(const std::wstring& path);
