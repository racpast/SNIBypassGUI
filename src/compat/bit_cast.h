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
#include <cstring>
#include <type_traits>

// std::bit_cast, for a build that is C++17.
//
// The standard one is C++20. The operation is needed regardless, because Win32 hands
// values across its API as integers and the program has to get pointers back out of
// them: a window's GWLP_USERDATA slot, an LPARAM carrying a pointer, a FARPROC from
// GetProcAddress. Every one of those is the same bits reinterpreted, not a conversion.
//
// The two obvious spellings are each rejected by one of this project's gates, and
// each refusal has a real reason behind it:
//
//   * reinterpret_cast is rejected by GCC's -Wcast-function-type, which is right: it
//     is how a function pointer with a mismatched signature becomes a crash at the
//     call rather than an error at the cast. reinterpret_cast<LONG_PTR> from a
//     pointer also trips clang-tidy's performance-no-int-to-ptr.
//   * std::memcpy between two pointer types is rejected by clang-tidy's
//     bugprone-bitwise-pointer-cast, which is also right: memcpy is not a conversion
//     operator, and using it as one hides what is happening.
//
// A union of the two types satisfies both gates, and that is what the first version of
// this did -- but reading the member that was not written is undefined behaviour in
// C++, merely tolerated by GCC and Clang as an extension. Passing the gates is not the
// same as being correct, and the correct operation exists: the bits are unchanged, and
// __builtin_bit_cast says exactly that, with the semantics std::bit_cast is specified
// to have. It is in GCC since 11 and Clang since 9.
//
// The fallback is for a compiler without the builtin. It is the memcpy the check
// dislikes, taken only where there is no other option, and on this build's compilers
// it is not compiled at all.
template <typename To, typename From>
To BitCast(const From& from) {
    static_assert(sizeof(To) == sizeof(From),
                  "BitCast needs both types to be the same size, since nothing is "
                  "converted and nothing is truncated");
    static_assert(
        std::is_trivially_copyable<To>::value && std::is_trivially_copyable<From>::value,
        "BitCast needs trivially copyable types: the point is that the "
        "object representation IS the value");
#if defined(__has_builtin)
#if __has_builtin(__builtin_bit_cast)
    return __builtin_bit_cast(To, from);
#endif
#endif
    To to;
    // NOLINT(bugprone-bitwise-pointer-cast): the rejected-by-check fallback, reached
    // only on a compiler without __builtin_bit_cast, where it is the least bad option.
    std::memcpy(&to, &from, sizeof(To));
    return to;
}
