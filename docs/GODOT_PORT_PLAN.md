# Match Three — Godot Port Plan (C#)

Goal: rewrite the Compose/Kotlin implementation in Godot 4 using **C#**
(.NET, GodotSharp) inside a new sibling folder, reusing existing art and
design docs, without touching or deleting anything under `app/` until the
Godot version is a full replacement.

C# over GDScript because the Kotlin source leans on `sealed interface`,
`data class`, exhaustive `when`, and a JUnit suite — C# records, sealed
classes, pattern-matching `switch` expressions, and NUnit/xUnit reproduce
that almost verbatim, which keeps the port mechanical instead of a rewrite.

## 0. What we're starting from

Repo today (confirmed by cloning `glevanov/match-three`):

```
match-three/
├── DESIGN.md / MECHANICS.md / ROADMAP.md / NOTES.md / AGENTS.md   ← source of truth, reuse as-is
├── assets/                     ← original art (icon.jpg, cat_with_gem.jpeg, PNGs for re-export)
├── app/src/main/assets/*.png   ← 9 gem/special sprites actually loaded by the app
└── app/src/main/java/com/matchthree/
    ├── game/model/    Board, Gem, GemType, Position, Special, BoardConfig
    ├── game/engine/   GameEngine, MatchDetector, Gravity, Refill, Scorer,
    │                  SpecialRules, BoardGenerator, LegalMoveDetector,
    │                  SeededRandom, IdSource, Step, Match
    ├── ui/            GameViewModel, BoardCanvas, GemActor, GemSprites,
    │                  StepPlayer, SwapIntent, BufferedSwapGuard, FrameStats
    ├── ui/screens/    MenuScreen, GameScreen, GameOverScreen
    └── data/          HighScoreStore (DataStore)
```

Total ~2,100 lines of Kotlin. The engine is already pure/JVM-testable and
step-driven ("Steps, not state" per AGENTS.md) — that architecture ports
directly into C#, which is what makes this rewrite low-risk.

## 1. Folder isolation strategy

Do **not** touch `app/`, `assets/`, or the Gradle files. Add the Godot
project as a new top-level sibling folder so both implementations
build/run independently until cutover:

```
match-three/
├── app/                     ← untouched, stays buildable
├── assets/                  ← untouched, original source art
├── godot/                   ← NEW: entire Godot project lives here
│   ├── project.godot
│   ├── MatchThree.csproj    ← Godot's generated C# project file
│   ├── MatchThree.sln
│   ├── Engine/              ← pure C# game logic, no Godot/Node dependency
│   │   ├── Model/           Board.cs, Gem.cs, GemType.cs, Position.cs, Special.cs, BoardConfig.cs
│   │   └── Rules/           GameEngine.cs, MatchDetector.cs, Gravity.cs, Refill.cs,
│   │                        Scorer.cs, SpecialRules.cs, BoardGenerator.cs,
│   │                        LegalMoveDetector.cs, SeededRandom.cs, IdSource.cs, Step.cs, Match.cs
│   ├── View/                ← Godot-attached C# scripts (Nodes)
│   │   ├── Game.cs          autoload/singleton, replaces GameViewModel
│   │   ├── BoardView.cs     replaces BoardCanvas
│   │   ├── GemActor.cs
│   │   ├── GemSprites.cs
│   │   ├── StepPlayer.cs
│   │   ├── SwapIntent.cs
│   │   ├── BufferedSwapGuard.cs
│   │   └── HighScoreStore.cs
│   ├── Scenes/              ← .tscn files: Menu.tscn, Game.tscn, GameOver.tscn, GemActor.tscn
│   ├── Assets/
│   │   ├── Sprites/         ← copied from app/src/main/assets/*.png
│   │   └── Icon/            ← copied from assets/icon.jpg
│   ├── Tests/               ← NUnit test project mirroring app/src/test
│   │   └── MatchThree.Tests.csproj
│   └── GODOT_DESIGN.md      ← Godot-specific addendum (see §5)
├── DESIGN.md / MECHANICS.md / ROADMAP.md / NOTES.md / AGENTS.md  ← unchanged, shared docs
└── GODOT_PORT_PLAN.md       ← this file
```

`MECHANICS.md` describes game *rules*, not the Compose implementation, so it
stays the single shared spec for both codebases untouched. `DESIGN.md`
describes the Kotlin/Compose tech stack specifically, so it stays put and a
new `godot/GODOT_DESIGN.md` documents the Godot/C# tech stack in parallel —
only merge them at final cutover.

