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

// The updater's own version, independent of APP_VERSION_NUM.
//
// The updater is not a second half of the application; it is a small tool with its own
// contract (the work-order format in updater/plan.h) and its own release cadence. Bumping
// the application must not claim the updater changed, and changing the work-order format
// must not require an application version bump to be meaningful.
//
// The distinction is not cosmetic. The application is delivered through a signed,
// content-addressed manifest, so every byte a release changes has a cost: a version
// derived from APP_VERSION_NUM would make the updater a new file on every single
// release, even when not one line of it changed. Its version is what a maintainer reads
// to decide whether a work-order change is compatible with what is already installed.
#define UPDATER_VERSION_NUM L"1.0.0"
