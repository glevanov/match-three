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

- `dotnet test godot/Tests` — engine rules, no Godot runtime (green: 81 tests).
- Godot editor: `--import` + `--build-solutions` verified under 4.7.2 mono;
  autoload runs ("MatchThree autoload ready" at startup).
- Headless self-tests (user args, exit code 0 = pass):
  - `--selftest-swap`: play one legal swap end-to-end; settled 81/81, 0 matches.
  - `--selftest-reject`: illegal swap animates there-and-back; phase to Idle.
  - `--selftest-burst=N`: N rapid submits through input lock (buffer, no
    wedges; drained to a settled, invariant-clean board).
  - `--selftest-timer`: 2s Classic timer -&gt; round ends ("Time's up!") with
    phase GameOver, then Restart() resets to Idle with a fresh round.
- Board simulation metrics (150 boards, 9x9/6): avg legal moves ≈ 18.8,
  random-swap match probability ≈ 0.13 — same ballpark as the Kotlin build.