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
| Flame fire ring (visual) | Augment: procedural `gem_fire.gdshader` annulus around the gem edge, over the existing centered Flame icon; ring is the additive cue, icon stays for readability |