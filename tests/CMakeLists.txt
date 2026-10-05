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

# snib_core reaches into the updater's work-order format, and those objects travel with
# it — they are named in snib_core's own source list, so nothing has to be added here.
# This target used to list them itself, because snib_core did not carry them and every
# Updater:: symbol was left unresolved; the fix belongs in one place, and that place is
# snib_core.
add_executable(snib_tests
    test_core.cpp
    $<TARGET_OBJECTS:snib_core>)

# Carries the include directories (src/) the test sources need, and the sodium and
# generated-header paths, without naming snib_core's objects a second time.
target_link_libraries(snib_tests PRIVATE snib_core)

# Same warning, charset and privacy semantics as the program under test.
#
# The privacy flags in particular used to be missing here, so the suite compiled under
# different semantics from what ships. snib_gui_link_options is deliberately NOT applied:
# this is a console program and takes no -municode, no -mwindows and no manifest spec.
target_link_libraries(snib_tests PRIVATE snib_compile_options)

target_compile_definitions(snib_tests PRIVATE
    "SNIB_SOURCE_DIR=L\"${CMAKE_SOURCE_DIR}\"")

target_link_options(snib_tests PRIVATE -static -static-libgcc -static-libstdc++)

target_link_libraries(snib_tests PRIVATE
    ws2_32 winhttp bcrypt crypt32 iphlpapi shell32 ole32 oleaut32 taskschd advapi32 secur32
    snib_sodium)

add_test(NAME core_tests COMMAND snib_tests)
