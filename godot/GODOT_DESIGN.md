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
│                SeededRandom, IdSource, Step, Match, Resolution
├── View/        Game.cs (autoload placeholder; real wiring in G2)
├── Scenes/      Game.tscn (placeholder main scene for G0; real in G2)
├── Assets/
│   ├── Sprites/ red..white.png, hypercube.png, flame.png, sparkle.png
│   │            (copied from app/src/main/assets; ORANGE renders white.png,
│   │            exactly like GemSprites.kt)
│   └── Icon/    icon.png (512×512, re-exported from assets/icon.jpg)
└── Tests/       NUnit project, mirrors app/src/test, runs via dotnet test
```

Asset import notes: sprites were re-imported by the 4.7.2 editor (`.import`
files are committed). Default filter settings apply; if pixel-art scaling
matters, match what GemSprites.kt does (aspect-fit into cell, nearest
filtering if the source PNGs are pixel art) at G2 render time.

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

- `dotnet test godot/Tests` — engine rules, no Godot runtime (green: 70 tests).
- Godot editor: `--import` + `--build-solutions` verified under 4.7.2 mono;
  autoload runs ("MatchThree autoload ready" at startup).
- Board simulation metrics (150 boards, 9x9/6): avg legal moves ≈ 18.8,
  random-swap match probability ≈ 0.13 — same ballpark as the Kotlin build.