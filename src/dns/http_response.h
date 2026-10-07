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

#include <cstddef>
#include <cstdint>
#include <vector>

namespace Dns {

// Incrementally parses one HTTP/1.x response carrying a DNS wire message. The
// parser follows Content-Length or chunk boundaries structurally; binary body
// bytes are never searched for framing markers.
class HttpResponseParser {
public:
    enum class Result { NeedMore, Complete, Error };

    Result Feed(const uint8_t* data, size_t len);
    Result Finish();

    Result result() const { return m_result; }
    const std::vector<uint8_t>& body() const { return m_body; }

private:
    enum class Phase {
        Headers,
        FixedBody,
        ChunkSize,
        ChunkData,
        ChunkDataCrlf,
        Trailers,
        Done,
        Failed,
    };

    Result Process();
    void Fail();

    std::vector<uint8_t> m_buffer;
    std::vector<uint8_t> m_body;
    size_t m_offset = 0;
    size_t m_scanOffset = 0;
    size_t m_remaining = 0;
    Phase m_phase = Phase::Headers;
    Result m_result = Result::NeedMore;
};

}  // namespace Dns
