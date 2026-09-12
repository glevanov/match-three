# Match Three — Design

Architecture, constraints, and repository layout for the project. Keep this
file focused on how the game is put together. Put gameplay rules in
[MECHANICS.md](MECHANICS.md), settled product choices in
[DECISIONS.md](DECISIONS.md), and live issues or tooling gotchas in
[NOTES.md](NOTES.md).

Bejeweled-style match-three game for Android (local APK, no store) built with
**Godot 4.7.2 (.NET/C#)**. The original app's sources were removed at cutover
and survive only in git history.

## Tech stack

- **Godot 4.7.2 stable (.NET build), C#, `net9.0` everywhere.** Keep
  `project.godot`, `Godot.NET.Sdk`, and the editor/runtime version aligned.
  Android/device builds require .NET 9; see [NOTES.md](NOTES.md).
- **`Engine/` is pure C#.** It has no `Godot` dependency and must build and
  test under plain `dotnet`.
- **`MatchThree.csproj` is the Godot-facing game assembly.** It references the
  engine project and contains the view, scenes, and presentation code.
- **NUnit** backs the engine test suite in `Tests/`.
- **Assets** live in `Assets/`; original source art is preserved in `original-art/`.
  Asset provenance is tracked in [ASSET_SOURCES.md](ASSET_SOURCES.md).

## Architecture

```text
View (Godot nodes)                 Engine (pure C#, Engine/)
────────────────────               ────────────────────────
Game.cs autoload             ◄──   GameEngine
BoardView / StepPlayer              swap → detect → resolve → gravity → refill
GemActor / HUD / Audio              emits ordered Step events:
                                    Swap / ComboActivate / SpecialBirth /
                                    Destroy / Fall / Spawn / Score / Settled
```

The engine is authoritative for rules and board state. It resolves a player
intent into an ordered `List<Step>` and the resulting board state.

The view is authoritative only for presentation. It replays engine steps as
animation instead of mutating a separate long-lived board model on its own.

Gems carry stable `Id`s so `GemActor`s can track the same gem through falls,
spawns, and special handling.

`Game.cs` is the bridge between the two sides: it owns round lifecycle,
input-lock behavior, buffered swaps, and signal fan-out to the UI.

## Design constraints

- **Engine stays Godot-free.** Rules belong in `Engine/`, not in the view.
- **State crosses the boundary as ordered steps.** Do not rely on ad-hoc view
  mutations to represent gameplay.
- **Playback is serialized through one consumer task.** One board actor should
  not have competing animation writers.
- **Randomness flows through `SeededRandom`.** Determinism matters for tests
  and reproducibility.

## Package layout

```text
match-three/
├── project.godot                ← Godot project config
├── MatchThree.csproj/.sln       ← game assembly; references Engine/
├── Engine/                      ← pure C# rules and data
│   ├── Model/                   ← Board, Gem, Position, Special, config
│   ├── Rules/                   ← GameEngine, matching, gravity, refill,
│   │                              scoring, board generation, legal moves,
│   │                              RNG, ids, steps, swap buffering
│   └── Data/                    ← GameMode, HighScoreStore, SettingsStore
├── View/                        ← Game autoload, board rendering/input,
│                                  step playback, gem actors, audio, UI,
│                                  debug screens, self-test entrypoints
├── Scenes/                      ← menu, game, gem actor, debug scenes
├── Assets/                      ← runtime sprites, shaders, audio,
│                                  backgrounds, icon
├── Tests/                       ← NUnit engine tests
├── original-art/                ← original source art kept for reference
├── scripts/                     ← Android export and self-test helpers
└── docs/                        ← mechanics, decisions, notes, provenance
```

## View layer

- **`Game.cs`** owns the engine instance, round state, input lock, buffered
  swap handling, and serial playback of board operations. It exposes score,
  timer, and round-lifecycle signals to the rest of the UI.
- **`BoardView.cs`** lays out and draws the board, handles touch/mouse input,
  and turns gestures into `SwapIntent`s.
- **`StepPlayer.cs`** maps gem ids to `GemActor`s and replays `Step`s with
  asynchronous animation.
- **`GemActor.cs` / `GemSprites.cs`** render base gems and special-gem
  presentation.
- **`GameAudio.cs`, `Background.cs`, and `UiClick.cs`** mirror round or UI
  state into presentation; they do not own gameplay rules.
- **`Hud.cs`, `MenuScreen.cs`, `GameOverScreen.cs`, and `DebugMenuScreen.cs`**
  are thin UI around the autoload state.

## Verification

- **Engine:** `dotnet test Tests/`
- **Headless self-tests:** `Game.SelfTests.cs` exposes `--selftest-*`
  entrypoints for swap, reject, burst, timer, special, hypercube, menu, and
  round-flow checks.
- **Device run:** `scripts/selftest-android.sh --selftest-flow` runs the
  current sources on a connected Android device using the debug-export patch
  flow described in [NOTES.md](NOTES.md).
- **Manual/device checks still matter** for touch feel, visual readability,
  and worst-case clear pacing.
