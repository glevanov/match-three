# Roadmap — Match Three

Progress tracker. Update checkboxes as milestones land.

The Godot/C# port replaced the Kotlin/Compose app; all port milestones
(see docs/GODOT_PORT_PLAN.md) are complete. Future work tracks against the
Godot project at the repo root.

## Godot port milestones (all complete)

- [x] **G0 — Scaffold** — project.godot, csproj/sln, folder layout, assets
      copied, NUnit test project, Game.cs autoload; dotnet build + editor
      verified.
- [x] **G1 — Engine core** — Board/Gem/Position/BoardConfig, generator,
      MatchDetector, Gravity, Refill, SeededRandom (+ the rest of the engine:
      GameEngine, SpecialRules, Scorer, LegalMoveDetector, Step hierarchy).
      70 tests green via dotnet test.
- [x] **G2 — UI render & input** — Game.tscn, GemActor.tscn, GemSprites,
      drag-to-swap (40% cell) + tap fallback, StepPlayer async/await
      sequencing; SwapIntent/BufferedSwapGuard tests ported (81 green).
- [x] **G3 — Scoring & timer** — Scorer (ported in G1), HUD, Classic 75s
      timer, dead-board reshuffle, GameOver placeholder.
- [x] **G4 — Specials & combos** — SpecialRules + SpecialComboTest landed
      with G1/G2; special marks in G2; end-to-end
      `--selftest-special`/`--selftest-hypercube`.
- [x] **G5 — Modes & polish** — Menu.tscn (Classic/Zen), mode wiring,
      HighScoreStore (87 tests green), GameOver high-score UI.
- [x] **G6 — Parity check** — rule-by-rule checklist against MECHANICS.md
      (see DESIGN.md); manual device checks still open (export, touch feel,
      visual/frame parity).
- [x] **G7 — Cutover** — moved godot/* to repo root, deleted the Kotlin app
      and Gradle files, merged GODOT_DESIGN.md into DESIGN.md, updated
      AGENTS.md/ROADMAP.md.

## Decisions log

| Decision | Value |
|---|---|
| Board size | 9×9 (locked; tunable later) |
| Rendering | Godot nodes (per-gem GemActor scenes) — Compose's single-Canvas constraint doesn't apply |
| Input during lock | Buffer most-recent drag, not drop; stale-drop only when a Hypercube enters/leaves the buffered pair |
| Special birth | Per shape (runs sharing cells): one gem transforms per shape per round; others clear |
| Cascade-swept specials | They detonate |
| Unique-cell scoring | Set<Position>, tested |
| Game over | Timer (Classic) or dead board + failed reshuffle (Zen) |
| Classic timer | 75 s placeholder per round, no bonuses yet |
| RNG | SeededRandom (System.Random); deterministic per session/seed, not cross-language with the old Kotlin build |