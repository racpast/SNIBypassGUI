# Copyright © 2026 Racpast. All Rights Reserved.
#
# This file is part of SNIBypassGUI, a proprietary software project.
#
# NOTICE: All information contained herein is, and remains the property of
# Racpast. The intellectual and technical concepts contained herein are
# proprietary to Racpast and are protected by copyright law and international
# treaties. Dissemination of this information or reproduction of this material
# is strictly forbidden unless prior written permission is obtained from Racpast.
#
# Unauthorized copying, modification, distribution, or use of this file,
# via any medium, is strictly prohibited.
#
# For licensing inquiries: snibypassgui@gmail.com or racpast@gmail.com
#
# See the LICENSE.md file in the project root for full terms and conditions.

# 3.25 is the real floor: DOWNLOAD_EXTRACT_TIMESTAMP below is 3.24, and
# CMakePresets.json declares schema version 6, which is 3.25. Declaring 3.21 let a
# build on an older CMake fail with an error about a FetchContent argument rather
# than about the version it was told it needed.
cmake_minimum_required(VERSION 3.25)

# ---------------------------------------------------------------------------
# Version — single source of truth is APP_VERSION_NUM in src/app/version.h.
#
# Read before project(), so that even CMake's own PROJECT_VERSION comes from
# there. Every other copy of the version in this build — the native FILEVERSION
# resource, the assembly manifest — is generated from these variables, so a bump
# is one edit in one file and nothing can be left behind at the old number.
# ---------------------------------------------------------------------------
file(STRINGS "${CMAKE_CURRENT_LIST_DIR}/src/app/version.h" _ver_line
     REGEX "^[ \t]*#define[ \t]+APP_VERSION_NUM[ \t]+L\"")
if(NOT _ver_line)
    message(FATAL_ERROR "APP_VERSION_NUM not found in src/app/version.h")
endif()
string(REGEX REPLACE ".*L\"([^\"]*)\".*" "\\1" SNIB_VERSION "${_ver_line}")

string(REPLACE "." ";" _ver_parts "${SNIB_VERSION}")
list(LENGTH _ver_parts _ver_len)
set(_ver_names A B C D)
foreach(_i RANGE 3)
    if(_i LESS _ver_len)
        list(GET _ver_parts ${_i} _part)
        string(REGEX REPLACE "[^0-9]" "" _part "${_part}")
        if(_part STREQUAL "")
            set(_part 0)
        endif()
    else()
        set(_part 0)
    endif()
    list(GET _ver_names ${_i} _name)
    set(SNIB_VER_${_name} ${_part})
endforeach()
set(SNIB_VER_NUMERIC "${SNIB_VER_A}.${SNIB_VER_B}.${SNIB_VER_C}.${SNIB_VER_D}")

project(SNIBypassGUI VERSION ${SNIB_VER_NUMERIC} LANGUAGES C CXX)
message(STATUS "SNIBypassGUI version: ${SNIB_VERSION} (numeric ${SNIB_VER_NUMERIC})")

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_C_STANDARD 11)

option(SNIB_BUILD_TESTS "Build the unit test suite" OFF)

# ---------------------------------------------------------------------------
# libsodium — official MinGW prebuilt distribution.
#
# Downloaded, never vendored: the archive is not committed, because a binary in the
# repository would hide which build it came from, record no ABI, and could not be
# reviewed. Upstream publishes a MinGW tarball alongside its Autotools sources, so
# the pinned URL plus SHA-256 below is the whole dependency — no compiler run, and
# therefore no Autotools, no `make`, and no MSYS2.
#
# The previous flow built libsodium from source through build-mingw64.sh, which
# needed cygpath and GNU make. That made an MSYS2 installation a hard requirement at
# a point where nothing else in this build wants one, and it failed on any machine
# without it long before anything could be compiled.
#
# Reproducibility note: the tarball name carries "-stable", and libsodium has been
# known to re-upload a name like that in place. The SHA-256 is what actually pins
# the bytes; if upstream ever reissues the file, this hash stops matching and the
# download fails loudly rather than silently pulling a different library. To move
# to a new release, change both the version and the hash together.
# ---------------------------------------------------------------------------
set(SNIB_SODIUM_VERSION "1.0.22")
set(SNIB_SODIUM_MINGW_SHA256
    "2dbb9fdb0882ec44a328b1f6a8fccbfa0bb0d0f5b1034f135e15827acaf6dba7")

