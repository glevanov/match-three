# Asset Sources

Provenance for third-party assets bundled into the game. `Assets/` holds the
renamed copies the game actually loads; this file records where each one came
from and the current license/attribution status (re-check before any
store/public distribution).

| Game file (`Assets/`) | Source | Original file | License / attribution |
|---|---|---|---|
| `Audio/music.mp3` (loop=true) | https://pixabay.com/music/beats-travel-nature-lofi-music-349633/ | `tunetank-travel-nature-lofi-music-349633.mp3` | Pixabay Content License. Attribution **not required** (recorded for reference). |
| `Audio/swipe.mp3` | https://pixabay.com/sound-effects/film-special-effects-fast-swipe-48158/ | `freesound_community-fast-swipe-48158.mp3` | Pixabay Content License. Attribution **not required** (recorded for reference). |
| `Audio/pop.mp3` (trimmed) | https://pixabay.com/sound-effects/film-special-effects-pop-402324/ | `dragon-studio-pop-402324.mp3` (DRAGON-STUDIO) | Pixabay Content License. Attribution **not required** (recorded for reference). |
| `Audio/flame.mp3` (trimmed) | https://pixabay.com/sound-effects/film-special-effects-short-fire-whoosh-1-317280/ | `djartmusic-short-fire-whoosh_1-317280.mp3` (djartmusic) | Pixabay Content License. Attribution **not required** (recorded for reference). |
| `Audio/star.mp3` (trimmed) | https://pixabay.com/sound-effects/musical-glockenspiel-up-sweep-502134/ | `yusuf_sfx-glockenspiel-up-sweep-502134.mp3` (yusuf_sfx) | Pixabay Content License. Attribution **not required** (recorded for reference). |
| `Audio/hypercube.mp3` (trimmed) | https://pixabay.com/sound-effects/film-special-effects-cinematic-impact-hit-352702/ | `universfield-cinematic-impact-hit-352702.mp3` (universfield) | Pixabay Content License. Attribution **not required** (recorded for reference). |
| `Backgrounds/starry_night.jpg` | https://pixabay.com/photos/stars-night-sky-starry-sky-2179083/ | `pexels-stars-2179083.jpg` (5638×3748) | Pixabay Content License. Attribution **not required** (recorded for reference). |
| `Backgrounds/starry_sky.jpg` | https://pixabay.com/photos/stars-sky-night-starry-sky-1837306/ | `pexels-stars-1837306.jpg` (4992×3648) | Pixabay Content License. Attribution **not required** (recorded for reference). |
| `Backgrounds/milky_way.jpg` | https://pixabay.com/photos/milky-way-starry-sky-9859259/ | `studiofriluma-milky-way-9859259.jpg` (6000×4000) | Pixabay Content License. Attribution **not required** (recorded for reference). |
| `Backgrounds/cosmos.jpg` | https://pixabay.com/photos/cosmos-milky-way-night-sky-stars-1853491/ | `pexels-cosmos-1853491.jpg` (6016×4016) | Pixabay Content License. Attribution **not required** (recorded for reference). |

Notes:

- `music.mp3` and `swipe.mp3` are byte-identical to the downloaded originals
  (no transcode/trim). `pop.mp3`, `flame.mp3`, `star.mp3` and `hypercube.mp3`
  are **trimmed re-encodes** (256 kbps mp3):
  - pop: 0.72 s source with ~188 ms lead + ~390 ms trail silence (actual pop
    body 0.19–0.31 s), cut to 0.145 s with the attack intact;
  - flame: 5.04 s source with ~848 ms lead + ~2.6 s trail silence, cut to
    1.61 s (the whoosh body 0.84–2.45 s);
  - star: 1.91 s source, 168 ms lead, 1.23 s trail; the glockenspiel attack
    spike sits at 0.16 s, cut to 0.61 s (0.14–0.75 s) keeping the spike
    intact;
  - hypercube: 3.05 s source with ~105 ms lead + ~0.5 s dead tail, cut to
    2.35 s (0.10–2.45 s), then **processed for phone audibility (v8)**: the
    raw master is ~all sub-bass (RMS -25 dB lower above 300 Hz — inaudible
    on phone speakers), so an edge layer (highpass ≥ 800 Hz + compressor +
    makeup) is mixed under the sub body, +13 dB above 300 Hz / +19 dB above
    800 Hz, alimiter 0.9. Peak went -4.3 → -0.6 dBFS (node runs at -4 dB).
  Untrimmed originals can be re-fetched from the URLs above (or stay in
  ~/Downloads where the user placed them).
- The music track is a full 3:04 song — quiet intro, hot master (~ -11 LUFS
  integrated, peaks to -0.03 dBFS), ~2.5 s silence fade-out at the tail — so
  each loop pass plays like a complete song that restarts. It is looped at
  import time (`loop=true` in `Assets/Audio/music.mp3.import`) rather than
  in code. `pop.mp3`'s master also peaks hot (+0.03 dBFS), which is why the
  Pop player runs at -3 dB.
- The four board backgrounds ship as **2560 px long-side Lanczos downscales**
  of the full-resolution Pixabay originals (5–6 MP-class; the largest size
  logged-out CDN serving is `_1280.jpg`, so the originals were downloaded
  directly and are kept at `~/Downloads/` under the names listed above).
  2560 px is beyond any phone screen's visible resolution (a covered-fit
  portrait crop of a ~1080×2400 display uses ≈1350×2900 px of the source)
  and keeps each imported texture ~5 MB ASTC instead of ~25–36 MB — APK
  size and VRAM both stay sane. Re-encode was Lanczos + quality-90 JPEG
  (optimize). They render landscape-cropped to the portrait viewport
  (stretch/keep-aspect-covered, `View/Background.cs`), so the effective
  on-screen image is a center slice of each photo.
- Original downloads also remain at `~/Downloads/` next to the copy
  pipeline that produced `Assets/Audio/` (the pop original was fetched
  directly from the Pixabay CDN during setup; the URL above is the
  canonical source).
- If the tracks are ever replaced, re-run the Godot import (`godot
  --headless --import`) so the `.import` files (committed) match the new
  sources.