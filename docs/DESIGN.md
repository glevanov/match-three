# Match Three — Design

Bejeweled-style match-three game for Android (local APK, no store) written in
**Godot 4.7.2 (.NET/Mono) + C#**. This repo is the completed Godot port
(formerly a Kotlin/Compose app — see `docs/GODOT_PORT_PLAN.md`, all milestones
done; the Kotlin sources were removed at cutover and survive only in git
history). Game rules live in [MECHANICS.md](MECHANICS.md); progress and the
decision log live in [ROADMAP.md](ROADMAP.md).

## Tech stack

- **Godot 4.7.2 stable (.NET build), C# / .NET 8** — pinned; the `.csproj`
  (`Godot.NET.Sdk/4.7.2`), `project.godot` feature tag and editor version must
  not drift apart.
- **`Engine/`** — pure C# class library, **zero `Godot` namespace dependency**.
  Builds and tests with `dotnet build` / `dotnet test` alone. Excluded from
  the game assembly via `<Compile Remove>` in `MatchThree.csproj` and
  referenced as a project.
- **NUnit** for engine tests (`Tests/`), run with `dotnet test`.
- Records + sealed record hierarchies + switch pattern matching reproduce the
  old Kotlin idioms (`data class`, `sealed interface`, exhaustive `when`).
- Art: the 9 PNGs in `Assets/Sprites/` (six colors — Orange renders
  `white.png` — plus hypercube/flame/sparkle) and `Assets/Icon/icon.png`
  (re-exported from the original `assets/icon.jpg`).

## Architecture

```
View (Godot nodes)                 Engine (pure C#, Engine/)
────────────────────               ────────────────────────
Game.cs autoload (signals)   ◄──   GameEngine: swap → detect →
BoardView / GemActor / StepPlayer  resolve → gravity → refill
                                   Emits ordered Step events:
                                   Swap / ComboActivate / SpecialBirth /
                                   Destroy / Fall / Spawn / Score / Settled
```

The engine resolves input into an ordered `List<Step>`; the view plays steps
back as animation. Gems carry stable `Id`s (value-type records) so actors
track them through falls and spawns. Kotlin's `StateFlow` became Godot
signals; coroutine step playback became `async`/`await` on
`ToSignal(tween, Finished)`.

## Package layout

```
match-three/
├── project.godot                ← Godot 4.7.2, C#, mobile renderer, 540×960
├── MatchThree.csproj/.sln       ← game assembly (Godot.NET.Sdk), refs Engine
├── Engine/                      ← pure C# rules, no Godot dependency
│   ├── Model/   Board, Gem, GemType, Special, Position, BoardConfig
│   ├── Data/    GameMode, HighScoreStore (user://highscores.json)
│   └── Rules/   GameEngine, MatchDetector, Gravity, Refill, Scorer,
│                SpecialRules, BoardGenerator, LegalMoveDetector,
│                SeededRandom, IdSource, Step, Match, Resolution,
│                SwapIntent, BufferedSwapGuard
├── View/        Game.cs (autoload), BoardView.cs, StepPlayer.cs,
│                GemActor.cs, GemSprites.cs, BoardOp.cs, Hud.cs,
│                GameOverScreen.cs, MenuScreen.cs
├── Scenes/      Menu.tscn (main), Game.tscn, GemActor.tscn
├── Assets/
│   ├── Sprites/ the 9 gem/special PNGs (copied from the old app assets)
│   └── Icon/    icon.png (512×512, re-exported from assets/icon.jpg)
├── Tests/       NUnit project, runs via dotnet test (no Godot runtime)
├── assets/      original source art (icon.jpg, cat_with_gem.jpeg, PNGs)
└── docs/        GODOT_PORT_PLAN.md (migration record, all milestones done)
```

## View layer