include(FetchContent)
FetchContent_Declare(libsodium_mingw
    URL "https://download.libsodium.org/libsodium/releases/libsodium-${SNIB_SODIUM_VERSION}-stable-mingw.tar.gz"
    URL_HASH "SHA256=${SNIB_SODIUM_MINGW_SHA256}"
    DOWNLOAD_EXTRACT_TIMESTAMP TRUE)
FetchContent_MakeAvailable(libsodium_mingw)

# The tarball is a multi-architecture drop: it carries win32 and win64 side by side.
# This project only ever targets x86_64, so only that half is referenced.
set(SNIB_SODIUM_DIST_DIR "${libsodium_mingw_SOURCE_DIR}/libsodium-win64")
set(SNIB_SODIUM_INCLUDE_DIR "${SNIB_SODIUM_DIST_DIR}/include")

add_library(snib_sodium STATIC IMPORTED GLOBAL)
set_target_properties(snib_sodium PROPERTIES
    IMPORTED_LOCATION "${SNIB_SODIUM_DIST_DIR}/lib/libsodium.a"
    INTERFACE_INCLUDE_DIRECTORIES "${SNIB_SODIUM_INCLUDE_DIR}")

# Both directories exist as soon as population has run, so unlike an ExternalProject
# there is nothing to pre-create for the imported target's include path, and no
# add_dependencies() to wire up — MakeAvailable is complete before this point.

# What clangd and clang-tidy read to learn each translation unit's flags. Without
# it an editor falls back to guessing them, and every Windows header resolves to
# nothing — the diagnostics that follow are about the guess, not the code.
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

# ---------------------------------------------------------------------------
# Windows only.
# ---------------------------------------------------------------------------
if(NOT WIN32)
    message(FATAL_ERROR "SNIBypassGUI targets Windows. Configure with a MinGW-w64 toolchain.")
endif()

# ---------------------------------------------------------------------------
# Compile definitions applied to every target.
# ---------------------------------------------------------------------------
add_compile_definitions(
    UNICODE
    _UNICODE
    WIN32_LEAN_AND_MEAN
    _WIN32_WINNT=0x0601
    SODIUM_STATIC
    # The bound on select()'s descriptor array, for both DNS servers.
    #
    # It has to be defined before <winsock.h> is read, because that header fixes
    # FD_SETSIZE's default at 64 and <winsock2.h> guards its own definition so it
    # does not argue. <windows.h> pulls <winsock.h> in, so a source file that
    # includes windows.h before any of this project's headers would lock in 64 —
    # silently, with the only symptom being a static_assert about socket budgets
    # failing somewhere else entirely.
    #
    # A compile definition rather than a header include is what removes that
    # ordering hazard: it is applied before the first line of every translation
    # unit, so there is no include order to get wrong. Keeping it here also means
    # the value cannot drift between the two servers — see the static_asserts in
    # dns/resolver.cpp and dns/dns_proxy.cpp, which tie their own connection
    # limits to it.
    #
    # It is a hard cap, not a hint: FD_SET past it corrupts the stack.
    FD_SETSIZE=128)

set(SNIB_WARNING_FLAGS -Wall -Wextra -Werror)

set(SNIB_CHARSET_FLAGS
    -finput-charset=UTF-8 -fexec-charset=UTF-8 -fwide-exec-charset=UTF-16LE)

