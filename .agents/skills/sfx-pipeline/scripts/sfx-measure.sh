#!/usr/bin/env bash
# sfx-measure.sh — one-shot audit of an audio asset for the Match Three SFX pipeline.
#
# Reports the numbers the pipeline always needs:
#   duration, peak, true peak, full/hp300/hp800 RMS (phone-speaker tilt),
#   integrated + momentary-max loudness, lead/trail silence, edge levels,
#   and the effective level at a given node volume.
#
# usage: sfx-measure.sh <file> [node_volume_db]
set -euo pipefail
export LC_ALL=C   # keep printf/awk on '.' decimals regardless of user locale

f="${1:?usage: sfx-measure.sh <file> [node_volume_db]}"
node="${2:-}"
[ -f "$f" ] || { echo "no such file: $f" >&2; exit 1; }

# Measurements are display-only: tolerate greps that find nothing
# (e.g. a 70 ms click has no ebur128 momentary readings).
set +e

num() { grep -oE '\-?[0-9]+(\.[0-9]+)?' | tail -1; }
r1() { awk -v v="${1:-}" 'BEGIN{if (v=="") print "?"; else printf "%.1f", v}'; }

astat() { # astat [pre-filter] -> runs astats (optionally preceded by extra filters)
    local pre="${1:-}"
    local filter="astats=metadata=1:reset=0"
    [ -n "$pre" ] && filter="$pre,$filter"
    ffmpeg -hide_banner -nostats -i "$f" -af "$filter" -f null - 2>&1 || true
}

dur=$(ffprobe -v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 "$f")
peak=$(astat | grep -m1 'Peak level dB' | num)
rms=$(astat | grep -m1 'RMS level dB' | num)
hp300=$(astat 'highpass=f=300' | grep -m1 'RMS level dB' | num)
hp800=$(astat 'highpass=f=800' | grep -m1 'RMS level dB' | num)
eb=$(ffmpeg -hide_banner -nostats -i "$f" -af ebur128=peak=true -f null - 2>&1 || true)
mom=$(printf '%s' "$eb" | grep -oE 'M: *-?[0-9]+(\.[0-9]+)?' | grep -oE '\-?[0-9]+(\.[0-9]+)?' | sort -n | tail -1)
int=$(printf '%s' "$eb" | grep -oE 'I: *-?[0-9]+(\.[0-9]+)? *LUFS' | tail -1 | num)
tpeak=$(printf '%s' "$eb" | grep 'Peak:' | tail -1 | num)

# edges: first/last 15 ms (cut-click check)
last_start=$(awk -v d="$dur" 'BEGIN{printf "%.3f", (d>0.015? d-0.015: 0)}')
edge_first=$(astat 'atrim=0:0.015' | grep -m1 'RMS level dB' | num)
edge_last=$(astat "atrim=start=$last_start" | grep -m1 'RMS level dB' | num)

tilt=$(awk -v a="$rms" -v b="$hp800" 'BEGIN{if (a==""||b=="") print "?"; else printf "%.1f", b-a}')

printf 'file:       %s\n' "$f"
printf 'duration:   %.3f s\n' "$dur"
printf 'peak:       %s dBFS   true peak: %s dBFS\n' "$(r1 "$peak")" "$(r1 "$tpeak")"
printf 'rms:        full %s | hp300 %s | hp800 %s  (hp800-full tilt: %s dB)\n' "$(r1 "$rms")" "$(r1 "$hp300")" "$(r1 "$hp800")" "$tilt"
printf 'loudness:   M-max %s | I %s LUFS\n' "$(r1 "$mom")" "$(r1 "$int")"
printf 'edges:      first15ms %s | last15ms %s dB RMS\n' "$(r1 "$edge_first")" "$(r1 "$edge_last")"

# silence map (lead/trail and multi-hit structure)
sil=$(ffmpeg -hide_banner -nostats -i "$f" -af silencedetect=noise=-45dB:d=0.03 -f null - 2>&1 || true)
printf 'silence:    %s\n' "$(printf '%s' "$sil" | grep -oE 'silence_(start|end): [0-9.]+' | paste -sd' ' - | sed 's/silence_start:/start=/g; s/silence_end:/end=/g')"

if [ -n "$node" ] && [ -n "$mom" ]; then
    awk -v m="$mom" -v i="$int" -v p="$tpeak" -v n="$node" \
        'BEGIN{printf "at node %+g dB: eff M %.1f | eff I %.1f | eff peak %.1f dBFS\n", n, m+n, i+n, p+n}'
fi