- `Game.cs` autoload replaces the old ViewModel: engine + board + input lock +
  ordered op queue, drained by ONE consumer task (two writers on one GemActor
  cancel each other's Tweens and wedge the phase — the Kotlin c45deee lesson).
  Buffered swaps use `BufferedSwapGuard`; stale-Hypercube intents are dropped,
  most-recent wins otherwise. Signals: ScoreChanged / TimerChanged /
  RoundEnded(reason, score) / RoundStarted; a generation counter makes
  in-flight callbacks from a previous round no-op.
- `BoardView.cs` (Node2D): board bg/grid/selection in `_Draw`, owns the
  StepPlayer, handles input — drag past 40% of a cell commits the directional
  swap; tap-tap select-adjacent fallback (MECHANICS.md). Touch and mouse.
- `StepPlayer.cs`: gem-id → GemActor map + logical id grid; sequences steps
  with `async`/`await` on `ToSignal(tween, Finished)`; parallel effects via
  `Task.WhenAll`. Timing: swap 150ms, destroy 200ms, fall 90+70/row ms.
- `GemActor.cs` on `GemActor.tscn`: Sprite2D base + Flame/Star overlay art +
  baked alpha-silhouette outlines.
- `Hud.cs` / `MenuScreen.cs` / `GameOverScreen.cs`: HUD strip, mode menu with
  per-mode high scores, game-over overlay (Best + New-high-score + Play again
  / Menu). Exit-to-menu goes through a confirm dialog.
- Easing note: Kotlin's FastOutSlowIn maps to Godot's Cubic+Out.

## Engine porting notes (behavioral drift watchlist)

- `Board` copies: `Gem` is a `readonly record struct`, so `Gem?[,]` clones are
  automatically deep — `WithSwapped`/`WithGem` allocate a fresh grid, never
  mutate.
- Kotlin smart casts do not port: nullable `Special?`/`Gem?` need explicit
  patterns (`is { Special: { } s }`, `.Value`).
- `FirstOrDefault` can't express "not found" for a value-type Position —
  `PositionOf` and birth-cell selection search explicitly.
- Kotlin trailing commas in argument lists are illegal in C#.
- `SeededRandom` wraps `System.Random` (int seed). Deterministic within this
  port, not bit-identical to Kotlin's `kotlin.random.Random` — exact gem
  sequences differ; all parity tests are structural, not RNG-value dependent.
- `ToSignal` returns `SignalAwaiter`, not `Task` — wrap: `async Task` method
  that awaits it (needed for `Task.WhenAll`).

## Verification

- `dotnet test Tests/` — engine rules, no Godot runtime (87 tests).
- Godot editor: `--import` + `--build-solutions` verified under 4.7.2 mono.
- Headless self-tests (user args after `--`, exit 0 = pass):
  `--selftest-swap`, `--selftest-reject`, `--selftest-burst=N`,
  `--selftest-timer`, `--selftest-special`, `--selftest-hypercube`,
  `--selftest-menu`.
- Board simulation metrics (150 boards, 9x9/6): avg legal moves ≈ 18.8,
  random-swap match probability ≈ 0.13.

### G6 parity checklist (MECHANICS.md rule -&gt; where it is verified)

Every rule below is verified by the ported test suite (87 tests, plain
`dotnet test`) and/or a headless self-test; anything needing a human hand is
called out explicitly.

| MECHANICS.md rule | Verification |
|---|---|
| 9x9/6 board, tunable | BoardGeneratorTest (size + invariants); simulation metrics |
| Drag-to-swap, ~40% cell threshold; tap-tap fallback | BoardView.cs port; touch feel needs a device |
| Input lock: buffer most-recent, no drops | --selftest-burst=10/20; drain-loop single consumer |
| Hypercube entered/left pair = stale drop | BufferedSwapGuardTest (7 tests) |
| Invalid swap there-and-back ~150ms | --selftest-reject (phase returns to Idle) |
| Matches: max H/V runs of 3+ | MatchDetectorTest (7 tests) |
| Cascade: match, clear, gravity, refill, re-check; depth 1+ | GameEngineTest (ordered steps, depth alternation) |
| Flame 3x3 / Star row+col / Hypercube colorless | SpecialComboTest (pure + integration) |
| Birth: per shape, one special, rest clear | SpecialComboTest per-shape-group tests |
| Precedence 5 &gt; T/L &gt; 4 &gt; 3 | SpecialComboTest (4 precedence tests) |
| Cascade-swept specials detonate | SpecialComboTest.flameSwept + engine round loop |
| Hypercube trigger (plain/special partner) | SpecialComboTest + --selftest-hypercube |
| Six combos + ComboActivate before Destroy | SpecialComboTest (all six, step order) |
| H+H full clear + immediate regeneration | SpecialComboTest + --selftest-hypercube (810 pts, no specials) |
| Scoring 10/gem, linear depth, unique cells | ScorerTest (7 tests) |
| No special-creation bonus | Scorer has no such path (by construction) |
| Invariants checked only after settle | Engine loop structure + settled-board assertions |
| Generation: no pre-match, legal move, regenerate | BoardGeneratorTest (50 boards) |
| Reshuffle Fisher-Yates, ids/types preserved, 20 retries | GameEngineTest.reshuffle (multiset preserved, null on dead) |
| Classic 75s timer ends round | --selftest-timer (Time's up! -&gt; GameOver -&gt; Restart) |
| Zen: ends on dead board + failed reshuffle | Engine test (reshuffle null) + Attach wiring; E2E needs a contrived dead board |
| Non-goals (no hints/bonus/mid-session persistence) | Honors by construction; only highscores persist |

Deliberate parity notes:
- Match sequences differ from the Kotlin build (System.Random vs
  kotlin.random.Random) — all parity tests are structural, none depend on
  exact RNG values.
- MECHANICS.md was edited (one line) to say a failed reshuffle ends the
  round, matching the Kotlin implementation and the ROADMAP decisions log
  ("dead board + failed reshuffle" game-over), which the port reproduces.
- Manual checks still open: touch feel (drag threshold, rejection bounce),
  HUD/gem visual parity on a device, Android export (G7 follow-up), frame
  pacing on worst-case clears (Kotlin M2 measured p95=16.71ms; Godot's
  profiler + Performance monitors replace FrameStats).