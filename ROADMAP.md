# Roadmap — Match Three

Progress tracker. Update checkboxes as milestones land.

- [ ] **M1 — Engine core** — Board, swap validation, MatchDetector, Gravity, Refill, SeededRandom. JVM unit tests incl. simulated board validation (9×9/6).
- [ ] **M2 — UI render & input** — Single-Canvas board, drag-to-swap + tap fallback, animation playback of Step events. Frame-time measured on worst case (full-board clear).
- [ ] **M3 — Scoring & timer** — Cascade scoping, HUD (score/time), Classic timer, GameOver screen.
- [ ] **M4 — Specials & combos** — Birth/precedence rules, all 6 combo pairs, unique-cell scoring tests.
- [ ] **M5 — Modes & polish** — Menu screen, Classic/Zen selection, high scores (DataStore), sounds/haptics optional.

## Decisions log

| Decision | Value |
|---|---|
| Board size | 9×9 (locked; tunable later) |
| Rendering | Single Canvas, fallback per-composable |
| Input during lock | Buffer most-recent drag, not drop |
| Special birth | One matched gem transforms;
others clear |
| Cascade-swept specials | They detonate |
| Unique-cell scoring | Set<Position>, tested |
| Game over | Timer (Classic) or dead board + failed reshuffle (Zen) |
