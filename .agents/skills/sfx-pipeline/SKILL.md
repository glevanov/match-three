---
name: sfx-pipeline
description: Use when asked to add or replace a sound effect (SFX).
---

## Steps
- [ ] (If provided a link) Download
- [ ] Analyze and process
- [ ] Install and import
- [ ] Verify
- [ ] Record provenance

## Download
If sourcing from https://pixabay.com, use `.agents/skills/sfx-pipeline/scripts/pixabay-fetch.sh <page-url> [outdir]`.
If script fails, use `curl`.

## Analyze and process
- Start with analyzing the SFX with `.agents/skills/sfx-pipeline/scripts/sfx-measure.sh <file> [node_db]`.
- Process for trim, gain and phone audibility.
- Gain must use `volume=+XdB`, never omit the `dB` suffix. Linear multiplier caused a hard-clipped click before.
  Target peak -6 to -3 dBFS.
- If hp800 is ≥~15dB down (phone-inaudible), apply edge-layer or harmonic-synthesis.
- Never soft-clip the whole signal, parallel layer only.
- When replacing an SFX, measure existing one first. Target the same effective M (±0.5dB) and effective peak.
- Prefer changing node dB over the file. Only touch the file for spectral (hp800) changes.
- Verify every synthesis render with `sfx-verify.sh`.
  Correlation ~1.0 / residual <-40dB = clean; correlation <0.99 / residual >-30dB = DISTORTED, redo.
- Encode as **MP3 256 kbps, 44.1 kHz**, use `ffmpeg`.

## Install and import
- Commit the `.import` file. Replacing an asset at the same path keeps the UID; new assets get one on import.
- Update debug preview. Update existing SFX button or add a new one.

## Verify
- Use `.agents/skills/sfx-pipeline/scripts/sfx-verify.sh <source> <rendered> [start] [end]`.
- Run available tests, including selftest.
- If asked, deploy to the phone with a project-level script (`scripts/export-android.sh`).

## Record provenance
- Record provenances even for assets that do not require attribution.
- Use `docs/ASSET_SOURCES.md`, follow existing table format.

## Report on failure
Report to the user if any skill script fails.
