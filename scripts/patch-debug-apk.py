#!/usr/bin/env python3
"""Patch a Godot Android debug APK with a rebuilt MatchThree.dll.

This exists because a Godot binary is not always available (and because the
headless self-tests can be driven on-device this way), while the matching
assets of an existing debug export are:

  * the managed assembly is replaced (assets/.godot/mono/publish/arm64/),
  * the sparse PCK entry for it is updated (Godot 4.7 exports record each
    file's size + md5 in assets/assets.sparsepck and slice the APK asset to
    the recorded size — a bigger dll is otherwise truncated and the runtime
    reports ".NET: Failed to open assembly image"),
  * optional extra command-line args are appended to assets/_cl_ (the export
    preset's `command_line/extra_args`; used to run --selftest-* on a device,
    where launch-intent extras are ignored because the activity is not
    exported),
  * the APK is re-aligned for 16 KB page devices (Android 16 / targetSdk 36
    requires native libs to be 16 KB aligned; plain `zipalign -p 4` only does
    4 KB and triggers the "app doesn't support 16 KB pages" dialog) and
    signed with Godot's debug keystore so it updates the installed app.

Usage:
  scripts/patch-debug-apk.py BASE_APK MatchThree.dll OUT_APK
      [--extra-arg FLAG]... [--install [--serial SERIAL]]

Environment overrides: ANDROID_BUILD_TOOLS (dir with zipalign/apksigner),
GODOT_DEBUG_KEYSTORE (defaults to ~/.local/share/godot/keystores/debug.keystore),
ADB (defaults to adb on PATH).
"""
import argparse
import hashlib
import os
import shutil
import struct
import subprocess
import sys
import tempfile

DLL_REL = "assets/.godot/mono/publish/arm64/MatchThree.dll"
PCK_REL = "assets/assets.sparsepck"
CL_REL = "assets/_cl_"
MANIFEST_REL = "AndroidManifest.xml"
KEYSTORE_PASS = "pass:android"
KEYSTORE_ALIAS = "androiddebugkey"
ATTR_DEBUGGABLE = 0x0101000F
ATTR_TYPE_INT_BOOLEAN = 0x12


def die(message: str) -> "NoReturn":  # noqa: F821 - simple CLI error helper
    print(f"error: {message}", file=sys.stderr)
    sys.exit(1)


def sdk_tool(name: str) -> str:
    tools_dir = os.environ.get("ANDROID_BUILD_TOOLS")
    if tools_dir:
        return os.path.join(tools_dir, name)
    sdk = os.environ.get("ANDROID_HOME") or os.path.expanduser("~/Android/Sdk")
    build_tools = os.path.join(sdk, "build-tools")
    if os.path.isdir(build_tools):
        # Newest build-tools wins: zipalign -P (16 KB page alignment) needs 35+.
        versions = sorted(os.listdir(build_tools), key=lambda v: [int(p) for p in v.split(".") if p.isdigit()])
        for version in reversed(versions):
            candidate = os.path.join(build_tools, version, name)
            if os.path.exists(candidate):
                return candidate
    found = shutil.which(name)
    if found:
        return found
    die(f"{name} not found; set ANDROID_BUILD_TOOLS or install Android SDK build-tools")


def read_cl_args(path: str) -> list[str]:
    """assets/_cl_ is a [u32 count] followed by [u32 length][utf-8 bytes] records."""
    data = open(path, "rb").read()
    if len(data) < 4:
        die(f"{CL_REL} is malformed (too short)")
    count = struct.unpack_from("<I", data, 0)[0]
    args, off = [], 4
    for index in range(count):
        if off + 4 > len(data):
            die(f"{CL_REL} is malformed (truncated length at {off})")
        length = struct.unpack_from("<I", data, off)[0]
        off += 4
        if off + length > len(data):
            die(f"{CL_REL} is malformed (truncated value {index} at {off})")
        args.append(data[off:off + length].decode("utf-8"))
        off += length
    return args


def write_cl_args(path: str, args: list[str]) -> None:
    with open(path, "wb") as f:
        f.write(struct.pack("<I", len(args)))
        for arg in args:
            raw = arg.encode("utf-8")
            f.write(struct.pack("<I", len(raw)))
            f.write(raw)


