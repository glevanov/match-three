# Match Three — Godot Design (C# port)

Parallel design doc to the Kotlin/Compose `DESIGN.md`. The Kotlin doc describes
the Android/Compose tech stack and stays untouched until final cutover (see
GODOT_PORT_PLAN.md §1); this doc describes the Godot/C# implementation inside
`godot/`. `MECHANICS.md` is the *only* rules doc — never forked.

## Tech stack

- **Godot 4.7.2 stable (.NET/Mono build)** — pinned. The `.csproj`
  (`Godot.NET.Sdk/4.7.2`), `project.godot` feature tag (`4.7`, `C#`), and the
  editor version must not drift apart (GODOT_PORT_PLAN §7).
- **C# / .NET 8** (`net8.0`), LangVersion 12. Records + sealed record
  hierarchies + switch pattern matching reproduce the Kotlin `data class` /
  `sealed interface` / exhaustive `when` idioms almost keystroke-for-keystroke.
- **`Engine/`** — pure C# class library, **zero `Godot` namespace dependency**.
  Builds and tests with plain `dotnet build` / `dotnet test`, no editor needed.
  Excluded from the game assembly via `<Compile Remove>` in `MatchThree.csproj`
  and referenced as a project.
- **NUnit 4** for tests (`Tests/`), mirroring `app/src/test`.

## Architecture

Same shape as DESIGN.md, relabeled:

```
View (Godot nodes)                 Engine (pure C#, Engine/)
────────────────────               ────────────────────────
Game.cs autoload (signals)   ◄──   GameEngine: swap → detect →
BoardView / GemActor / StepPlayer  resolve → gravity → refill
                                   Emits ordered Step events:
                                   Swap / ComboActivate / SpecialBirth /
                                   Destroy / Fall / Spawn / Score / Settled
```

The engine resolves input into an ordered `List<Step>`; `View/` plays steps
back as animation. Gems carry stable `Id`s (value-type records) so actors
track them through falls and spawns. Kotlin's `StateFlow` becomes Godot
signals; coroutine step playback becomes `async`/`await` on
`ToSignal(Tween, Tween.SignalName.Finished)`.

## Package layout

```
godot/
├── project.godot                ← Godot 4.7.2, C#, mobile renderer, 540×960
├── MatchThree.csproj/.sln       ← game assembly (Godot.NET.Sdk), refs Engine
├── Engine/                      ← pure C# rules, no Godot dependency
│   ├── Model/   Board, Gem, GemType, Special, Position, BoardConfig
│   └── Rules/   GameEngine, MatchDetector, Gravity, Refill, Scorer,
│                SpecialRules, BoardGenerator, LegalMoveDetector,
│                SeededRandom, IdSource, Step, Match, Resolution,
│                SwapIntent, BufferedSwapGuard
├── View/        Game.cs (autoload), BoardView.cs, StepPlayer.cs,
│                GemActor.cs, GemSprites.cs, BoardOp.cs
├── Scenes/      Game.tscn (main: BoardView node), GemActor.tscn
├── Assets/
│   ├── Sprites/ red..white.png, hypercube.png, flame.png, sparkle.png
│   │            (copied from app/src/main/assets; ORANGE renders white.png,
│   │            exactly like GemSprites.kt)
│   └── Icon/    icon.png (512×512, re-exported from assets/icon.jpg)
└── Tests/       NUnit project, mirrors app/src/test, runs via dotnet test
```

