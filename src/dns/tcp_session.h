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
// DNS-over-TCP framing, in one place.
//
// A DNS message on TCP is a 16-bit big-endian length followed by that many
// bytes. Both the clients here and the upstream sessions speak it, so the
// accumulator and the encoder live together rather than being written once per
// direction per side.
//
// The accumulator is bounded: a peer that announces 65535 bytes and then keeps
// sending cannot make a session allocate without limit. Crossing the bound is
// reported as Broken, which the owner treats as a dead connection.
//
// The bound is enforced on the bytes that have not yet been formed into a
// message, not on the buffer's raw size. A message that has been parsed and is
// awaiting TakeMessage is already framed — its length is known and it is exactly
// as long as it claims — so bytes appended behind it cannot make it invalid, and
// the size check steps aside while one is outstanding. It is re-applied to the
// remainder before any further buffering, so the total still cannot grow without
// limit; what is exempt is one already-claimed message, never free space.
//
// The same guard treats a broken stream as broken: once a session has been told
// Broken, a later Append is not a chance to start over. Callers drop the
// connection when they see it, so accepting further bytes would only hand back a
// message from a stream whose framing has already been abandoned.
#include <cstddef>
#include <cstdint>
#include <vector>

#include "dns/socket_utils.h"

namespace Dns {

// Prefix `message` with its 16-bit length, ready to write to a stream socket.
// Returns empty if `message` is too long to frame, which is the only case a
// caller has to handle separately.
std::vector<uint8_t> EncodeTcpMessage(const std::vector<uint8_t>& message);

// Frame `message` into `out` and reset `sent` to zero, so the buffer is ready to
// be handed to SocketUtils::WritePending.
//
// Both DNS servers owe a client its answer behind a length prefix, and both need
// the same two things done about it: the frame must be built by the shared
// encoder rather than by writing the length bytes at the call site, and the
// offset must go back to zero because the buffer is being replaced rather than
// appended to. A caller that forgot the second would resume mid-answer.
//
// False means the message is too long for a 16-bit prefix and cannot be sent at
// all — the caller should drop the connection rather than write a truncated
// length that the client would read as a shorter message.
bool QueueFramedResponse(std::vector<uint8_t>& out, size_t& sent,
                         const std::vector<uint8_t>& message);

// Reassembles length-prefixed messages from a byte stream that arrives in
// arbitrary pieces. One instance holds one direction of one connection.
class TcpSessionReader {
public:
    // How the last Append() left the stream.
    enum class State {
        Incomplete,  // no complete message yet; keep reading
        Ready,       // a whole message is waiting for TakeMessage()
        Broken,      // framing this receiver cannot recover from
    };

    // Add bytes read from the socket.
    State Append(const uint8_t* data, size_t len);

    // Whether a complete message is waiting.
    bool HasMessage() const { return m_readyLen != 0; }

    // Remove and return the waiting message, clearing it from the stream. If a
    // second message was already buffered behind it, that one becomes ready.
    std::vector<uint8_t> TakeMessage();

    void Clear();

private:
    // Recompute m_readyLen from the front of the stream.
    State Parse();

    // Drop the consumed prefix, shifting the unread tail to the start.
    void Compact();

    // Consumed bytes are never erased as they are taken; m_pos marks where the
    // live stream begins. Front erasure is O(buffer) because a vector has nowhere
    // else to put the tail, so doing it once per message makes draining N
    // pipelined messages O(N^2) — and both servers drain in a loop. The cursor
    // makes taking a message O(message), and Compact() reclaims the prefix once
    // per Append instead of once per message.
    std::vector<uint8_t> m_in;  // stream bytes; [0, m_pos) is already consumed
    size_t m_pos = 0;           // first unconsumed byte
    size_t m_readyLen = 0;      // length of the complete message at m_pos
};

// The client-facing half of one TCP connection, shared by the two servers.
//
// Both serve a client the same way at this level, and it is the level this
// header is about: take whole length-prefixed queries off `reader`, queue a
// length-prefixed answer into `out`, resume an interrupted write from `outSent`
// rather than from the beginning, and treat a connection that has heard nothing
// for its deadline as finished. Those fields and the three operations over them
// are one implementation here rather than the same eight fields copied twice.
//
// What each server adds around it is its own and stays in its own struct: the
// resolver also holds the upstream connection it opened on this client's behalf
// and the reader for that side, and the forwarder holds the handle on the query
// it has racing in its worker pool. Neither is anyone else's business, which is
// why this stops at the connection itself and does not try to be a session.
//
// Defined after TcpSessionReader because it holds one by value. It is trivially
// movable and copyable, which the servers rely on: both keep their sessions in a
// vector and erase from the middle of it when a connection ends.
struct TcpClientConnection {
    SOCKET socket = INVALID_SOCKET;
    uint64_t deadline = 0;

    TcpSessionReader reader;   // queries arriving from the client
    std::vector<uint8_t> out;  // length-prefixed response owed to the client
    size_t outSent = 0;

    // Give the connection an idle deadline measured from now.
    void Touch() { deadline = SocketUtils::Now() + SocketUtils::kTcpIdleTimeoutMs; }

    // Frame `message` into `out`, reset the write cursor, and arm the deadline.
    //
    // The cursor reset is the part worth having in one place: the buffer is
    // being replaced rather than appended to, and a caller that forgot would
    // resume mid-answer on the next writable pass — sending the client the tail
    // of a response whose head it never got.
    //
    // False means the message cannot be framed at all (it is longer than a
    // 16-bit length can describe), in which case nothing has been queued and the
    // caller should drop the connection instead of sending a length that lies.
    bool QueueResponse(const std::vector<uint8_t>& message) {
        if (!QueueFramedResponse(out, outSent, message)) return false;
        Touch();
        return true;
    }

    // Write what is queued, resuming from `outSent`. On Done the buffer is
    // cleared and the cursor reset, so the caller only has to decide what the
    // connection does next — there is no half-written response left to notice.
    SocketUtils::WriteResult Write() {
        const SocketUtils::WriteResult result = SocketUtils::WritePending(socket, out, outSent);
        if (result == SocketUtils::WriteResult::Done) {
            out.clear();
            outSent = 0;
        }
        return result;
    }
};

}  // namespace Dns
