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

#pragma once
#include <cstdint>
#include <deque>
#include <string>

// Progress arithmetic for the update download: how far along, how fast, and how
// much longer.
//
// This is separated from the window for one reason: it is the part that is easy to
// get wrong and impossible to check by looking at it. A speed that flickers between
// zero and 200 MB/s, or an estimate that counts down to zero and then counts back
// up, both look like a broken window when they are really a broken formula — and
// neither can be caught by eye on one machine. Here it is a pure function of
// (bytes, time), so a test can drive it with a synthetic clock and assert the
// numbers.
namespace Update {

// A monotonic instant, in seconds since some arbitrary origin. The caller supplies
// these rather than the class reading a clock, which is what makes the behaviour
// testable: a test can advance time by exactly 100 ms and know what the answer is.
using MeterClock = double;

class Meter {
public:
    // Start a new run. Clears the rate history, so a retry whose total changed does
    // not inherit samples from the pass that failed.
    void Plan(uint64_t totalBytes, size_t totalFiles);

    // Bytes that have arrived. Monotonic within a pass.
    void AddBytes(uint64_t n);

    // Which file is being fetched, and how many are done. Display only.
    void SetFile(size_t doneFiles, std::wstring path);

    // The apply phase has begun: downloading is over, so the rate and the estimate
    // stop applying and the window shows an indeterminate bar instead.
    void SetApplying(bool applying);

    // Take a time reading. Called on a timer by the UI, roughly every 100 ms. The
    // rate is computed from the samples this collects, so calling it more or less
    // often changes the resolution, not the answer.
    void Sample(MeterClock now);

    struct Snapshot {
        uint64_t doneBytes = 0;
        uint64_t totalBytes = 0;
        size_t doneFiles = 0;
        size_t totalFiles = 0;
        std::wstring currentFile;

        // 0..100. Reported as a rounded integer because that is what a progress bar
        // and a percentage label both want; keeping the fraction here instead would
        // put the same rounding in two callers.
        int percent = 0;

        // Bytes per second over the trailing window. 0 until there is a basis for it.
        double bytesPerSecond = 0.0;

        // Seconds remaining, or -1 when there is no honest answer yet -- the first
        // moments of a transfer, or a stalled one. An absent estimate is a real state
        // and is rendered as absent; substituting 0 or a guess is how a progress
        // window ends up lying.
        int64_t etaSeconds = -1;

        bool applying = false;
    };

    Snapshot Now() const;

private:
    // One (time, cumulative bytes) reading.
    struct Sample_ {
        MeterClock at;
        uint64_t bytes;
    };

    uint64_t m_totalBytes = 0;
    uint64_t m_doneBytes = 0;
    size_t m_totalFiles = 0;
    size_t m_doneFiles = 0;
    std::wstring m_currentFile;
    bool m_applying = false;
    // Time of the first Sample() call in this run. -1 until the first sample
    // arrives. Reset by Plan() so a retry does not inherit a head start.
    MeterClock m_firstSampleAt = -1.0;

    // The trailing window of readings the rate is measured over. Bounded by age, not
    // by count, so the window is the same number of seconds whether the UI samples
    // at 10 Hz or 30 Hz.
    std::deque<Sample_> m_samples;
};

// The width of the window the rate is averaged over, in seconds.
//
// Long enough that a burst or a gap does not swing the number, short enough that a
// genuine change in throughput is visible within a second or two. A per-sample rate
// would be useless here: the reads arrive in 64 KB bursts, so the instantaneous rate
// alternates between zero and a huge number and the display reads as broken.
constexpr MeterClock kMeterWindowSeconds = 3.0;

// ---- Formatting ------------------------------------------------------------
//
// Language-neutral: these produce "4.6 MB/s" and "2:13", and the translated
// sentence around them comes from the i18n table. Composing a unit inside a
// translated string would need the number to be substituted into it, which the
// string table deliberately does not support.
std::wstring FormatBytes(uint64_t bytes);        // "12.4 MB"
std::wstring FormatRate(double bytesPerSecond);  // "4.6 MB/s"
std::wstring FormatDuration(int64_t seconds);    // "2:13", "1:02:03"

}  // namespace Update
