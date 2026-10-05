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

#include "dns/tcp_session.h"

#include <cstring>

namespace Dns {
namespace {

// The two-byte prefix describes the payload only, so all 65535 payload bytes are
// valid and the in-memory framed representation may occupy 65537 bytes.
constexpr size_t kMaxFramedMessage = SocketUtils::kMaxMessage;

}  // namespace

std::vector<uint8_t> EncodeTcpMessage(const std::vector<uint8_t>& message) {
    if (message.empty() || message.size() > kMaxFramedMessage) return {};

    std::vector<uint8_t> framed;
    framed.reserve(message.size() + 2);
    framed.push_back(static_cast<uint8_t>((message.size() >> 8u) & 0xFFu));
    framed.push_back(static_cast<uint8_t>(message.size() & 0xFFu));
    framed.insert(framed.end(), message.begin(), message.end());
    return framed;
}

bool QueueFramedResponse(std::vector<uint8_t>& out, size_t& sent,
                         const std::vector<uint8_t>& message) {
    std::vector<uint8_t> framed = EncodeTcpMessage(message);
    if (framed.empty()) return false;
    out = std::move(framed);
    sent = 0;
    return true;
}

TcpSessionReader::State TcpSessionReader::Parse() {
    // From here on, everything before m_pos is consumed: either it was taken, or
    // it was compacted away. Compaction always happens before new bytes are
    // buffered, so this is never called with consumed bytes still in front.
    m_readyLen = 0;

    const size_t available = m_in.size() - m_pos;

    // Two bytes of length, then that many bytes of message. Anything short of
    // the prefix is simply not a message yet.
    if (available < 2) return State::Incomplete;

    const size_t declared =
        (static_cast<size_t>(m_in[m_pos]) << 8u) | static_cast<size_t>(m_in[m_pos + 1]);

    // A zero-length message is legal on the wire and means nothing to anyone;
    // without rejecting it here the prefix would never advance and the
    // connection would spin forever on the same two bytes.
    if (declared == 0) return State::Broken;

    if (declared > kMaxFramedMessage) return State::Broken;

    if (available < declared + 2) return State::Incomplete;

    m_readyLen = declared;
    return State::Ready;
}

// Drop the consumed prefix from the front of the buffer, shifting the unread tail
// down to the start and resetting the cursor. Everything before m_pos is either
// already taken or about to be superseded, so nothing that matters is lost.
void TcpSessionReader::Compact() {
    if (m_pos == 0) return;
    if (m_pos >= m_in.size()) {
        m_in.clear();
    } else {
        m_in.erase(m_in.begin(), m_in.begin() + static_cast<ptrdiff_t>(m_pos));
    }
    m_pos = 0;
}

TcpSessionReader::State TcpSessionReader::Append(const uint8_t* data, size_t len) {
    // A broken stream stays broken: the caller has already been told to drop
    // the connection and must not be handed a half-parsed message afterwards.
    //
    // The check is skipped only when a whole message has been claimed and not yet
    // taken. That is safe because the cap below is measured against what is left
    // after the claimed message, so a stream that has genuinely overflowed cannot
    // reach the end of this function with the skip in force — it comes back
    // Broken from the cap itself. The skip exists so that appending more bytes
    // after a message was claimed cannot invalidate it.
    if (m_readyLen == 0 && m_in.size() - m_pos > kMaxFramedMessage + 2) return State::Broken;

    if (data != nullptr && len > 0) {
        // Reclaim the taken prefix before deciding whether this append fits.
        // Without this the cursor's savings would be invisible to the bound.
        Compact();

        // Refuse the read rather than growing without bound. This is the only
        // place the cap can be enforced once bytes are in flight, because the
        // prefix is not readable until it has been buffered.
        if (len > kMaxFramedMessage + 2 - m_in.size()) {
            m_in.clear();
            m_pos = 0;
            m_readyLen = 0;
            return State::Broken;
        }
        m_in.insert(m_in.end(), data, data + len);
    }

    return Parse();
}

std::vector<uint8_t> TcpSessionReader::TakeMessage() {
    if (m_readyLen == 0) return {};

    // Copied out before the cursor moves, and the new cursor lands just past the
    // message, so the bytes behind it are never walked again. Advancing the cursor
    // rather than erasing from the front is what keeps N pipelined messages at
    // O(N) total instead of paying a front-erase memmove per message.
    std::vector<uint8_t> message(m_in.begin() + static_cast<ptrdiff_t>(m_pos + 2),
                                 m_in.begin() + static_cast<ptrdiff_t>(m_pos + 2 + m_readyLen));
    m_pos += 2 + m_readyLen;
    Parse();  // a pipelined second message may already be sitting here
    return message;
}

void TcpSessionReader::Clear() {
    m_in.clear();
    m_pos = 0;
    m_readyLen = 0;
}

}  // namespace Dns
