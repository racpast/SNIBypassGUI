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
//
// MinGW headers expose the C23 memset_explicit API before the runtime provides
// its symbol. libsodium correctly uses that API for secret erasure, so provide
// the missing semantics instead of aliasing it to optimizable memset.
//
// WHY THIS FILE EXISTS AND CANNOT BE DELETED:
//
// The prebuilt libsodium archive (libsodium-1.0.22-stable-mingw.tar.gz) was
// compiled with an older GCC toolchain and its object files reference the
// memset_explicit symbol without defining it. That symbol is C23; libsodium
// uses it to erase secrets in a way the optimizer is not allowed to remove.
//
// Without this file, linking fails with:
//   undefined reference to `memset_explicit'
//     libsodium.a(libsodium_la-utils.o):utils.c
//
// DO NOT replace this with plain memset() or an alias to one. memset() on a
// buffer about to go out of scope is dead-store eliminated, which would
// silently drop the erasure of key material — the exact guarantee libsodium
// depends on. This implementation uses volatile to prevent optimization.
//
// A previous from-source build masked this issue: a current GCC happens to
// provide the symbol itself, so the reference resolved by accident. Now that
// we use the prebuilt archive, this compatibility shim is mandatory.

#include <stddef.h>

void* memset_explicit(void* destination, int value, size_t count) {
    volatile unsigned char* out = (volatile unsigned char*)destination;
    while (count-- != 0) *out++ = (unsigned char)value;
    return destination;
}
