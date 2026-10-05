// Copyright © 2026 Racpast. All Rights Reserved.
//
// This file is part of SNIBypassGUI, a proprietary software project.
//
// NOTICE: All information contained herein is, and remains the property of
// Racpast. The intellectual and technical property described herein is
// proprietary to Racpast and is protected by copyright law and international
// treaties. Dissemination of this information or reproduction of this material
// is strictly forbidden unless prior written permission is obtained from Racpast.
//
// Unauthorized copying, modification, distribution, or use of this file,
// via any medium, is strictly prohibited.
//
// For licensing inquiries: snibypassgui@gmail.com or racpast@gmail.com
//
// See the LICENSE.md file in the project root for full terms and conditions.

#include "update/progress.h"

#include <cmath>
#include <cstdio>

namespace Update {

void Meter::Plan(uint64_t totalBytes, size_t totalFiles) {
    m_totalBytes = totalBytes;
    m_totalFiles = totalFiles;
    m_doneBytes = 0;
    m_doneFiles = 0;
    m_currentFile.clear();
    m_applying = false;
    m_firstSampleAt = -1.0;
    // Cleared, not carried over. A stale-retry pass reports a different total, and
    // samples taken against the old one would produce a rate measured across a
    // change of scale.
    m_samples.clear();
}

void Meter::AddBytes(uint64_t n) {
    if (m_doneBytes + n < m_doneBytes) {  // overflow guard: saturate rather than wrap
        m_doneBytes = m_totalBytes;
        return;
    }
    m_doneBytes += n;
    if (m_totalBytes && m_doneBytes > m_totalBytes) m_doneBytes = m_totalBytes;
}

void Meter::SetFile(size_t doneFiles, std::wstring path) {
    m_doneFiles = doneFiles;
    m_currentFile = std::move(path);
}

void Meter::SetApplying(bool applying) {
    m_applying = applying;
}

void Meter::Sample(MeterClock now) {
    if (m_firstSampleAt < 0.0) m_firstSampleAt = now;
    m_samples.push_back({now, m_doneBytes});

    // Drop readings that have fallen out of the window, but always keep at least two
    // so a rate can still be computed when the window has just been reset.
    while (m_samples.size() > 2 && now - m_samples.front().at > kMeterWindowSeconds) {
        m_samples.pop_front();
    }
}

Meter::Snapshot Meter::Now() const {
    Snapshot s;
    s.doneBytes = m_doneBytes;
    s.totalBytes = m_totalBytes;
    s.doneFiles = m_doneFiles;
    s.totalFiles = m_totalFiles;
    s.currentFile = m_currentFile;
    s.applying = m_applying;

    if (m_totalBytes > 0) {
        // Rounded to nearest, and clamped: a bar that reaches 100% while the last file
        // is still being written, or sits at 99% after it finished, both look broken.
        //
        // std::lround rather than a + 0.5 cast: the cast truncates toward zero, so it
        // rounds the wrong way for negatives and invites the mistaken belief that it
        // rounds half up in general.
        const double pct =
            100.0 * static_cast<double>(m_doneBytes) / static_cast<double>(m_totalBytes);
        s.percent = static_cast<int>(std::lround(pct));
        if (s.percent > 100) s.percent = 100;
        if (s.percent < 0) s.percent = 0;
    }

    if (m_applying) {
        // The download is over; a rate and an estimate would describe nothing.
        return s;
    }

    // The rate is measured across the whole window rather than between the last two
    // samples. Reads arrive in bursts, so a per-pair rate alternates between zero and
    // an enormous number; the window is what makes the displayed figure steady.
    if (m_samples.size() >= 2) {
        const Sample_& first = m_samples.front();
        const Sample_& last = m_samples.back();
        const double span = last.at - first.at;
        if (span > 0.0 && last.bytes > first.bytes) {
            s.bytesPerSecond = static_cast<double>(last.bytes - first.bytes) / span;
        }

        // An estimate is only offered once the sampling has been running for a full
        // window, which is the honest condition: before that the rate is measured over
        // a fraction of a second and a remaining-bytes division turns it into a wild
        // guess. During that first window the field is absent, and the window renders
        // it as absent.
        //
        // The window-full check uses the time elapsed since the first Sample() call,
        // not the span between the two ends of the retained deque. The deque is
        // continuously trimmed to the trailing kMeterWindowSeconds, so its span is
        // always strictly less than kMeterWindowSeconds and the >= threshold here
        // would never be reached if the deque were used instead.
        const bool windowFull =
            m_firstSampleAt >= 0.0 && (last.at - m_firstSampleAt) >= kMeterWindowSeconds;
        if (windowFull && s.bytesPerSecond > 1.0 && m_totalBytes > m_doneBytes) {
            const double remaining =
                static_cast<double>(m_totalBytes - m_doneBytes) / s.bytesPerSecond;
            // Bounded so a pathological rate cannot produce a number no duration can
            // format. Anything past a day is reported as no estimate rather than as a
            // figure nobody believes.
            if (remaining < 86400.0) {
                s.etaSeconds = static_cast<int64_t>(std::lround(remaining));
            }
        }
    }

    return s;
}

// ---- Formatting ------------------------------------------------------------

std::wstring FormatBytes(uint64_t bytes) {
    // Decimal units, matching what file managers and the shell report for a download
    // size. This is a display convention (1 MB = 10^6), not a measurement, and mixing
    // it with the binary units used for memory would make the same file appear to
    // change size between two windows.
    static const wchar_t* kUnits[] = {L"B", L"KB", L"MB", L"GB", L"TB"};
    double value = static_cast<double>(bytes);
    size_t unit = 0;
    while (value >= 1000.0 && unit + 1 < 5) {
        value /= 1000.0;
        ++unit;
    }

    // The number is formatted here and the unit is appended as a wide string, rather
    // than both going through one format string.
    //
    // The reason is a real trap and not fastidiousness. MinGW ships two printf
    // personalities that disagree about what %s means inside a WIDE format: C99 says
    // %s is a narrow string and %ls is wide, while MSVCRT's own _swprintf says %s is
    // wide. Which one is in effect depends on the build, and under the C99 reading a
    // wide argument is taken as narrow bytes -- so L"MB" is read as 'M' followed by
    // the string's zero high byte and stops there. That is not hypothetical: it is
    // what this function did, rendering "12.4 MB" as "12.4 M" and "4.6 MB/s" as
    // "4.6 M/s", and it is invisible in any test that only uses the unit "B", since
    // a one-character unit truncates to itself.
    wchar_t number[64];
    if (unit == 0) {
        static_cast<void>(
            swprintf(number, 64, L"%llu", static_cast<unsigned long long>(bytes)));
    } else {
        // One decimal below 100, none above: "9.8 MB" is informative, "512.4 MB" is
        // false precision.
        static_cast<void>(swprintf(number, 64, value < 100.0 ? L"%.1f" : L"%.0f", value));
    }
    return std::wstring(number) + L" " + kUnits[unit];
}

std::wstring FormatRate(double bytesPerSecond) {
    if (bytesPerSecond <= 0.0) return std::wstring();
    return FormatBytes(static_cast<uint64_t>(std::lround(bytesPerSecond))) + L"/s";
}

std::wstring FormatDuration(int64_t seconds) {
    if (seconds < 0) return std::wstring();

    wchar_t buf[32];
    const int64_t h = seconds / 3600;
    const int64_t m = (seconds % 3600) / 60;
    const int64_t s = seconds % 60;
    if (h > 0) {
        static_cast<void>(swprintf(buf, 32, L"%lld:%02lld:%02lld", static_cast<long long>(h),
                                   static_cast<long long>(m), static_cast<long long>(s)));
    } else {
        // Minutes unpadded, seconds always two digits: "2:07", not "02:07".
        static_cast<void>(swprintf(buf, 32, L"%lld:%02lld", static_cast<long long>(m),
                                   static_cast<long long>(s)));
    }
    return buf;
}

}  // namespace Update
