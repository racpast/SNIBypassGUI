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
// See the LICENSE.md file in the project root for full license terms.

#include "updater/plan.h"

#include <windows.h>

#include <aclapi.h>
#include <bcrypt.h>
#include <sddl.h>

#include <string>
#include <unordered_map>

namespace Updater {
namespace {

// The DACL on the plan directory and every plan file: a protected DACL (no inherited
// entries) granting full control to SYSTEM and to the Administrators group, and
// nothing to anyone else.
//
// This is the whole point of the module. The previous design wrote its script into
// %TEMP%, which the user can write to, so a lower-integrity process could plant a file
// or a directory junction at the script's fixed path and have it executed by the
// elevated process that ran the script. Here the directory cannot be written to by a
// non-administrator, and the file is created fresh under a random name, so there is no
// path to plant and no name to predict.
constexpr wchar_t kPlanDirSddl[] = L"D:P(A;OICI;GA;;;SY)(A;OICI;GA;;;BA)";

// The subdirectory under %ProgramData%. Machine-wide and administrator-only, which is
// what distinguishes it from %TEMP% and from anything under the user profile.
constexpr wchar_t kPlanDirName[] = L"SNIBypassGUI";

// ---- Narrow UTF-8 helpers ---------------------------------------------------
//
// Deliberately local. The work order is the only text this module handles, and the
// updater links none of the application — app/text.cpp would drag in the whole core
// library for two conversions.

std::string ToUtf8(const std::wstring& w) {
    if (w.empty()) return {};
    const int n = WideCharToMultiByte(CP_UTF8, 0, w.data(), static_cast<int>(w.size()), nullptr,
                                      0, nullptr, nullptr);
    if (n <= 0) return {};
    std::string out(static_cast<size_t>(n), '\0');
    WideCharToMultiByte(CP_UTF8, 0, w.data(), static_cast<int>(w.size()), out.data(), n,
                        nullptr, nullptr);
    return out;
}

std::wstring FromUtf8(const std::string& s) {
    if (s.empty()) return {};
    const int n = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, s.data(),
                                      static_cast<int>(s.size()), nullptr, 0);
    if (n <= 0) return {};
    std::wstring out(static_cast<size_t>(n), L'\0');
    MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, s.data(), static_cast<int>(s.size()),
                        out.data(), n);
    return out;
}

// A SECURITY_ATTRIBUTES holding the DACL above, for directory and file creation.
// False when the descriptor cannot be built, in which case the caller must not create
// anything.
bool BuildPlanSecurityAttributes(SECURITY_ATTRIBUTES& sa) {
    PSECURITY_DESCRIPTOR descriptor = nullptr;
    if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(kPlanDirSddl, SDDL_REVISION_1,
                                                              &descriptor, nullptr))
        return false;

    sa = {};
    sa.nLength = sizeof(sa);
    // Ownership is transferred to the process: the descriptor is used by every
    // creation call, and freeing it after the first would leave the rest dangling. The
    // leak is bounded by the number of plans this process writes, and is the documented
    // usage for a SECURITY_ATTRIBUTES whose lifetime is the operation.
    sa.lpSecurityDescriptor = descriptor;
    sa.bInheritHandle = FALSE;
    return true;
}

// True when the attribute bits describe a link rather than the object it resolves to.
// A reparse point (symlink, junction, mount point) is a redirection placed by someone
// else, so it is never something this module created and never something it will open.
bool IsReparsePoint(DWORD attributes) {
    return (attributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0;
}

// Append one "<key>=<value>" line, refusing a value that would break the format. A
// newline, carriage return or NUL cannot appear in a Win32 path, so this is a
// round-trip consistency check, not a filter — but a plan whose fields do not survive
// the write must never reach the updater, which would otherwise act on a different path
// from the one the parent validated.
bool AppendField(std::string& out, const char* key, const std::wstring& value) {
    if (value.empty()) return false;
    for (wchar_t c : value)
        if (c == L'\n' || c == L'\r' || c == L'\0') return false;
    out += key;
    out += '=';
    out += ToUtf8(value);
    out += "\r\n";
    return true;
}

bool AppendFlag(std::string& out, const char* key, bool value) {
    out += key;
    out += '=';
    out += (value ? '1' : '0');
    out += "\r\n";
    return true;
}

bool AppendUInt(std::string& out, const char* key, unsigned long value) {
    out += key;
    out += '=';
    out += std::to_string(value);
    out += "\r\n";
    return true;
}

}  // namespace