Note: SwapIntent/BufferedSwapGuard live in `Engine/Rules/` (not `View/` as the
plan's §1 tree sketched) because they are pure logic with zero Godot
dependency — `dotnet test` must be able to cover them (plan §4's testability
intent). They are conceptually view-layer input plumbing.

## View layer (G2)

- `Game.cs` autoload replaces GameViewModel: engine + board + input lock +
  ordered op queue, drained by ONE consumer task (Kotlin commit c45deee
  lesson: two coroutines touching the same GemActor cancel each other's
  Tweens and wedge the phase). Buffered swaps use `BufferedSwapGuard`;
  stale-Hypercube intents are dropped, most-recent wins otherwise.
  G3: StateFlow -&gt; Godot signals — ScoreChanged / TimerChanged /
  RoundEnded(reason, score) / RoundStarted; a generation counter makes
  in-flight callbacks from a previous round no-op. Classic 75s timer runs on
  SceneTreeTimer; Restart() resets the round.
- `Hud.cs` (CanvasLayer): score / timer / mode labels; subscribes to Game
  signals.
- `GameOverScreen.cs` (CanvasLayer, in Game.tscn): dims the board, shows
  reason + score, Play again -&gt; Restart(). High-score UI lands in G5.
- G5: `MenuScreen.cs` / `Scenes/Menu.tscn` (main scene) — Classic/Zen with
  per-mode high scores; mode is passed via `Game.StartRound(mode)`, which
  restarts the round and switches scenes. `GameOverScreen` gains Best +
  New-high-score + Menu button. `HighScoreStore` (Engine/Data) persists
  user://highscores.json (System.Text.Json) with an injected directory so
  it runs under dotnet test. HUD gains the exit-to-menu confirm dialog
  (AcceptDialog).
- G4 is fully covered by G1/G2 code (SpecialRules + SpecialComboTest shipped
  in G1, special marks in G2); `--selftest-special`/`--selftest-hypercube`
  verify Flame+Flame (250 pts) and H+H (810 pts + regeneration) end-to-end.
- `BoardView.cs` (Node2D): draws board bg/grid/selection in `_Draw`, owns the
  StepPlayer, handles input — drag past 40% of a cell commits the directional
  swap; tap-tap select-adjacent fallback (MECHANICS.md). Both touch and mouse
  events are handled (mouse for desktop testing).
- `StepPlayer.cs`: gem-id → GemActor map + logical id grid; sequences steps
  with `async`/`await` on `ToSignal(tween, Finished)`; parallel effects via
  `Task.WhenAll`. Timing constants ported from StepPlayer.kt:
  swap 150ms, destroy 200ms, fall 90+70/row ms (simultaneous landing).
- `GemActor.cs` on `Scenes/GemActor.tscn` (Sprite2D base + Flame/Star overlay
  art + baked silhouette outline); stable gem ids, same as Kotlin.
- `GemSprites.cs`: loads the 9 PNGs once; bakes the alpha-traced silhouette
  outline (Kotlin traceAlpha + stroke) into textures at load time.
- Easing: Kotlin's FastOutSlowIn maps to Godot's Cubic+Out (closest built-in
  transition; no bezier curves in the Tween API).

## Heads-up: `ToSignal` returns `SignalAwaiter`, not `Task`

Godot's C# `ToSignal(...)` yields a `SignalAwaiter` — awaitable but not a
`Task`, so it cannot be returned from a `Task`-typed method or used with
`Task.WhenAll`. Wrap it: `public async Task X() { ... await ToSignal(...); }`.

## Engine porting notes (behavioral drift watchlist)

- `Board` copies: `Gem` is a `readonly record struct`, so `Gem?[,]` clones are
  automatically deep — `WithSwapped`/`WithGem` allocate a fresh grid, never
  mutate (GODOT_PORT_PLAN §7 highest-risk item).
- Kotlin smart casts do not port: nullable `Special?` / `Gem?` need explicit
  patterns (`is { Special: { } s }`, `.Value`).
- Kotlin allows `firstOrNull` on value types; C# `FirstOrDefault` can't
  distinguish "not found" from cell (0,0) — `PositionOf` and birth-cell
  selection search explicitly.
- Kotlin trailing commas in argument lists are legal; C# rejects them.
- `SeededRandom` wraps `System.Random` (int seed). Deterministic within this
  port, not bit-identical to Kotlin's `kotlin.random.Random` (no cross-language
  replay required).
- `System.Random` differs from Kotlin RNG → exact gem sequences differ from
  the Kotlin build; all parity-critical tests are structural, not RNG-value
  dependent.

## Verification

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

### Self-test exit codes

All self-tests run headless with `--` separated user args; exit code 0 = pass.
Each prints `SELFTEST-OK-...` and throws on any invariant violation.

- `dotnet test godot/Tests` — engine rules, no Godot runtime (green: 87 tests,
  incl. HighScoreStore with a temp-dir store).
- Godot editor: `--import` + `--build-solutions` verified under 4.7.2 mono;
  autoload runs ("MatchThree autoload ready" at startup).
- Headless self-tests (user args, exit code 0 = pass):
  - `--selftest-swap`: play one legal swap end-to-end; settled 81/81, 0 matches.
  - `--selftest-reject`: illegal swap animates there-and-back; phase to Idle.
  - `--selftest-burst=N`: N rapid submits through input lock (buffer, no
    wedges; drained to a settled, invariant-clean board).
  - `--selftest-timer`: 2s Classic timer -&gt; round ends ("Time's up!") with
    phase GameOver, then Restart() resets to Idle with a fresh round.
  - `--selftest-special`: two adjacent Flames -&gt; Flame+Flame combo
    (25 cells x 10 = 250 first round), settles invariant-clean.
  - `--selftest-hypercube`: two adjacent Hypercubes -&gt; 81-cell clear
    (810 pts) + immediate regeneration, no specials left.
  - `--selftest-menu`: Zen round started via StartRound; mode wired, no
    timer, board settled.
- Board simulation metrics (150 boards, 9x9/6): avg legal moves ≈ 18.8,
  random-swap match probability ≈ 0.13 — same ballpark as the Kotlin build.