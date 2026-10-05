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

// The updater: a separate, minimal executable that finishes what the main program
// cannot finish for itself.
//
// Its work is defined by the fact that a running image can neither replace nor delete
// itself. The main program therefore writes a work order (see updater/plan.h) naming
// the files involved, starts this program, and exits; this one waits for it to be gone
// and then performs the swap or the removal and starts the new copy.
//
// It is a separate binary rather than a hidden mode of the main executable for two
// reasons, both about what it has to survive:
//
//   * It exists and runs while the executable beside it is being replaced, and while
//     the install directory is being torn down. A mode of that executable would inherit
//     every assumption about that executable still being usable.
//   * Its command line is one fixed flag plus a path this program generated, and it
//     reads no configuration, loads no payload and starts no service. Being separate is
//     what keeps that true, because it links none of the application.
namespace Updater {

// The only command line this program accepts. Returns an exit code once the request has
// been handled; a non-zero code means it was not, and the reason is on the command line
// that was passed rather than in a log this program cannot write.
int Run(const wchar_t* commandLine);

}  // namespace Updater