Only when the Godot build is verified feature-complete (see milestones in §6)
do you delete `app/`, move `godot/*` up to repo root, and fold
`GODOT_DESIGN.md` into `DESIGN.md`.

## 2. Prerequisites

- **Godot 4.x .NET/Mono build** (not the standard build — C# support ships
  in a separate download from godotengine.org).
- **.NET SDK** (Godot 4.x targets .NET 6/8 depending on version — check the
  Godot release notes for the exact SDK version to install).
- This sandbox can scaffold `.cs`/`.csproj`/`.tscn`/`project.godot` files and
  copy the PNG assets, but has no network access to download the Godot
  editor or the .NET SDK (network is allow-listed to package registries and
  GitHub, not godotengine.org or dotnet.microsoft.com). You'll build and run
  the project locally in the Godot editor as it's scaffolded.

## 3. Reusing assets

Copy (don't move) into `godot/Assets/`:

| Source | Destination | Notes |
|---|---|---|
| `app/src/main/assets/red.png, blue.png, green.png, yellow.png, purple.png, orange.png` | `godot/Assets/Sprites/` | 6 gem colors, set Godot import filter to match current aspect-fit-in-cell look |
| `app/src/main/assets/flame.png, sparkle.png` (star) | `godot/Assets/Sprites/` | center-overlay sprites drawn on top of the color sprite, same as `GemSprites.kt` does today |
| `app/src/main/assets/hypercube.png` | `godot/Assets/Sprites/` | has its own full sprite, not an overlay |
| `assets/icon.jpg` (512×512) | `godot/Assets/Icon/icon.png` | re-export as project icon via Godot's export presets (Android adaptive icon later) |
| `assets/cat_with_gem.jpeg` | leave in repo-root `assets/`, don't copy | reference art only, per existing DESIGN.md note — not used by either app |

Check the actual PNGs before setting import filters (pixel-art vs. raster)
— match whatever `GemSprites.kt` currently does when scaling into cells.

## 4. Architecture mapping

Pure engine (no Godot dependency — a plain C# class library, testable
without booting the engine at all, same spirit as "game/ is pure Kotlin"):

| Kotlin | C# equivalent | Notes |
|---|---|---|
| `Position.kt` (`data class`) | `readonly record struct Position(int Row, int Col)` | records give free value equality + `with` expressions, matching Kotlin `data class` copy semantics; `IsOrthogonallyAdjacentTo` becomes an instance method |
| `GemType.kt` (`enum class`) | `enum GemType { Red, Green, Blue, Yellow, Purple, Orange }` | 1:1 |
| `Special.kt` | `enum Special { Flame, Star, Hypercube }` | 1:1 |
| `Gem.kt` (`data class`) | `readonly record Gem(int Id, GemType Type, Special? Special = null)` | nullable `Special?` matches Kotlin's nullable exactly |
| `BoardConfig.kt` | `readonly record BoardConfig(int Width = 9, int Height = 9, int GemTypeCount = 6)` | 1:1, constants as `const` fields |
| `Board.kt` | `sealed class Board` (or plain `class`) wrapping `Gem?[,]` | keep the "returns a new Board, never mutates" contract explicit: `WithSwapped`/`WithGem` deep-copy internally, same as Kotlin — this is the single highest-risk spot in the port (see §7) |
| `IdSource.kt` | `class IdSource` (simple counter) | 1:1 |
| `SeededRandom.kt` | wrap `System.Random` with an explicit seed for deterministic tests | 1:1 |
| `BoardGenerator.kt` | `BoardGenerator.cs` | same left-to-right/top-to-bottom generation + no-pre-existing-match rule |
| `MatchDetector.kt` | `MatchDetector.cs` | same run-detection logic |
| `LegalMoveDetector.kt` | `LegalMoveDetector.cs` | 1:1 |
| `SpecialRules.kt` | `SpecialRules.cs` | birth precedence table (5>T/L>4>3), combo table — port the `when` as a C# `switch` expression |
| `Gravity.kt` / `Refill.kt` | `Gravity.cs` / `Refill.cs` | 1:1 |
| `Scorer.kt` | `Scorer.cs` | unique-cell scoring via `HashSet<Position>` — a direct match for Kotlin's `Set<Position>`, no workaround needed |
| `Match.kt` | `Match.cs` | 1:1 |
| `Step.kt` (`sealed interface` + nested `data class`) | `abstract record Step` with nested `sealed record` subtypes: `Swap`, `ComboActivate`, `SpecialBirth`, `Destroy`, `Score`, `Fall`, `Spawn`, `Settled` | this is the best-case mapping in the whole port — C# sealed record hierarchies + `switch` pattern matching reproduce Kotlin's sealed interface almost keystroke-for-keystroke, including exhaustiveness warnings from the compiler |
| `GameEngine.kt` | `GameEngine.cs` | orchestrates swap→detect→resolve→gravity→refill→re-check exactly as today; returns `List<Step>` |

View layer (Godot Node-attached C# scripts):

| Kotlin (Compose) | Godot/C# equivalent | Notes |
|---|---|---|
| `GameViewModel.kt` (StateFlow) | `Game.cs`, an autoload `Node` exposing Godot **signals** (`[Signal] delegate void BoardChangedEventHandler(...)`) instead of `StateFlow` | |
| `BoardCanvas.kt` (single-Canvas draw) | `BoardView.cs` on a `Node2D`, instancing one `GemActor.tscn` per gem | Godot's node overhead is much lower than Compose's per-composable cost, so per-gem nodes are fine — no need to fight for single-Canvas-only like the Kotlin doc warns; keep board math in `Engine/`, not in the view |
| `GemActor.kt` | `GemActor.cs` on a `Sprite2D` + `Tween`/`AnimationPlayer` | one instanced scene per gem, matched to gem `Id` for continuity across falls/spawns, same as Kotlin's stable-id approach |
| `GemSprites.kt` | `GemSprites.cs` (preloads `Texture2D` per `GemType`/`Special`, composites overlay + base) | keep the "center overlay on color sprite for Flame/Star, standalone sprite for Hypercube" behavior |
| `StepPlayer.kt` | `StepPlayer.cs` using `async`/`await` on `SceneTreeTimer`/`Tween.Finished` (Godot's C# API supports `await ToSignal(...)`) to sequence Steps in order | trickiest file in the port; budget real review time — C#'s `async`/`await` maps well to what Kotlin was doing with coroutines here |
| `SwapIntent.kt` / `BufferedSwapGuard.kt` | `SwapIntent.cs` / `BufferedSwapGuard.cs` | port the "buffer most-recent, drop stale Hypercube swap" logic verbatim — subtle enough to warrant its own unit tests, same as today |
| `FrameStats.kt` / `FrameTimeTracker.kt` | optional — Godot exposes `Performance.GetMonitor(Performance.Monitor.TimeProcess)` and a built-in profiler; only port if you want the same custom p95 measurement harness |
| `MenuScreen.kt`, `GameScreen.kt`, `GameOverScreen.kt` | `Scenes/Menu.tscn`, `Scenes/Game.tscn`, `Scenes/GameOver.tscn` (`Control`-based UI, C# scripts attached) | swap scenes via `GetTree().ChangeSceneToFile(...)` |
| `HighScoreStore.kt` (DataStore) | `HighScoreStore.cs` using `FileAccess` + `System.Text.Json` to/from `user://highscores.json` | keep the same "per-mode high score" shape |
| `MainActivity.kt` / splash / adaptive icon | `project.godot` export presets (Android) + Godot's own splash config | not code, just export settings |
| `Theme.kt`, `colors.xml`, `strings.xml` | a Godot `Theme` resource (`.tres`) + a `Strings` static class or literals if not localizing | |

Tests:

| Kotlin (JUnit, `app/src/test`) | C# equivalent |
|---|---|
| `GameEngineTest`, `MatchDetectorTest`, `GravityTest`, `RefillTest`, `ScorerTest`, `SpecialComboTest`, `SwapValidationTest`, `BoardGeneratorTest`, `BoardSimulationTest`, `SeededRandomTest`, `BufferedSwapGuardTest`, `FrameStatsTest`, `SwapIntentTest`, `HighScoreStoreTest` | `godot/Tests/MatchThree.Tests.csproj`, an NUnit (or xUnit) test project referencing the `Engine/` class library directly — since `Engine/` has zero Godot dependency, these tests run with plain `dotnet test`, no Godot runtime needed at all, mirroring `./gradlew test`'s speed and independence from the Android app |

## 5. `GODOT_DESIGN.md` (new file, parallel to `DESIGN.md`)

Draft contents once the port starts:

- **Tech stack** — Godot 4.x (.NET/Mono build), C#, `Engine/` as a
  Godot-independent class library so the rules can be unit tested with
  `dotnet test` alone; NUnit for tests.
- **Architecture** — same diagram as `DESIGN.md`, relabeled: `Engine/` pure
  C# (no `Godot` namespace dependency) → `Game.cs` autoload (StateFlow →
  Godot signals) → scenes apply Steps and drive animation via `async`/`await`.
- **Package layout** — the `godot/` tree from §1.
- Keep `MECHANICS.md` as the *only* rules doc; don't fork it.

## 6. Milestones (mirrors `ROADMAP.md`, isolated to `godot/`)

- [x] **G0 — Scaffold** — `project.godot`, `.csproj`/`.sln`, folder layout,
      copy assets, add NUnit test project, empty `Game.cs` autoload. Confirm
      `dotnet build` and the Godot editor both pick up the C# project.
- [x] **G1 — Engine core** — port `Board`, `Gem`, `Position`, `BoardConfig`,
      `BoardGenerator`, `MatchDetector`, `Gravity`, `Refill`, `SeededRandom`
      as a pure `Engine/` class library. Port `BoardGeneratorTest`/
      `BoardSimulationTest`/`MatchDetectorTest`/`GravityTest`/`RefillTest`/
      `SeededRandomTest` to NUnit, runnable via `dotnet test` with no Godot
      runtime. Green before moving on.
- [x] **G2 — UI render & input** — `Scenes/Game.tscn`, `GemActor.tscn`,
      `GemSprites.cs`, drag-to-swap (~40% cell threshold) + tap fallback,
      `StepPlayer.cs` sequencing `Swap`/`Destroy`/`Fall`/`Spawn`/`Settled` via
      `async`/`await`. Port `SwapIntentTest`/`BufferedSwapGuardTest`.
- [ ] **G3 — Scoring & timer** — `Scorer.cs`, HUD, Classic 75s timer, dead-
      board reshuffle, `GameOver.tscn` placeholder. Port `ScorerTest`.
- [ ] **G4 — Specials & combos** — `SpecialRules.cs` (birth precedence,
      6 combos, hypercube trigger, cascade-swept detonation, H+H
      regeneration), special marks on gem sprites. Port
      `SpecialComboTest`/`SwapValidationTest`.
- [ ] **G5 — Modes & polish** — `Menu.tscn` (Classic/Zen), mode-wired
      `Game.tscn`, `HighScoreStore.cs`, `GameOver.tscn` real high-score UI.
      Port `HighScoreStoreTest`.
- [ ] **G6 — Parity check** — play both builds side by side against every
      rule in `MECHANICS.md`; fix drift.
- [ ] **G7 — Cutover** — move `godot/*` to repo root, delete `app/`,
      `build.gradle.kts`, `settings.gradle.kts`, `gradle/`, `gradlew*`; merge
      `GODOT_DESIGN.md` into `DESIGN.md`; update `AGENTS.md`/`ROADMAP.md` to
      describe the Godot/C# project going forward.

## 7. Risk notes

- **`Board` copy semantics**: Kotlin's `Board` is deep-copied on every
  mutation (`withSwapped`, `withGem`); a naive C# port using a `Gem?[,]`
  array must be equally deliberate — arrays are reference types in C#, so
  `WithSwapped`/`WithGem` need to allocate and copy a fresh array every time,
  never mutate in place. This is the single biggest source of possible
  behavioral drift from the original engine; consider `ImmutableArray<T>`
  from `System.Collections.Immutable` to make the immutability compiler-
  enforced rather than convention-enforced.
- **`Step` sealed hierarchy**: this is actually the *easy* part in C# —
  `abstract record Step` with `sealed record` subtypes plus a `switch`
  expression on type patterns gives near-exhaustiveness checking, closer to
  Kotlin's `sealed interface` + `when` than most other language pairs would.
- **Async sequencing in `StepPlayer`**: Kotlin's coroutine-based step playback
  becomes C#'s `async Task PlayAsync(IEnumerable<Step> steps)` using
  `await ToSignal(tween, Tween.SignalName.Finished)` between steps — verify
  early that Godot's C# signal-await API behaves the same way under rapid
  cascades (many steps in a row) as it does for a single step, since that's
  where subtle ordering bugs would show up first.
- **GodotSharp version pinning**: the Godot .NET build's API surface (e.g.
  signal codegen, `Node` lifecycle attributes) has changed across 4.x point
  releases — pin the exact Godot version in `GODOT_DESIGN.md` once chosen, so
  the `.csproj` target framework and Godot editor version don't drift apart.
