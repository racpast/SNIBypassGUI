#!/usr/bin/env python3
r"""Make compile_commands.json usable by clang-tidy for this GCC/MinGW build.

Copyright © 2026 Racpast. All Rights Reserved.

This file is part of SNIBypassGUI, a proprietary software project.

NOTICE: All information contained herein is, and remains the property of
Racpast. The intellectual and technical concepts contained herein are
proprietary to Racpast and are protected by copyright law and international
treaties. Dissemination of this information or reproduction of this material
is strictly forbidden unless prior written permission is obtained from Racpast.

Unauthorized copying, modification, distribution, or use of this file,
via any medium, is strictly prohibited.

For licensing inquiries: snibypassgui@gmail.com or racpast@gmail.com

See the LICENSE.md file in the project root for full terms and conditions.

WHY THIS EXISTS
---------------
The build is GCC/MinGW (see CMakeLists.txt), and `clang-tidy` is what runs the
static analysis in CI (.clang-tidy). clang-tidy reads the flags out of
compile_commands.json, so it is handed GCC's flags — and some of them it does not
implement at all. It reports them as hard errors:

    error: unknown argument: '-fwide-exec-charset=UTF-16LE' [clang-diagnostic-error]

A run that cannot compile its input produces no analysis, so the gate would fail
for a reason that has nothing to do with the code.

This rewrites the database into a sibling directory with those flags removed, and
changes nothing else: the same includes, the same defines, the same standard, and
the same spelling of every path.

The commands are edited as text, in place: the flags below are cut out of the
string and nothing else about it is touched. Reaching for shlex to split the
command into words and re-quote them back would destroy the command on this
platform — POSIX word splitting treats `\` in `D:\a\...` as an escape, and POSIX
quoting wraps `-DSNIB_SOURCE_DIR=...` in single quotes, which clang-tidy (reading
the command with Windows rules) does not treat as quoting at all. clang-tidy then
cannot find any of the listed sources, compiles nothing, and the gate fails for a
reason that has nothing to do with the code.

Only the flags that are GCC-specific and irrelevant to an AST-level analysis are
dropped:

  * -finput-charset / -fexec-charset / -fwide-exec-charset
        source and literal encoding. clang has no equivalent spelling. These
        affect the bytes clang emits, not the code it sees, so dropping them
        leaves the analysis intact — and .clang-format plus .gitattributes
        already pin the encodings those flags encode.

  * -municode
        selects MinGW's wmain/wWinMain entry handling. Link-time, not parse-time.

  * -fno-ident, -ffile-prefix-map, -fmacro-prefix-map
        the privacy flags. They rewrite strings; they do not change the AST.

  * -specs=...
        the generated spec that drops GCC's default-manifest.o. Link-time only.

Nothing here is a permanent part of the build: compile_commands.json is
regenerated on every configure, so this writes a separate directory and leaves
the original alone.

USAGE
-----
    python tools/clangdb.py --build build
    clang-tidy -p build/clang-db src/**/*.cpp
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys

# Flags clang does not implement, matched by prefix where they take an argument.
# Anything with a value is matched as "flag=value" or "flag value"; the build uses
# the "=" form for all of these.
_DROP_PREFIXES = (
    "-finput-charset=",
    "-fexec-charset=",
    "-fwide-exec-charset=",
    "-fno-ident",
    "-ffile-prefix-map=",
    "-fmacro-prefix-map=",
    "-municode",
    "-mwindows",
    "-specs=",
    "-Wl,--gc-sections",
)

# Each prefix above as a pattern that matches one whole token, without the
# whitespace before it — the caller supplies that boundary, so it is written once.
# Matching the whole token is what keeps "-mwindows" from eating "-mwindows-extra",
# a flag that merely starts with the same letters.
#
# An "=" argument runs to whitespace, quoted or not. The quoted branch handles a
# backslash-escaped character inside the quotes, so a value like "D:/a\"b" is
# consumed whole rather than being cut short at the escaped quote.
_QUOTED = r'"(?:\\.|[^"\\])*"'
_DROP_PATTERNS = tuple(
    re.escape(prefix) + r'(?:' + _QUOTED + r'|\S*)'
    if prefix.endswith("=") else
    re.escape(prefix) + r'(?:\s*=\S*)?(?=\s|$)'
    for prefix in _DROP_PREFIXES
)

# Flags of the form "flag value": the flag and the word after it both go. None of
# the flags in _DROP_PREFIXES is used in this separated form by this build, but a
# caller's CMake could introduce one, and silently leaving a bare argument behind
# would corrupt the command line in a way that shows up as a confusing clang error.
_SEPARATED = ("-specs",)
_SEPARATED_PATTERNS = tuple(
    re.escape(flag) + r'\s+(?:' + _QUOTED + r'|\S+)' for flag in _SEPARATED
)


def adjust(command: str) -> tuple[str, int]:
    """Return `command` with the GCC-only flags removed, and how many went.

    A textual edit, deliberately: word-splitting and re-quoting the command would
    mangle the Windows paths in it (see the module docstring).
    """
    dropped = 0
    for pattern in _SEPARATED_PATTERNS + _DROP_PATTERNS:
        # The boundary is part of the match, which is why the patterns above carry
        # no leading \s*: "|^" is what lets a flag in first position match too.
        command, count = re.subn(r'(?:^|\s)' + pattern, " ", command)
        dropped += count
    # Dropping a flag leaves the whitespace that framed it behind as a run; a flag at
    # the very start leaves a leading space. Collapse both so the rewritten command
    # stays readable.
    return re.sub(r"[ \t]{2,}", " ", command).strip(), dropped


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--build", default="build",
                        help="build directory holding compile_commands.json")
    parser.add_argument("--out", default=None,
                        help="output directory (default: <build>/clang-db)")
    parser.add_argument("--quiet", action="store_true")
    args = parser.parse_args()

    source = os.path.join(args.build, "compile_commands.json")
    if not os.path.isfile(source):
        sys.stderr.write(
            f"no compilation database at {source}\n"
            "Configure with -DCMAKE_EXPORT_COMPILE_COMMANDS=ON (the build does this "
            "by default; see CMakeLists.txt).\n")
        return 1

    with open(source, encoding="utf-8") as handle:
        database = json.load(handle)

    out_dir = args.out or os.path.join(args.build, "clang-db")
    os.makedirs(out_dir, exist_ok=True)

    rewritten = 0
    for entry in database:
        command = entry.get("command")
        if not command:
            # An entry expressed as an argument vector would need the same
            # treatment applied to the list; this build never produces one.
            continue
        adjusted, dropped = adjust(command)
        if dropped:
            entry["command"] = adjusted
            rewritten += 1
        # Point clang at the directory it was invoked from, so a relative include
        # or a generated header resolves the same way it did during the build.
        entry.setdefault("directory", os.path.abspath(args.build))

    destination = os.path.join(out_dir, "compile_commands.json")
    with open(destination, "w", encoding="utf-8") as handle:
        json.dump(database, handle, indent=1)

    if not args.quiet:
        print(f"clangdb: {len(database)} entries, {rewritten} adjusted -> {destination}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
