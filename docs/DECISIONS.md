# Match Three — Decision Log

Settled decisions from development. Consult before proposing a change to
board size, input model, scoring, or game-over rules (see AGENTS.md). The
game rules derived from these live in [MECHANICS.md](MECHANICS.md).

| Decision | Value |
|---|---|
| Board size | 9×9 (locked; tunable later) |
| Rendering | Godot nodes (per-gem GemActor scenes) |
| Input during lock | Buffer most-recent drag, not drop; stale-drop only when a Hypercube enters/leaves the buffered pair |
| Special birth | Per shape (runs sharing cells): one gem transforms per shape per round; others clear |
| Cascade-swept specials | They detonate |
| Unique-cell scoring | Set<Position>, tested |
| Game over | Timer (Classic) or dead board + failed reshuffle (Zen) |
| Classic timer | 75 s placeholder per round, no bonuses yet |
| RNG | SeededRandom (System.Random); deterministic per session/seed |
| Flame aura (visual) | Replaces the fire-ring attempt: procedural `gem_fire.gdshader` body aura — fbm cracks plus an erosion-derived rim, `blend_add` so the glow brightens the gem — masked by the gem's own base-texture alpha (FireRing sized to Base's exact drawn footprint so UVs line up 1:1), so the aura cannot bleed past the gem art. Tinted per gem type: `core_color`/`rim_color` uniforms are set from the gem art's mean opaque color (core lightened 55% toward white, rim 15%) — a blue gem glows light blue. `intensity` 0.85: on-device measurement showed 0.35 was near-invisible (mean delta 3-27/255 against plain gems, worst on light gems). Centered Flame icon dropped: the aura alone is the cue; Star keeps its overlay. If the aura reads too strong, lower `intensity`; if still too subtle on-device, re-enable the icon (one line in `GemActor.ApplyAppearance`) |