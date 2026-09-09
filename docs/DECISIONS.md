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
| Flame aura (visual) | Replaces the fire-ring attempt: procedural `gem_fire.gdshader` body aura — fbm cracks plus an erosion-derived rim, `blend_add` so the glow brightens the gem — masked by the gem's own base-texture alpha (FireRing sized to Base's exact drawn footprint so UVs line up 1:1). Per user direction (v8): the flame lives in an **outer band near the silhouette; the gem's center stays clean** (no flame/tint), and a **soft low-intensity halo** is allowed to bleed a little past the silhouette into neighboring cells — this deliberately replaces the earlier hard no-bleed invariant (DECISIONS-era v4) at the user's request; light halo intensity keeps it a glow, not an obstruction. Tinted per gem type: `core_color`/`rim_color` uniforms set from the gem art's mean opaque color (core lightened 55% toward white, rim 15%) — a blue gem glows light blue. `intensity` 0.85 (0.35 was near-invisible on-device). Centered Flame icon dropped: the aura alone is the cue; Star keeps its overlay. Levers: `intensity`, `halo_intensity`, `rim_distance`, `band_distance` (shader). If too subtle, re-enable the icon (one line in `GemActor.ApplyAppearance`) |