std::wstring RandomPlanFileName() {
    // 128 bits from the OS CSPRNG, hex-encoded. A predictable name is the one thing
    // that would let a plan be anticipated, so this is drawn fresh on every call.
    static const wchar_t kHex[] = L"0123456789abcdef";

    unsigned char bytes[16] = {};
    if (BCryptGenRandom(nullptr, bytes, sizeof(bytes), BCRYPT_USE_SYSTEM_PREFERRED_RNG) != 0) {
        // Not a supported configuration. Refusing to update would be a visible failure,
        // and a predictable name inside an administrator-only directory is still not a
        // hole, so this degrades instead of aborting.
        LARGE_INTEGER ticks = {};
        QueryPerformanceCounter(&ticks);
        ULONGLONG mixed = static_cast<ULONGLONG>(ticks.QuadPart) ^
                          (static_cast<ULONGLONG>(GetCurrentProcessId()) << 32u) ^
                          static_cast<ULONGLONG>(GetTickCount64());
        for (unsigned char& b : bytes) {
            mixed ^= mixed >> 12u;
            mixed ^= mixed << 25u;
            mixed ^= mixed >> 27u;
            b = static_cast<unsigned char>((mixed * 2685821657736338717ULL) >> 24u);
        }
    }

    std::wstring name = L"plan-";
    name.reserve(5 + sizeof(bytes) * 2 + 4);
    for (unsigned char b : bytes) {
        name.push_back(kHex[b >> 4u]);
        name.push_back(kHex[b & 0xFu]);
    }
    name += L".tmp";
    return name;
}

std::wstring PlanDirectory() {
    // %ProgramData% is the documented location and is what an elevated process sees;
    // it is machine-wide, exists on every supported system, and is writable only by
    // administrators and SYSTEM. Nothing here falls back to a per-user directory: a
    // user-writable location is exactly what made the old script plantable.
    std::wstring root;
    {
        wchar_t buf[MAX_PATH + 1] = {};
        const DWORD n =
            GetEnvironmentVariableW(L"ProgramData", buf, static_cast<DWORD>(std::size(buf)));
        if (n > 0 && n < std::size(buf)) root.assign(buf, n);
    }
    if (root.empty()) return {};

    std::wstring dir = root;
    if (dir.back() != L'\\') dir.push_back(L'\\');
    dir += kPlanDirName;

    const DWORD attrs = GetFileAttributesW(dir.c_str());
    if (attrs == INVALID_FILE_ATTRIBUTES) {
        SECURITY_ATTRIBUTES sa = {};
        if (!BuildPlanSecurityAttributes(sa)) return {};
        if (!CreateDirectoryW(dir.c_str(), &sa)) {
            // A concurrent creator winning the race leaves the directory in place with
            // the same DACL, which is the outcome we wanted.
            if (GetLastError() != ERROR_ALREADY_EXISTS) return {};
        }
    } else if (!(attrs & FILE_ATTRIBUTE_DIRECTORY) || IsReparsePoint(attrs)) {
        // Something is sitting at our path that is not a plain directory. Do not use
        // it: a junction here would redirect every plan into a location of someone
        // else's choosing.
        return {};
    }

    // Re-applying the DACL on every call also repairs a directory whose security was
    // changed after it was created, which is the only way this stays enforceable.
    PSECURITY_DESCRIPTOR descriptor = nullptr;
    if (ConvertStringSecurityDescriptorToSecurityDescriptorW(kPlanDirSddl, SDDL_REVISION_1,
                                                             &descriptor, nullptr)) {
        BOOL present = FALSE;
        BOOL defaulted = FALSE;
        PACL dacl = nullptr;
        if (GetSecurityDescriptorDacl(descriptor, &present, &dacl, &defaulted) && present) {
            SetNamedSecurityInfoW(
                const_cast<wchar_t*>(dir.c_str()), SE_FILE_OBJECT,
                DACL_SECURITY_INFORMATION | PROTECTED_DACL_SECURITY_INFORMATION, nullptr,
                nullptr, dacl, nullptr);
        }
        LocalFree(descriptor);
    }

    return dir;
}