set(SNIB_PRIVACY_FLAGS
    -fno-ident
    "-ffile-prefix-map=${CMAKE_SOURCE_DIR}=."
    "-fmacro-prefix-map=${CMAKE_SOURCE_DIR}=.")

# ---------------------------------------------------------------------------
# The build's shared compile and link semantics, declared exactly once.
#
# Every target used to carry its own verbatim copy of the same five-line
# target_compile_options block, and the two executables their own copy of the same
# link block. That meant adding one warning flag was a five-place edit with no
# mechanism stopping a target from being missed — and one WAS missed: the test
# target never applied SNIB_PRIVACY_FLAGS, so it compiled under different
# semantics from the program it tests.
#
# As INTERFACE libraries these propagate through target_link_libraries, so a new
# target opts in with one line and cannot opt in partially.
# ---------------------------------------------------------------------------
add_library(snib_compile_options INTERFACE)
target_compile_options(snib_compile_options INTERFACE
    $<$<COMPILE_LANGUAGE:CXX>:-O2>
    -municode
    ${SNIB_WARNING_FLAGS}
    ${SNIB_CHARSET_FLAGS}
    ${SNIB_PRIVACY_FLAGS})

# Everything here has src/ on its include path, and nothing but the generated
# headers needs more than that.
target_include_directories(snib_compile_options INTERFACE "${CMAKE_SOURCE_DIR}/src")

# ---------------------------------------------------------------------------
# Resource script — generated into the build tree so src/ stays clean.
# ---------------------------------------------------------------------------
set(SNIB_GENERATED_DIR "${CMAKE_BINARY_DIR}/generated")
file(MAKE_DIRECTORY "${SNIB_GENERATED_DIR}")

set(SNIB_APP_RC "${SNIB_GENERATED_DIR}/app.rc")
configure_file("${CMAKE_SOURCE_DIR}/src/app.rc.in" "${SNIB_APP_RC}" @ONLY)

# The assembly manifest carries the version too, so it is generated from the same
# variables rather than kept in step by hand. It lands beside the generated app.rc,
# which is where that script's reference to it resolves from.
configure_file("${CMAKE_SOURCE_DIR}/src/app.manifest.in"
               "${SNIB_GENERATED_DIR}/app.manifest" @ONLY)

# The updater's version is its own — see src/updater/version.h for why — so it is read
# from that header and split into the four components a FILEVERSION needs, exactly as
# APP_VERSION_NUM is above.
file(STRINGS "${CMAKE_CURRENT_LIST_DIR}/src/updater/version.h" _upd_ver_line
     REGEX "^[ \t]*#define[ \t]+UPDATER_VERSION_NUM[ \t]+L\"")
if(NOT _upd_ver_line)
    message(FATAL_ERROR "UPDATER_VERSION_NUM not found in src/updater/version.h")
endif()
string(REGEX REPLACE ".*L\"([^\"]*)\".*" "\\1" SNIB_UPDATER_VERSION "${_upd_ver_line}")
string(REPLACE "." ";" _upd_parts "${SNIB_UPDATER_VERSION}")
list(LENGTH _upd_parts _upd_len)
foreach(_i RANGE 3)
    if(_i LESS _upd_len)
        list(GET _upd_parts ${_i} _part)
        string(REGEX REPLACE "[^0-9]" "" _part "${_part}")
        if(_part STREQUAL "")
            set(_part 0)
        endif()
    else()
        set(_part 0)
    endif()
    list(GET _ver_names ${_i} _name)
    set(UPDATER_VER_${_name} ${_part})
endforeach()
set(UPDATER_VER_NUMERIC "${UPDATER_VER_A}.${UPDATER_VER_B}.${UPDATER_VER_C}.${UPDATER_VER_D}")
message(STATUS "SNIBypassGUI updater version: ${SNIB_UPDATER_VERSION} (numeric ${UPDATER_VER_NUMERIC})")

