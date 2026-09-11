#!/usr/bin/env bash
set -euo pipefail

usage() {
    cat <<'EOF'
Usage: scripts/export-android.sh [options]

Export the Android debug APK with Godot's .NET export flow, optionally install
it on a connected device, and optionally launch it.

Options:
  --install         Install the exported APK with adb
  --run             Launch the app after export/install
  --apk PATH        Output APK path (default: build/matchthree-debug.apk)
  --serial SERIAL   adb device serial to target
  --godot PATH      Godot mono binary to use
  -h, --help        Show this help

Environment:
  GODOT_BIN         Default Godot binary if --godot is not passed

Notes:
  - This script intentionally uses `godot --export-debug` without
    `--build-solutions`. Combining them previously produced APKs missing the
    packaged .NET assemblies (`Assemblies not found`) and the app crashed on
    startup on device.
  - If `dotnet` is not on PATH, the script automatically prepends
    `$HOME/.dotnet` when available.

Examples:
  scripts/export-android.sh
  scripts/export-android.sh --install --run
  GODOT_BIN=/tmp/godot_mono/Godot_v4.7.2-stable_mono_linux_x86_64/godot \
    scripts/export-android.sh --install --run
EOF
}

die() {
    echo "error: $*" >&2
    exit 1
}

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

apk_path="build/matchthree-debug.apk"
godot_bin="${GODOT_BIN:-/tmp/godot_mono/Godot_v4.7.2-stable_mono_linux_x86_64/godot}"
install_apk=false
run_app=false
serial=""
package_name="com.matchthree"
launcher_activity="com.godot.game.GodotAppLauncher"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --install)
            install_apk=true
            ;;
        --run)
            run_app=true
            ;;
        --apk)
            [[ $# -ge 2 ]] || die "--apk requires a path"
            apk_path="$2"
            shift
            ;;
        --serial)
            [[ $# -ge 2 ]] || die "--serial requires a device serial"
            serial="$2"
            shift
            ;;
        --godot)
            [[ $# -ge 2 ]] || die "--godot requires a path"
            godot_bin="$2"
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            die "unknown option: $1"
            ;;
    esac
    shift
done

if ! command -v dotnet >/dev/null 2>&1; then
    if [[ -x "$HOME/.dotnet/dotnet" ]]; then
        export PATH="$HOME/.dotnet:$PATH"
    else
        die "dotnet not found on PATH, and $HOME/.dotnet/dotnet does not exist"
    fi
fi

command -v unzip >/dev/null 2>&1 || die "unzip is required to validate the APK"
[[ -x "$godot_bin" ]] || die "Godot binary not found or not executable: $godot_bin"

adb_cmd=(adb)
if [[ -n "$serial" ]]; then
    adb_cmd+=( -s "$serial" )
fi
if [[ "$install_apk" == true || "$run_app" == true ]]; then
    command -v adb >/dev/null 2>&1 || die "adb is required for --install/--run"
fi

mkdir -p "$(dirname "$apk_path")"

# 16 KB page-size compatibility: Godot 4.7.2's own native libs are 16 KB
# aligned, but the .NET 8 Mono runtime packs are not (only .NET 9+ is).
# Android 16 then shows a compatibility warning dialog at launch for
# debuggable builds, which can swallow touches. Setting
# android:pageSizeCompat="enabled" on <application> opts the app into
# page-size compat mode explicitly, and Android then launches it without the
# warning. The build template lives in the gitignored android/ tree, so ensure
# the attribute here, idempotently.
manifest="$repo_root/android/build/src/main/AndroidManifest.xml"
if [[ -f "$manifest" ]] && ! grep -q 'android:pageSizeCompat' "$manifest"; then
    if command -v python3 >/dev/null 2>&1; then
        python3 - "$manifest" <<'PY'
import re, sys
path = sys.argv[1]
src = open(path).read()
if 'android:pageSizeCompat' not in src:
    src, n = re.subn(r'(<application\b)', r'\1\n        android:pageSizeCompat="enabled"', src, count=1)
    if n == 0:
        raise SystemExit(f"error: no <application> tag in {path}")
    open(path, "w").write(src)
    print(f"    manifest: added android:pageSizeCompat=\"enabled\" to {path}")
PY
    else
        echo "    warning: python3 not found; add android:pageSizeCompat=\"enabled\" to" >&2
        echo "             $manifest to avoid the Android 16 16 KB warning dialog" >&2
    fi
fi

echo "==> Exporting Android debug APK"
echo "    repo:  $repo_root"
echo "    godot: $godot_bin"
echo "    apk:   $apk_path"
# Godot 4.7.2 headless Android export prints one known spurious warning here:
# `EditorSettings not instantiated yet when getting setting
#  export/android/shutdown_adb_on_exit`.
# Upstream fixed it later; suppress only that exact two-line block so real
# export errors still surface.
"$godot_bin" --headless --path "$repo_root" --export-debug "Android" "$apk_path" 2>&1 |
    sed '/^ERROR: EditorSettings not instantiated yet when getting setting "export\/android\/shutdown_adb_on_exit"\.$/{N;d;}'

apk_listing="$(unzip -l "$apk_path")"
for required in \
    "assets/.godot/mono/publish/arm64/MatchThree.dll" \
    "assets/.godot/mono/publish/arm64/MatchThree.Engine.dll" \
    "assets/.godot/mono/publish/arm64/GodotSharp.dll"
do
    grep -Fq "$required" <<<"$apk_listing" || die "APK validation failed; missing $required"
done

echo "==> APK validated (managed assemblies present)"
ls -lh "$apk_path"

if [[ "$install_apk" == true ]]; then
    echo "==> Installing APK"
    "${adb_cmd[@]}" install -r "$apk_path"
fi

if [[ "$run_app" == true ]]; then
    echo "==> Launching app"
    "${adb_cmd[@]}" shell am force-stop "$package_name" || true
    "${adb_cmd[@]}" shell am start -n "$package_name/$launcher_activity"
fi