bool WritePlan(const std::wstring& fullPath, const Plan& plan) {
    if (fullPath.empty()) return false;

    std::string body;
    body += "format=1\r\n";
    body += "op=";
    body += (plan.op == Op::Replace ? "replace" : "remove");
    body += "\r\n";

    if (!AppendField(body, "target", plan.target)) return false;
    if (plan.op == Op::Replace) {
        if (!AppendField(body, "new", plan.newFile)) return false;
        if (!AppendField(body, "backup", plan.backup)) return false;
    } else {
        if (!AppendField(body, "dir", plan.dir)) return false;
    }
    if (!AppendUInt(body, "pid", plan.parentPid)) return false;
    if (!AppendFlag(body, "autostart", plan.autostart)) return false;

    if (body.size() > kMaxPlanBytes) return false;

    SECURITY_ATTRIBUTES sa = {};
    if (!BuildPlanSecurityAttributes(sa)) return false;

    // CREATE_NEW, not CREATE_ALWAYS. The difference is the fix: CREATE_ALWAYS follows a
    // reparse point and truncates whatever it resolves to, so a junction planted at the
    // target would turn this write into an attacker-chosen file overwrite. CREATE_NEW
    // fails outright when anything already exists there.
    HANDLE file = CreateFileW(fullPath.c_str(), GENERIC_WRITE, 0, &sa, CREATE_NEW,
                              FILE_ATTRIBUTE_NORMAL, nullptr);
    if (file == INVALID_HANDLE_VALUE) return false;

    DWORD written = 0;
    const bool ok =
        WriteFile(file, body.data(), static_cast<DWORD>(body.size()), &written, nullptr) &&
        written == body.size();
    if (ok) FlushFileBuffers(file);
    CloseHandle(file);

    if (!ok) DeleteFileW(fullPath.c_str());
    return ok;
}