configure_file("${CMAKE_SOURCE_DIR}/src/updater/updater.rc.in"
               "${SNIB_GENERATED_DIR}/updater.rc" @ONLY)

enable_language(RC)
set(CMAKE_RC_FLAGS "${CMAKE_RC_FLAGS} -I${SNIB_GENERATED_DIR} -I${CMAKE_SOURCE_DIR}/resources/branding -I${CMAKE_SOURCE_DIR}/src -I${CMAKE_SOURCE_DIR}/resources/embedded")

# Suppress GCC's automatic default-manifest.o so the one from our .rc is the only one.
# Both executables need this, so it is generated once, here, before either target.
include("${CMAKE_SOURCE_DIR}/cmake/NoDefaultManifest.cmake")
snib_generate_no_default_manifest_spec(SNIB_NO_DEFAULT_MANIFEST_SPEC)

# The two GUI executables link identically, so they share one declaration. It is
# defined here rather than beside snib_compile_options above because it names the
# generated spec file, which does not exist until the call above.
#
# The test runner is a console program and deliberately does NOT use this: it takes
# no -municode, no -mwindows and no manifest spec, and supplies its own -static
# flags.
add_library(snib_gui_link_options INTERFACE)
target_link_options(snib_gui_link_options INTERFACE
    -municode -mwindows
    -static -static-libgcc -static-libstdc++
    -s
    "-specs=${SNIB_NO_DEFAULT_MANIFEST_SPEC}"
    -Wl,--gc-sections)

# The work order's implementation, named once and compiled into whichever target
# needs it.
#
# This is a source list rather than an OBJECT library that others link. Two targets
# need these definitions and neither reaches the other through a link line: the
# updater executable, and snib_core — whose consumers take it as
# $<TARGET_OBJECTS:snib_core>, a generator expression that yields only what snib_core
# itself compiles. An object file pulled in by a generator expression is not part of
# that set, so a linked OBJECT library would leave every Updater:: symbol undefined
# in the application and the tests. Compiling this one small file per target is the
# price of both being self-contained.
set(SNIB_UPDATER_PROTOCOL_SOURCES src/updater/plan.cpp)

# ---------------------------------------------------------------------------
# Updater module: embedded, never distributed.
#
# Built as an ordinary executable so it can be run and tested on its own, then
# embedded into the application as an RCDATA resource and written out only at the
# moment it is needed. It is NOT installed and NOT listed in the update manifest.
#
# That is the point. A second .exe sitting beside the application in a green/portable
# zip is a loose file the user can see, the uninstaller has to know about, and the
# update channel has to carry. Embedding it removes all three: the zip stays one
# executable plus payload, the manifest has nothing new to describe, and the module
# travels inside the file that already carries the version it belongs to.
#
# It is also what makes the uninstall correct. The updater deletes the install tree it
# was started from, so it cannot live in that tree; running it from %ProgramData%
# instead means nothing pins the program directory and the non-recursive rmdir behind
# it can actually succeed.
#
# Kept as small as it can be: no payload, no configuration, no services, no libsodium,
# and no manifest (see src/updater/updater.rc.in for why it needs none). That is what
# lets it work while the main executable is being replaced or removed — precisely when
# it runs.
# ---------------------------------------------------------------------------
add_executable(Updater WIN32
    src/updater/main.cpp
    src/updater/updater.cpp
    ${SNIB_UPDATER_PROTOCOL_SOURCES}
    "${SNIB_GENERATED_DIR}/updater.rc")

target_include_directories(Updater PRIVATE
    "${SNIB_GENERATED_DIR}")

target_link_libraries(Updater PRIVATE snib_compile_options snib_gui_link_options)