def patch_sparse_pck(path: str, dll_size: int, dll_md5: bytes) -> None:
    data = bytearray(open(path, "rb").read())
    if data[:4] != b"GDPC":
        die(f"{PCK_REL} has no GDPC header")
    off, patched = 108, False  # file table start (pack format 4)
    while off < len(data):
        plen = struct.unpack_from("<I", data, off)[0]
        if plen <= 0 or plen > 300 or off + 4 + plen + 40 > len(data):
            break
        off += 4
        name = data[off:off + plen].rstrip(b"\x00").decode()
        off += plen
        value_offset = off
        _, recorded_size = struct.unpack_from("<QQ", data, off)
        off += 16
        if name == ".godot/mono/publish/arm64/MatchThree.dll":
            struct.pack_into("<Q", data, value_offset + 8, dll_size)
            data[off:off + 16] = dll_md5
            patched = True
            print(f"  pck: MatchThree.dll size {recorded_size} -> {dll_size}, md5 updated")
        off += 16
        off += 4
    if not patched:
        die("MatchThree.dll entry not found in the sparse pck")
    with open(path, "wb") as f:
        f.write(data)


def _axml_string_pool(data: bytes) -> list[str]:
    """Decode the manifest's string pool (enough to resolve tag names)."""
    if struct.unpack_from("<H", data, 8)[0] != 0x0001:
        return []
    string_count, _style_count, flags, strings_start, _styles_start = struct.unpack_from("<IIIII", data, 16)
    is_utf8 = (flags & 0x100) != 0
    offsets_base = 8 + 28  # pool chunk header is 28 bytes
    strings, base = [], 8 + strings_start
    for i in range(string_count):
        pos = base + struct.unpack_from("<I", data, offsets_base + i * 4)[0]
        if is_utf8:
            length = data[pos]
            pos += 1
            if length & 0x80:
                length = ((length & 0x7F) << 8) | data[pos]
                pos += 1
            strings.append(data[pos:pos + length].decode("utf-8", "replace"))
        else:
            length = struct.unpack_from("<H", data, pos)[0]
            pos += 2
            if length & 0x8000:
                length = ((length & 0x7FFF) << 16) | struct.unpack_from("<H", data, pos)[0]
                pos += 2
            strings.append(data[pos:pos + length * 2].decode("utf-16-le", "replace"))
    return strings


def _axml_resource_map(data: bytes) -> list[int]:
    """Resource IDs for the string pool entries (chunk type 0x0180)."""
    off = 8
    while off + 8 <= len(data):
        chunk_type, _header_size, chunk_size = struct.unpack_from("<HHI", data, off)
        if chunk_size <= 0:
            break
        if chunk_type == 0x0180:
            count = (chunk_size - 8) // 4
            return list(struct.unpack_from(f"<{count}I", data, off + 8))
        off += chunk_size
    return []


