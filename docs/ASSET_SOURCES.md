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

Notes:

- `music.mp3` and `swipe.mp3` are byte-identical to the downloaded originals
  (no transcode/trim). `pop.mp3` and `flame.mp3` are **trimmed re-encodes**
  (256 kbps mp3): pop - 0.72 s source with ~188 ms lead + ~390 ms trail
  silence (actual pop body 0.19–0.31 s), cut to 0.145 s with the attack
  intact; flame - 5.04 s source with ~848 ms lead + ~2.6 s trail silence,
  cut to 1.61 s (the whoosh body 0.84–2.45 s). Untrimmed originals can be
  re-fetched from the URLs above.
- The music track is a full 3:04 song — quiet intro, hot master (~ -11 LUFS
  integrated, peaks to -0.03 dBFS), ~2.5 s silence fade-out at the tail — so
  each loop pass plays like a complete song that restarts. It is looped at
  import time (`loop=true` in `Assets/Audio/music.mp3.import`) rather than
  in code. `pop.mp3`'s master also peaks hot (+0.03 dBFS), which is why the
  Pop player runs at -3 dB.
- Original downloads also remain at `~/Downloads/` next to the copy
  pipeline that produced `Assets/Audio/` (the pop original was fetched
  directly from the Pixabay CDN during setup; the URL above is the
  canonical source).
- If the tracks are ever replaced, re-run the Godot import (`godot
  --headless --import`) so the `.import` files (committed) match the new
  sources.