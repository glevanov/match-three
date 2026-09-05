# Roadmap — Match Three

Progress tracker. Update checkboxes as milestones land.

- [x] **M1 — Engine core** — Board, swap validation, MatchDetector, Gravity, Refill, SeededRandom. JVM unit tests incl. simulated board validation (9×9/6).
- [x] **M2 — UI render & input** (p95=16.71 within 16.67 budget on Pixel 8) — Single-Canvas board, drag-to-swap + tap fallback, animation playback of Step events. Frame-time measured on worst case (full-board clear).
- [x] **M3 — Scoring & timer** — Step.Score emission, Scorer (unique-cell, linear depth), HUD score/time, Classic 75s placeholder timer, dead-board reshuffle + game over, GameOverScreen placeholder.
- [x] **M4 — Specials & combos** — Special enum, birth precedence (5 > T/L > 4 > 3), all 6 combos + hypercube trigger, cascade-swept detonation, H+H regeneration, Canvas special marks, unique-cell scoring tests.
- [x] **M5 — Modes & polish** — Menu (Classic 75s / Zen endless), mode-wired GameScreen, DataStore high scores per mode, GameOver high score + menu button. Sounds/haptics skipped (no audio assets; optional per plan).

## Decisions log

| Decision | Value |
|---|---|
| Board size | 9×9 (locked; tunable later) |
| Rendering | Single Canvas, fallback per-composable |
| Input during lock | Buffer most-recent drag, not drop; stale-drop only when a Hypercube enters/leaves the buffered pair |
| Special birth | Per shape (runs sharing cells): one gem transforms per shape per round; others clear |
| Cascade-swept specials | They detonate |
| Unique-cell scoring | Set<Position>, tested |
| Game over | Timer (Classic) or dead board + failed reshuffle (Zen) |
| Classic timer (M3) | 75 s placeholder per round, no bonuses yet |