# bcrypt: BCryptGenRandom, for the plan file name. advapi32:
# ConvertStringSecurityDescriptorToSecurityDescriptorW and SetNamedSecurityInfoW, for
# the plan directory's DACL. shell32: CommandLineToArgvW. There is no separate sddl
# library — the SDDL and security-descriptor APIs both live in advapi32.
target_link_libraries(Updater PRIVATE bcrypt advapi32 shell32 user32)

# The generated fragment that embeds the built module. app.rc includes this, and it is
# generated rather than written by hand because it names an absolute build-tree path —
# the one thing that cannot be known ahead of time.
file(GENERATE
    OUTPUT "${SNIB_GENERATED_DIR}/updater_embedded.rc"
    CONTENT "// GENERATED -- do not edit. See CMakeLists.txt.\n\
// The updater module, embedded as a resource.\n\
//\n\
// APP_RCDATA_UPDATER is the resource id, kept next to the EULA ids in\n\
// src/platform/embedded_text.h. It is compiled in unconditionally: a build is not a\n\
// variant of the application, and a missing module would be a broken install rather\n\
// than a supported configuration.\n\
APP_RCDATA_UPDATER RCDATA \"$<TARGET_FILE:Updater>\"\n")

# The app.rc object embeds the module, so it must be compiled after the module has been
# linked. A target-level dependency does that correctly, where a file-level one would
# have to name the module file directly and would not bring its own build along.
set_source_files_properties("${SNIB_APP_RC}" PROPERTIES OBJECT_DEPENDS
                            "${SNIB_GENERATED_DIR}/updater_embedded.rc")

# ---------------------------------------------------------------------------
# Shared object library: every translation unit except the entry point.
# ---------------------------------------------------------------------------
add_library(snib_core OBJECT
    src/app/bootstrap.cpp
    src/app/controller.cpp
    src/app/filesystem.cpp
    src/app/i18n.cpp
    src/app/logging.cpp
    src/app/paths.cpp
    src/app/services.cpp
    src/app/settings.cpp
    src/app/text.cpp
    src/app/version.cpp
    src/compat/memset_explicit.c
    src/dns/answer.cpp
    src/dns/cancel.cpp
    src/dns/dns_proxy.cpp
    src/dns/dns_proxy_config.cpp
    src/dns/dnscrypt_client.cpp
    src/dns/dnsstamp.cpp
    src/dns/doh_client.cpp
    src/dns/dot_client.cpp
    src/dns/file_watcher.cpp
    src/dns/http_response.cpp
    src/dns/message.cpp
    src/dns/network_utils.cpp
    src/dns/nrpt.cpp
    src/dns/plain_client.cpp
    src/dns/redirector.cpp
    src/dns/resolver.cpp
    src/dns/rules.cpp
    src/dns/socket_utils.cpp
    src/dns/tcp_session.cpp
    src/dns/tls_utils.cpp
    src/platform/autostart.cpp
    src/platform/command.cpp
    src/platform/dialogs.cpp
    src/platform/elevation.cpp
    src/platform/embedded_text.cpp
    src/platform/ini.cpp
    src/platform/ports.cpp
    src/platform/process.cpp
    src/platform/shell.cpp
    src/platform/shortcut.cpp
    src/update/client.cpp
    src/update/crypto.cpp
    src/update/http.cpp
    src/update/json.cpp
    src/update/progress.cpp
    src/updater/module.cpp
    # snib_core's sources call into the updater's work-order format (services.cpp
    # writes a plan, update/client.cpp starts the module, module.cpp resolves the plan
    # directory), so the format's implementation is compiled in here. It is a source
    # list and not a linked OBJECT library for the reason given where the list is
    # declared: a consumer reaches this library by generator expression, which does not
    # carry anything that arrived by linking.
    ${SNIB_UPDATER_PROTOCOL_SOURCES})

target_include_directories(snib_core PUBLIC
    "${CMAKE_SOURCE_DIR}/src"
    "${SNIB_SODIUM_INCLUDE_DIR}"
    "${SNIB_GENERATED_DIR}")

