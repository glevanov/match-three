#!/usr/bin/env bash
# sfx-verify.sh — prove a processed SFX did not distort the source body.
#
# Decodes source and rendered file to mono f32, aligns them, gain-matches,
# and reports correlation + residual. A clean trim+gain render is
# correlation ~1.00000 with a residual below -40 dB; a soft-clipped body
# shows up as correlation < 0.99 and residual > -30 dB.
#
# usage: sfx-verify.sh <source-file> <rendered-file> [source-start] [source-end]
#   source-start/end restrict the comparison to the window that was rendered
#   (seconds, like ffmpeg -ss/-to). Omit to compare whole files.
set -euo pipefail
export LC_ALL=C

src="${1:?usage: sfx-verify.sh <source-file> <rendered-file> [source-start] [source-end]}"
out="${2:?usage: sfx-verify.sh <source-file> <rendered-file> [source-start] [source-end]}"
start="${3:-}"
end="${4:-}"

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

render_src() {
    if [ -n "$start" ] || [ -n "$end" ]; then
        ffmpeg -v error -y -ss "${start:-0}" ${end:+-to "$end"} -i "$src" -ac 1 -ar 44100 -f f32le "$tmp/ref.f32"
    else
        ffmpeg -v error -y -i "$src" -ac 1 -ar 44100 -f f32le "$tmp/ref.f32"
    fi
}

render_src
ffmpeg -v error -y -i "$out" -ac 1 -ar 44100 -f f32le "$tmp/mine.f32"

python3 - "$tmp/ref.f32" "$tmp/mine.f32" <<'PY'
import sys
import numpy as np

ref = np.fromfile(sys.argv[1], dtype=np.float32).astype(np.float64)
mine = np.fromfile(sys.argv[2], dtype=np.float32).astype(np.float64)

def rms(x):
    return float(np.sqrt(np.mean(x ** 2))) if len(x) else 0.0

def lowpass(x, sr=44100, f=800):
    # simple FFT brick-wall lowpass — good enough for a distortion check
    S = np.fft.rfft(x)
    freqs = np.fft.rfftfreq(len(x), 1 / sr)
    S[freqs > f] = 0
    return np.fft.irfft(S, n=len(x))

def align(ref, mine):
    best = (-2.0, 0)
    for lag in range(-2200, 2201, 2):
        if lag >= 0:
            a, b = ref[:len(mine) - lag], mine[lag:]
        else:
            a, b = ref[-lag:len(mine)], mine[:len(mine)]
        n = min(len(a), len(b))
        if n < 512:
            continue
        a, b = a[:n], b[:n]
        d = np.linalg.norm(a) * np.linalg.norm(b)
        if d == 0:
            continue
        c = float(np.dot(a, b) / d)
        if c > best[0]:
            best = (c, lag)
    c, lag = best
    return (ref[:len(mine) - lag], mine[lag:]) if lag >= 0 else (ref[-lag:], mine[:])

ref_a, mine_a = align(ref, mine)
n = min(len(ref_a), len(mine_a))
ref_a, mine_a = ref_a[:n], mine_a[:n]

# gain-match rendered to source, then measure what is left
scale = float(np.dot(ref_a, mine_a) / max(np.dot(mine_a, mine_a), 1e-30))
fit = mine_a * scale
resid = ref_a - fit
thd = 20 * np.log10(max(rms(resid), 1e-12) / max(rms(ref_a), 1e-12))
corr = float(np.corrcoef(ref_a, mine_a)[0, 1])

# body check (100-800 Hz) — where distortion is most audible
lp_ref, lp_fit = lowpass(ref_a), lowpass(fit)
lp_resid = lp_ref - lp_fit
body_corr = float(np.corrcoef(lp_ref, lp_fit)[0, 1])
body_thd = 20 * np.log10(max(rms(lp_resid), 1e-12) / max(rms(lp_ref), 1e-12))

print(f"samples compared : {n}  ({n/44100:.3f} s)")
print(f"gain match       : rendered x {scale:.4f} ({20*np.log10(max(scale,1e-12)):+.2f} dB)")
print(f"full band        : corr {corr:.5f} | residual {thd:6.1f} dB")
print(f"body 100-800 Hz  : corr {body_corr:.5f} | residual {body_thd:6.1f} dB")
verdict = "CLEAN (trim/gain only)" if body_thd < -30 else "DISTORTED — body was clipped; use a parallel layer"
print(f"verdict          : {verdict}")
PY
