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

#include <windows.h>

#include "updater/updater.h"

// The whole program. Everything it does is in Updater::Run — which is what makes the
// logic testable — and this only delivers the command line to it.
//
// GetCommandLineW, not lpCmdLine. That parameter is the command line with the program
// name already removed, and Updater::Run tokenizes with CommandLineToArgvW, which
// treats the first token as the program name. Handing it lpCmdLine would make the first
// real argument look like argv[0] and be skipped.
int WINAPI wWinMain(HINSTANCE, HINSTANCE, LPWSTR, int) {
    return Updater::Run(GetCommandLineW());
}