def patch_manifest_debuggable(path: str) -> None:
    """Flip android:debuggable to false in the binary AndroidManifest.xml.

    Debug APKs are debuggable, which keeps adb run-as / editor remote
    debugging working but is unwanted for hand-made builds. Before Android
    exports moved to the net9.0 TFM the flip also suppressed the Android 16
    16 KB page-size compatibility dialog (the older Mono runtime libs were
    only 4 KB aligned); exports are net9.0 now, so the native libs are 16 KB
    aligned and the dialog is gone at the source (docs/NOTES.md "16 KB page
    alignment").
    """
    data = bytearray(open(path, "rb").read())
    if struct.unpack_from("<H", data, 0)[0] != 0x0003:
        die(f"{MANIFEST_REL} is not a binary AXML manifest")
    pool = _axml_string_pool(data)
    resource_map = _axml_resource_map(data)
    off, patched = 8, 0
    while off + 8 <= len(data):
        chunk_type, header_size, chunk_size = struct.unpack_from("<HHI", data, off)
        if chunk_size <= 0:
            break
        if chunk_type == 0x0102:  # START_ELEMENT
            attr_start, _, attr_count = struct.unpack_from("<HHH", data, off + header_size + 8)
            tag_index = struct.unpack_from("<I", data, off + header_size + 4)[0]
            tag = pool[tag_index] if tag_index < len(pool) else "?"
            attrs = off + header_size + attr_start
            for i in range(attr_count):
                attr = attrs + i * 20
                if attr + 20 > len(data):
                    break
                name_index = struct.unpack_from("<I", data, attr + 4)[0]
                res_id = resource_map[name_index] if name_index < len(resource_map) else 0
                if res_id != ATTR_DEBUGGABLE:
                    continue
                if tag != "application":
                    continue
                old = struct.unpack_from("<I", data, attr + 16)[0]
                struct.pack_into("<I", data, attr + 8, 0xFFFFFFFF)  # rawValue: none
                struct.pack_into("<B", data, attr + 15, ATTR_TYPE_INT_BOOLEAN)
                struct.pack_into("<I", data, attr + 16, 0)  # false
                patched += 1
                print(f"  manifest: <{tag} android:debuggable> {bool(old)} -> False")
        off += chunk_size
    if not patched:
        die(f"android:debuggable not found in {MANIFEST_REL}")
    with open(path, "wb") as f:
        f.write(data)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("base_apk")
    parser.add_argument("dll")
    parser.add_argument("out_apk")
    parser.add_argument("--extra-arg", action="append", default=[], metavar="FLAG",
                        help="append a command-line arg to assets/_cl_ (repeatable)")
    parser.add_argument("--install", action="store_true", help="adb install the patched APK")
    parser.add_argument("--no-debuggable", action="store_true",
                        help="set android:debuggable=false (no longer needed for the "
                             "16 KB dialog: Android exports use aligned .NET 9 libs)")
    parser.add_argument("--serial", help="adb device serial for --install")
    args = parser.parse_args()

    if not os.path.exists(args.base_apk):
        die(f"base APK not found: {args.base_apk}")
    if not os.path.exists(args.dll):
        die(f"assembly not found: {args.dll}")

    dll = open(args.dll, "rb").read()
    if dll[:2] != b"MZ":
        die(f"{args.dll} is not a PE/.NET assembly")

    work = tempfile.mkdtemp(prefix="patchapk-")
    print(f"==> Extracting {args.base_apk}")
    subprocess.run(["unzip", "-q", args.base_apk, "-d", work], check=True)

    for required in (DLL_REL, PCK_REL, CL_REL):
        if not os.path.exists(os.path.join(work, required)):
            die(f"{required} not found in {args.base_apk} (not a Godot .NET Android export?)")

    print(f"==> Replacing {DLL_REL} ({len(dll)} bytes)")
    with open(os.path.join(work, DLL_REL), "wb") as f:
        f.write(dll)
    patch_sparse_pck(os.path.join(work, PCK_REL), len(dll), hashlib.md5(dll).digest())

    if args.extra_arg:
        cl_path = os.path.join(work, CL_REL)
        cl_args = read_cl_args(cl_path)
        for arg in args.extra_arg:
            if arg not in cl_args:
                cl_args.append(arg)
        write_cl_args(cl_path, cl_args)
        print(f"==> {CL_REL} args: {cl_args}")

    if args.no_debuggable:
        patch_manifest_debuggable(os.path.join(work, MANIFEST_REL))

    print("==> Repacking (stored entries) and aligning for 16 KB pages")
    unsigned = os.path.join(work, "unsigned.apk")
    subprocess.run(["zip", "-qr", "-0", unsigned, "."], cwd=work, check=True)
    aligned = os.path.join(work, "aligned.apk")
    subprocess.run([sdk_tool("zipalign"), "-f", "-P", "16", "4", unsigned, aligned], check=True)

    keystore = os.environ.get("GODOT_DEBUG_KEYSTORE",
                              os.path.expanduser("~/.local/share/godot/keystores/debug.keystore"))
    if not os.path.exists(keystore):
        die(f"Godot debug keystore not found: {keystore} (set GODOT_DEBUG_KEYSTORE)")
    print(f"==> Signing with {keystore}")
    subprocess.run([sdk_tool("apksigner"), "sign", "--ks", keystore,
                    "--ks-pass", KEYSTORE_PASS, "--ks-key-alias", KEYSTORE_ALIAS,
                    "--out", args.out_apk, aligned],
                   check=True, stderr=subprocess.DEVNULL)
    print(f"==> Wrote {args.out_apk} ({os.path.getsize(args.out_apk)} bytes)")

    if args.install:
        adb = os.environ.get("ADB", "adb")
        adb_cmd = [adb] + (["-s", args.serial] if args.serial else [])
        print("==> Installing")
        subprocess.run(adb_cmd + ["install", "--no-incremental", args.out_apk], check=True)
    shutil.rmtree(work, ignore_errors=True)


main()
