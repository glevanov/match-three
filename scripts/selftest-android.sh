#!/usr/bin/env bash
# Build the current sources, patch them into the exported debug APK with a
# --selftest-* flag baked in, install on the connected device, launch, and
# report the self-test result from logcat.
#
# Why patch instead of export: a Godot binary is not always available, and
# launch-intent extras are ignored by Godot 4.7 Android builds (the activity
# is not exported), so the flag is packed into assets/_cl_ instead. The
# matching debug export must exist (scripts/export-android.sh).
#
# Usage: scripts/selftest-android.sh --selftest-flow [options]
#   --serial SERIAL    adb device serial
#   --timeout SECONDS  how long to wait for the result (default 120)
#   --base-apk PATH    debug export to patch (default build/matchthree-debug.apk)
#   --keep-apk PATH    keep the patched APK at PATH
set -euo pipefail

usage() {
    sed -n '2,12p' "$0" | sed 's/^# \{0,1\}//'
    exit "${1:-0}"
}

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

flag=""
serial=""
timeout_s=120
base_apk="build/matchthree-debug.apk"
keep_apk=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --selftest-*) flag="$1" ;;
        --serial) [[ $# -ge 2 ]] || { echo "error: --serial needs a value" >&2; exit 2; }; serial="$2"; shift ;;
        --timeout) [[ $# -ge 2 ]] || { echo "error: --timeout needs a value" >&2; exit 2; }; timeout_s="$2"; shift ;;
        --base-apk) [[ $# -ge 2 ]] || { echo "error: --base-apk needs a value" >&2; exit 2; }; base_apk="$2"; shift ;;
        --keep-apk) [[ $# -ge 2 ]] || { echo "error: --keep-apk needs a value" >&2; exit 2; }; keep_apk="$2"; shift ;;
        -h|--help) usage 0 ;;
        *) echo "error: unknown argument: $1" >&2; usage 2 ;;
    esac
    shift
done

[[ -n "$flag" ]] || { echo "error: a --selftest-* flag is required" >&2; usage 2; }
[[ -f "$base_apk" ]] || { echo "error: $base_apk not found - run scripts/export-android.sh first" >&2; exit 1; }
command -v adb >/dev/null || { echo "error: adb not found on PATH" >&2; exit 1; }
command -v unzip >/dev/null || { echo "error: unzip is required" >&2; exit 1; }

if ! command -v dotnet >/dev/null 2>&1; then
    if [[ -x "$HOME/.dotnet/dotnet" ]]; then
        export PATH="$HOME/.dotnet:$PATH"
    else
        echo "error: dotnet not found on PATH and $HOME/.dotnet/dotnet does not exist" >&2
        exit 1
    fi
fi

adb_cmd=(adb)
[[ -n "$serial" ]] && adb_cmd+=( -s "$serial" )

build_dir="$(mktemp -d)"
trap 'rm -rf "$build_dir"' EXIT

echo "==> Publishing MatchThree.dll (Debug, android-arm64)"
dotnet publish MatchThree.csproj -c Debug -r android-arm64 -o "$build_dir/publish" --self-contained false >/dev/null

out_apk="$build_dir/selftest.apk"
echo "==> Patching $base_apk with $flag"
python3 scripts/patch-debug-apk.py "$base_apk" "$build_dir/publish/MatchThree.dll" "$out_apk" \
    --extra-arg="$flag" --install ${serial:+--serial "$serial"}

if [[ -n "$keep_apk" ]]; then
    cp "$out_apk" "$keep_apk"
    echo "==> Kept patched APK at $keep_apk"
fi

echo "==> Launching $flag"
"${adb_cmd[@]}" logcat -c
"${adb_cmd[@]}" shell am force-stop com.matchthree
"${adb_cmd[@]}" shell am start -n com.matchthree/com.godot.game.GodotAppLauncher >/dev/null

deadline=$(( $(date +%s) + timeout_s ))
while (( $(date +%s) < deadline )); do
    log="$("${adb_cmd[@]}" logcat -d -s godot:* 2>/dev/null || true)"
    if grep -q "SELFTEST-OK" <<<"$log"; then
        grep "SELFTEST-OK" <<<"$log" | tail -1
        echo "PASS: $flag"
        exit 0
    fi
    if grep -q "SELFTEST-" <<<"$log"; then
        echo "--- logcat ---" >&2
        grep -E "SELFTEST-|Exception|ERROR" <<<"$log" | tail -20 >&2
        echo "FAIL: $flag" >&2
        exit 1
    fi
    sleep 2
done

echo "TIMEOUT: $flag produced no result in ${timeout_s}s" >&2
"${adb_cmd[@]}" logcat -d -s godot:* 2>/dev/null | tail -20 >&2
exit 1