bool ReadPlan(const std::wstring& fullPath, Plan& plan, std::wstring& why) {
    plan = {};
    why.clear();

    // GetFileAttributesW does not follow a link, so a reparse point is reported as the
    // link it is and rejected here rather than opened.
    const DWORD attrs = GetFileAttributesW(fullPath.c_str());
    if (attrs == INVALID_FILE_ATTRIBUTES) {
        why = L"the plan file does not exist";
        return false;
    }
    if (IsReparsePoint(attrs)) {
        why = L"the plan file is a reparse point";
        return false;
    }
    if (attrs & FILE_ATTRIBUTE_DIRECTORY) {
        why = L"the plan file is a directory";
        return false;
    }

    HANDLE file = CreateFileW(fullPath.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr,
                              OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (file == INVALID_HANDLE_VALUE) {
        why = L"the plan file could not be opened";
        return false;
    }

    LARGE_INTEGER size = {};
    if (!GetFileSizeEx(file, &size) || size.QuadPart <= 0 || size.QuadPart > kMaxPlanBytes) {
        CloseHandle(file);
        why = L"the plan file has an implausible size";
        return false;
    }

    std::string raw(static_cast<size_t>(size.QuadPart), '\0');
    DWORD read = 0;
    const bool readOk =
        ReadFile(file, raw.data(), static_cast<DWORD>(raw.size()), &read, nullptr) != 0 &&
        read == raw.size();
    CloseHandle(file);
    if (!readOk) {
        why = L"the plan file could not be read";
        return false;
    }

    // Strict line format. Every line is key=value; blank lines are skipped, and a key
    // that is not one of the documented fields is refused rather than ignored, so a
    // plan that does not match the documented shape is rejected instead of being
    // interpreted as far as it happens to parse.
    std::unordered_map<std::string, std::string> fields;
    size_t pos = 0;
    while (pos <= raw.size()) {
        size_t end = raw.find('\n', pos);
        if (end == std::string::npos) end = raw.size();
        std::string line = raw.substr(pos, end - pos);
        if (!line.empty() && line.back() == '\r') line.pop_back();
        pos = end + 1;
        if (line.empty()) continue;

        const size_t eq = line.find('=');
        if (eq == std::string::npos || eq == 0) {
            why = L"a plan line is not key=value";
            return false;
        }
        const std::string key = line.substr(0, eq);
        // Refused here rather than after the known fields are extracted. Reading the
        // keys we recognise and ignoring the rest is exactly the "as far as it parses"
        // behaviour this format exists to avoid: a plan carrying a field this build
        // does not understand was written by a version that meant something by it, and
        // acting on the subset would be acting on a different request than the one
        // that was written.
        //
        // The list is the set of keys WritePlan can produce, kept adjacent to it so
        // the two are read together.
        static const char* const kKnownKeys[] = {"format", "op",  "target", "new",
                                                 "backup", "dir", "pid",    "autostart"};
        bool known = false;
        for (const char* candidate : kKnownKeys)
            if (key == candidate) known = true;
        if (!known) {
            why = L"the plan contains an unknown key";
            return false;
        }
        if (!fields.emplace(key, line.substr(eq + 1)).second) {
            why = L"the plan repeats a key";
            return false;
        }
    }

    const auto get = [&fields](const char* key) -> const std::string* {
        const auto it = fields.find(key);
        return it == fields.end() ? nullptr : &it->second;
    };

    const std::string* format = get("format");
    if (!format || *format != "1") {
        why = L"the plan format is missing or unsupported";
        return false;
    }

    const std::string* op = get("op");
    if (!op) {
        why = L"the plan has no op";
        return false;
    }
    if (*op == "replace") {
        plan.op = Op::Replace;
    } else if (*op == "remove") {
        plan.op = Op::Remove;
    } else {
        why = L"the plan op is not recognized";
        return false;
    }

    // Every path the updater will act on has to have been named explicitly; there is no
    // default for a missing one.
    const std::string* target = get("target");
    if (!target || target->empty()) {
        why = L"the plan has no target";
        return false;
    }
    plan.target = FromUtf8(*target);
    if (plan.target.empty()) {
        why = L"the plan target is not valid UTF-8";
        return false;
    }

    if (plan.op == Op::Replace) {
        const std::string* fresh = get("new");
        const std::string* backup = get("backup");
        if (!fresh || fresh->empty() || !backup || backup->empty()) {
            why = L"the plan is missing the staged or backup path";
            return false;
        }
        plan.newFile = FromUtf8(*fresh);
        plan.backup = FromUtf8(*backup);
        if (plan.newFile.empty() || plan.backup.empty()) {
            why = L"a plan path is not valid UTF-8";
            return false;
        }
    } else {
        const std::string* dir = get("dir");
        if (!dir || dir->empty()) {
            why = L"the remove plan has no directory";
            return false;
        }
        plan.dir = FromUtf8(*dir);
        if (plan.dir.empty()) {
            why = L"the plan directory is not valid UTF-8";
            return false;
        }
    }

    const std::string* pid = get("pid");
    if (!pid || pid->empty()) {
        why = L"the plan has no parent pid";
        return false;
    }
    {
        char* stop = nullptr;
        const unsigned long parsed = strtoul(pid->c_str(), &stop, 10);
        if (!stop || *stop != '\0' || parsed == 0) {
            why = L"the plan parent pid is not a number";
            return false;
        }
        plan.parentPid = static_cast<DWORD>(parsed);
    }

    const std::string* autostart = get("autostart");
    if (!autostart) {
        why = L"the plan has no autostart flag";
        return false;
    }
    if (*autostart == "1") {
        plan.autostart = true;
    } else if (*autostart == "0") {
        plan.autostart = false;
    } else {
        why = L"the plan autostart flag is neither 0 nor 1";
        return false;
    }

    return true;
}

void DeletePlanFile(const std::wstring& fullPath) {
    if (!fullPath.empty()) DeleteFileW(fullPath.c_str());
}

}  // namespace Updater
