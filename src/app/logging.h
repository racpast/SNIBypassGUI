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
#include <string>

// Appends to logs\snibypassgui.log. Off by default (cheap atomic check).
// Directory created on first write. All state is trivially destructible
// (safe to call after main() returns).
void LogInit();
void LogLine(const std::wstring& level, const std::wstring& msg);

// The live switch. LogSetEnabled persists the choice as well, so it is the single
// call a caller needs; LogEnabled answers without touching the disk.
bool LogEnabled();
void LogSetEnabled(bool on);

#define LOGI(m) LogLine(L"INFO", (m))
#define LOGW(m) LogLine(L"WARN", (m))
#define LOGE(m) LogLine(L"ERROR", (m))