# snib_core's sources call into the updater's work-order format, whose definitions
# arrive as the protocol objects named in the source list above.
target_link_libraries(snib_core PRIVATE snib_compile_options)

# ---------------------------------------------------------------------------
# UI objects (only needed in the executable, not in the test runner).
# ---------------------------------------------------------------------------
add_library(snib_ui OBJECT
    src/ui/eula.cpp
    src/ui/progress_window.cpp
    src/ui/supported_sites.cpp
    src/ui/tray.cpp)

target_include_directories(snib_ui PUBLIC "${CMAKE_SOURCE_DIR}/src")
target_link_libraries(snib_ui PRIVATE snib_compile_options)

# ---------------------------------------------------------------------------
# Main executable.
# ---------------------------------------------------------------------------
add_executable(SNIBypassGUI WIN32
    src/main.cpp
    $<TARGET_OBJECTS:snib_core>
    $<TARGET_OBJECTS:snib_ui>
    "${SNIB_APP_RC}")

# The application embeds the updater module as a resource, so the module has to exist
# before the resource script is compiled.
add_dependencies(SNIBypassGUI Updater)

target_include_directories(SNIBypassGUI PRIVATE
    "${SNIB_GENERATED_DIR}")

# snib_compile_options and the GUI link flags come through here (including the spec
# that suppresses GCC's default-manifest.o, so ours from app.rc is the only one); the
# updater protocol objects arrive transitively, because snib_core links them.
target_link_libraries(SNIBypassGUI PRIVATE
    snib_compile_options
    snib_gui_link_options)

add_custom_command(TARGET SNIBypassGUI POST_BUILD
    COMMAND ${CMAKE_COMMAND}
            "-DEXE=$<TARGET_FILE:SNIBypassGUI>"
            -P "${CMAKE_SOURCE_DIR}/cmake/VerifyManifest.cmake"
    VERBATIM)

# taskschd/oleaut32: start-at-logon is registered through the Task Scheduler API,
# so a non-ASCII install path never has to survive a console code page. uxtheme:
# OpenThemeData/DrawThemeBackground for the update progress bar, so it is drawn with
# the same theme parts the shell's own copy dialog uses. dwmapi: DwmGetColorizationColor
# for the accent the window's heading glyph and focus ring take their colour from.
target_link_libraries(SNIBypassGUI PRIVATE
    iphlpapi ws2_32 winhttp bcrypt crypt32 shell32 ole32 oleaut32 taskschd advapi32 user32
    gdi32 comctl32 secur32 uxtheme dwmapi
    snib_sodium)

# ---------------------------------------------------------------------------
# Tests (opt-in).
# ---------------------------------------------------------------------------
if(SNIB_BUILD_TESTS)
    enable_testing()
    add_subdirectory(tests)
endif()

# ---------------------------------------------------------------------------
# Install + dist.
# ---------------------------------------------------------------------------
# ONLY the application is installed. The updater module is embedded inside it, so it is
# deliberately absent here: a second executable in the archive root is a file the user
# sees, the uninstaller has to track, and the update channel has to carry. It is also
# what would stop the uninstaller from removing the program directory at all, since a
# module that lives in the tree being deleted cannot be the thing that deletes it.
install(TARGETS SNIBypassGUI RUNTIME DESTINATION .)
install(DIRECTORY "${CMAKE_SOURCE_DIR}/resources/payload/"
        DESTINATION .
        USE_SOURCE_PERMISSIONS)

add_custom_target(dist
    COMMAND ${CMAKE_COMMAND} -E rm -rf "${CMAKE_SOURCE_DIR}/dist"
    COMMAND ${CMAKE_COMMAND} --install "${CMAKE_BINARY_DIR}" --prefix "${CMAKE_SOURCE_DIR}/dist"
    DEPENDS SNIBypassGUI
    COMMENT "Staging exe + payload into dist/")
