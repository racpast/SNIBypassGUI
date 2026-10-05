// Copyright © 2026 Racpast. All Rights Reserved.
//
// This file is part of SNIBypassGUI, a proprietary software project.
// See the LICENSE.md file in the project root for full terms and conditions.

#include "dns/http_response.h"

#include <algorithm>
#include <cctype>
#include <limits>
#include <string>

namespace Dns {
namespace {

constexpr size_t kMaxHeaderBytes = size_t{64} * 1024;
constexpr size_t kMaxBodyBytes = 65535;

std::string Lower(std::string value) {
    std::transform(value.begin(), value.end(), value.begin(),
                   [](unsigned char c) { return static_cast<char>(std::tolower(c)); });
    return value;
}

std::string Trim(const std::string& value) {
    size_t begin = 0;
    while (begin < value.size() && (value[begin] == ' ' || value[begin] == '\t')) ++begin;
    size_t end = value.size();
    while (end > begin && (value[end - 1] == ' ' || value[end - 1] == '\t')) --end;
    return value.substr(begin, end - begin);
}

bool ParseDecimal(const std::string& text, size_t& value) {
    if (text.empty()) return false;
    value = 0;
    for (const char c : text) {
        if (c < '0' || c > '9') return false;
        const size_t digit = static_cast<size_t>(c - '0');
        if (value > (std::numeric_limits<size_t>::max() - digit) / 10) return false;
        value = value * 10 + digit;
    }
    return true;
}

bool ParseHex(const std::string& text, size_t& value) {
    if (text.empty()) return false;
    value = 0;
    for (const char c : text) {
        unsigned digit = 0;
        if (c >= '0' && c <= '9')
            digit = static_cast<unsigned>(c - '0');
        else if (c >= 'a' && c <= 'f')
            digit = static_cast<unsigned>(c - 'a' + 10);
        else if (c >= 'A' && c <= 'F')
            digit = static_cast<unsigned>(c - 'A' + 10);
        else
            return false;
        if (value > (std::numeric_limits<size_t>::max() - digit) / 16) return false;
        value = value * 16 + digit;
    }
    return true;
}

size_t FindCrlf(const std::vector<uint8_t>& data, size_t offset) {
    for (size_t i = offset; i + 1 < data.size(); ++i) {
        if (data[i] == '\r' && data[i + 1] == '\n') return i;
    }
    return std::string::npos;
}

size_t FindHeaderEnd(const std::vector<uint8_t>& data, size_t offset) {
    // `i + 4 <= size` is the bound the four-byte marker needs: the last position
    // that can start it is size - 4. The former `i + 3 < size` stopped one byte
    // early, so a header block ending exactly at the end of the buffer was missed
    // until more bytes arrived — harmless for a caller that keeps feeding, but
    // Finish() is documented as terminal and a single Feed-then-Finish would have
    // been told Error on a response that was complete.
    for (size_t i = offset; i + 4 <= data.size(); ++i) {
        if (data[i] == '\r' && data[i + 1] == '\n' && data[i + 2] == '\r' &&
            data[i + 3] == '\n') {
            return i;
        }
    }
    return std::string::npos;
}

}  // namespace

HttpResponseParser::Result HttpResponseParser::Feed(const uint8_t* data, size_t len) {
    if (m_result != Result::NeedMore) return m_result;
    if (data == nullptr && len != 0) {
        Fail();
        return m_result;
    }
    if (len != 0) m_buffer.insert(m_buffer.end(), data, data + len);
    return Process();
}

HttpResponseParser::Result HttpResponseParser::Finish() {
    if (m_result != Result::NeedMore) return m_result;
    Process();
    if (m_result == Result::NeedMore) Fail();
    return m_result;
}

void HttpResponseParser::Fail() {
    m_phase = Phase::Failed;
    m_result = Result::Error;
    m_body.clear();
}

HttpResponseParser::Result HttpResponseParser::Process() {
    for (;;) {
        if (m_phase == Phase::Headers) {
            const size_t headEnd = FindHeaderEnd(m_buffer, m_scanOffset);
            if (headEnd == std::string::npos) {
                if (m_buffer.size() > kMaxHeaderBytes) Fail();
                m_scanOffset = m_buffer.size() > 3 ? m_buffer.size() - 3 : 0;
                return m_result;
            }
            if (headEnd + 4 > kMaxHeaderBytes) {
                Fail();
                return m_result;
            }

            const std::string head(reinterpret_cast<const char*>(m_buffer.data()), headEnd);
            const size_t statusEnd = head.find("\r\n");
            if (statusEnd == std::string::npos || head.compare(0, 5, "HTTP/") != 0) {
                Fail();
                return m_result;
            }
            const std::string status = head.substr(0, statusEnd);
            const size_t space = status.find(' ');
            if (space == std::string::npos || space + 4 > status.size() ||
                status.substr(space + 1, 3) != "200" ||
                (space + 4 < status.size() && status[space + 4] != ' ')) {
                Fail();
                return m_result;
            }

            bool hasLength = false;
            size_t contentLength = 0;
            std::string transferEncoding;
            size_t lineStart = statusEnd + 2;
            while (lineStart < head.size()) {
                const size_t lineEnd = head.find("\r\n", lineStart);
                const size_t end = lineEnd == std::string::npos ? head.size() : lineEnd;
                const std::string line = head.substr(lineStart, end - lineStart);
                const size_t colon = line.find(':');
                if (colon == std::string::npos || colon == 0) {
                    Fail();
                    return m_result;
                }
                const std::string name = Lower(Trim(line.substr(0, colon)));
                const std::string value = Trim(line.substr(colon + 1));
                if (name == "content-length") {
                    size_t parsed = 0;
                    if (!ParseDecimal(value, parsed) ||
                        (hasLength && parsed != contentLength)) {
                        Fail();
                        return m_result;
                    }
                    hasLength = true;
                    contentLength = parsed;
                } else if (name == "transfer-encoding") {
                    if (!transferEncoding.empty()) transferEncoding += ',';
                    transferEncoding += Lower(value);
                }
                if (lineEnd == std::string::npos) break;
                lineStart = lineEnd + 2;
            }

            m_offset = headEnd + 4;
            if (!transferEncoding.empty()) {
                if (hasLength || Trim(transferEncoding) != "chunked") {
                    Fail();
                    return m_result;
                }
                m_phase = Phase::ChunkSize;
                m_scanOffset = m_offset;
            } else if (hasLength) {
                if (contentLength > kMaxBodyBytes) {
                    Fail();
                    return m_result;
                }
                m_remaining = contentLength;
                m_phase = contentLength == 0 ? Phase::Done : Phase::FixedBody;
            } else {
                // A close-delimited response cannot be distinguished from a
                // timeout or cancellation by the TLS receive API. Require an
                // explicit framing mechanism so partial DNS messages are never
                // accepted as complete.
                Fail();
                return m_result;
            }
        }

        if (m_phase == Phase::FixedBody || m_phase == Phase::ChunkData) {
            const size_t available = m_buffer.size() - m_offset;
            const size_t take = std::min(available, m_remaining);
            if (take > kMaxBodyBytes - m_body.size()) {
                Fail();
                return m_result;
            }
            m_body.insert(m_body.end(), m_buffer.begin() + static_cast<ptrdiff_t>(m_offset),
                          m_buffer.begin() + static_cast<ptrdiff_t>(m_offset + take));
            m_offset += take;

            m_remaining -= take;
            if (m_remaining != 0) return m_result;
            m_phase = (m_phase == Phase::FixedBody) ? Phase::Done : Phase::ChunkDataCrlf;
        }

        if (m_phase == Phase::ChunkSize) {
            const size_t lineEnd = FindCrlf(m_buffer, m_scanOffset);
            if (lineEnd == std::string::npos) {
                if (m_buffer.size() - m_offset > 128) Fail();
                m_scanOffset =
                    std::max(m_offset, m_buffer.empty() ? size_t{0} : m_buffer.size() - 1);
                return m_result;
            }
            std::string sizeText(reinterpret_cast<const char*>(m_buffer.data() + m_offset),
                                 lineEnd - m_offset);
            const size_t semi = sizeText.find(';');
            if (semi != std::string::npos) sizeText.resize(semi);
            sizeText = Trim(sizeText);
            size_t chunkSize = 0;
            if (!ParseHex(sizeText, chunkSize) || chunkSize > kMaxBodyBytes - m_body.size()) {
                Fail();
                return m_result;
            }
            m_offset = lineEnd + 2;
            m_scanOffset = m_offset;
            m_remaining = chunkSize;
            m_phase = chunkSize == 0 ? Phase::Trailers : Phase::ChunkData;
            continue;
        }

        if (m_phase == Phase::ChunkDataCrlf) {
            if (m_buffer.size() - m_offset < 2) return m_result;
            if (m_buffer[m_offset] != '\r' || m_buffer[m_offset + 1] != '\n') {
                Fail();
                return m_result;
            }
            m_offset += 2;
            m_phase = Phase::ChunkSize;
            m_scanOffset = m_offset;
            continue;
        }

        if (m_phase == Phase::Trailers) {
            const size_t lineEnd = FindCrlf(m_buffer, m_scanOffset);
            if (lineEnd == std::string::npos) {
                if (m_buffer.size() - m_offset > kMaxHeaderBytes) Fail();
                m_scanOffset =
                    std::max(m_offset, m_buffer.empty() ? size_t{0} : m_buffer.size() - 1);
                return m_result;
            }
            if (lineEnd == m_offset) {
                m_offset += 2;
                m_phase = Phase::Done;
            } else {
                const auto begin = m_buffer.begin() + static_cast<ptrdiff_t>(m_offset);
                const auto end = m_buffer.begin() + static_cast<ptrdiff_t>(lineEnd);
                if (std::find(begin, end, static_cast<uint8_t>(':')) == end) {
                    Fail();
                    return m_result;
                }
                m_offset = lineEnd + 2;
                m_scanOffset = m_offset;
                continue;
            }
        }

        if (m_phase == Phase::Done) {
            m_result = Result::Complete;
            return m_result;
        }
        if (m_phase == Phase::Failed) return m_result;
    }
}

}  // namespace Dns
