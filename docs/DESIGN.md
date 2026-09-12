# Match Three — Design

Bejeweled-style match-three game for Android (local APK, no store) written in
**Godot 4.7.2 (.NET/Mono) + C#**. The original app's sources were removed at
cutover and survive only in git history. Game rules live in
[MECHANICS.md](MECHANICS.md); settled decisions live in
[DECISIONS.md](DECISIONS.md).

## Tech stack

- **Godot 4.7.2 stable (.NET build), C# / .NET 9** — pinned; the `.csproj`
  (`Godot.NET.Sdk/4.7.2`), `project.godot` feature tag and editor version must
  not drift apart. The game, `Engine/` and `Tests/` all target `net9.0`, so
  every build needs the .NET 9 SDK/runtime. Godot's generated template would
  keep an older TFM on desktop and use `net9.0` only for Android
  (`GodotTargetPlatform=android`); this project deliberately uses one TFM
  everywhere — Android requires `net9.0` (Godot 4.5+, and only the .NET 9
  Mono runtime packs are 16 KB page aligned) and the desktop/editor Godot
  host rolls forward to the installed .NET 9 runtime. Details and the
  verification recipe live in
  [NOTES.md](NOTES.md#16-kb-page-alignment-resolved-in-the-net-9-android-target).
- **`Engine/`** — pure C# class library, **zero `Godot` namespace dependency**.
  Builds and tests with `dotnet build` / `dotnet test` alone. Excluded from
  the game assembly via `<Compile Remove>` in `MatchThree.csproj` and
  referenced as a project.
- **NUnit** for engine tests (`Tests/`), run with `dotnet test`.
- Records + sealed record hierarchies + switch pattern matching for value
  equality and exhaustive `switch` analysis.
- Art: the 8 PNGs in `Assets/Sprites/` (six colors — Orange now uses its
  real art, downscaled from the original `assets/orange.png` — plus
  hypercube/star_06) and the procedural effects in `Assets/Shaders/`
  (`gem_fire.gdshader` flame aura, `gem_star.gdshader` star bloom).
  `Assets/Icon/icon.png` is re-exported from the original `assets/icon.jpg`.
  Board backgrounds: 4 night-sky photos in `Assets/Backgrounds/`, one shown
  per round behind the board (random pick, `View/Background.cs`; provenance:
  ASSET_SOURCES.md).
- Audio: `Assets/Audio/` — `music.mp3` (looping board-music bed, imported
  with `loop=true`), `swipe.mp3` (swap whoosh), `pop.mp3` (gem-clear pop,
  pitched per cascade), `flame.mp3`/`star.mp3`/`hypercube.mp3` (special
  detonation SFX — Flame whoosh, Star sweep, Hypercube impact) and
  `birth.mp3` (glockenspiel chime when a special gem is born). Provenance
  and source links: [ASSET_SOURCES.md](ASSET_SOURCES.md); play policy:
  DECISIONS.md "Audio (v1)".

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
track them through falls and spawns. State flows to the UI via Godot signals;
step playback uses `async`/`await` on `ToSignal(tween, Finished)`.

## Package layout

```
match-three/
├── project.godot                ← Godot 4.7.2, C#, mobile renderer, 540×960
├── MatchThree.csproj/.sln       ← game assembly (Godot.NET.Sdk), refs Engine
├── Engine/                      ← pure C# rules, no Godot dependency
│   ├── Model/   Board, Gem, GemType, Special, Position, BoardConfig
│   ├── Data/    GameMode, HighScoreStore (user://highscores.json),
│   │            SettingsStore (user://settings.json — music toggle)
│   └── Rules/   GameEngine (its `Resolution` record is declared at the
│                bottom of GameEngine.cs), MatchDetector, Gravity, Refill,
│                Scorer, SpecialRules, BoardGenerator, LegalMoveDetector,
│                SeededRandom, IdSource, Step, Match, SwapIntent,
│                BufferedSwapGuard
├── View/        Game.cs (autoload) + Game.SelfTests.cs (headless
│                --selftest-* checks), BoardView.cs, StepPlayer.cs,
│                GemActor.cs, GemSprites.cs, BoardOp.cs, Hud.cs,
│                GameOverScreen.cs, MenuScreen.cs, DebugMenuScreen.cs,
│                GameAudio.cs (round music + swap sfx), Background.cs
│                (per-round random night-sky photo behind the board),
│                SafeArea.cs, DebugStarGlow.cs / DebugSounds.cs
│                (debug screens; NOTES.md)
├── Scenes/      Menu.tscn (main), DebugMenu.tscn, Game.tscn,
│                GemActor.tscn, DebugStarGlow.tscn / DebugSounds.tscn
│                (debug only; NOTES.md)
├── Assets/
│   ├── Sprites/ the 8 gem/special PNGs (copied from the old app assets)
│   ├── Backgrounds/ 4 night-sky photos, one per round behind the board
│   │            (random pick per round; sources: ASSET_SOURCES.md)
│   ├── Audio/   music.mp3 (looping board bed) + swipe.mp3 (swap whoosh)
│   │            + pop.mp3 (gem-clear pop) + flame/star/hypercube.mp3
│   │            (special detonations) + birth.mp3 (birth chime);
│   │            sources: ASSET_SOURCES.md
│   ├── Shaders/ gem_fire.gdshader (flame aura), gem_star.gdshader
│   │            (star bloom) — visuals per DECISIONS.md
│   └── Icon/    icon.png (512×512, re-exported from assets/icon.jpg)
├── Tests/       NUnit project, runs via dotnet test (no Godot runtime)
├── assets/      original source art (icon.jpg, cat_with_gem.jpeg, PNGs)
└── docs/        MECHANICS.md (rules), DECISIONS.md (decision log),
                 NOTES.md (known issues), ASSET_SOURCES.md (third-party
                 asset provenance: source URLs + license status)
```

## View layer

- `Game.cs` autoload: engine + board + input lock + ordered op queue, drained
  by ONE consumer task (two concurrent writers on one GemActor cancel each
  other's Tweens and wedge the phase, so animations are serialized through one
  consumer). Buffered swaps use `BufferedSwapGuard`; stale-Hypercube intents
  are dropped, most-recent wins otherwise. Signals: ScoreChanged /
  TimerChanged / RoundEnded(reason, score) / RoundStarted; a generation counter
  makes in-flight callbacks from a previous round no-op.
- `BoardView.cs` (Node2D): board bg/grid/selection in `_Draw`, owns the
  StepPlayer, handles input — drag past 40% of a cell commits the directional
  swap; tap-tap select-adjacent fallback (MECHANICS.md). Touch and mouse.
- `StepPlayer.cs`: gem-id → GemActor map + logical id grid; sequences steps
  with `async`/`await` on `ToSignal(tween, Finished)`; parallel effects via
  `Task.WhenAll`. Timing: swap 150ms, destroy 200ms, fall 90+70/row ms.
- `GemActor.cs` on `GemActor.tscn`: base sprite plus per-special effects —
  Flame: `gem_fire.gdshader` body aura only (FireRing rect, tinted from
  the gem's own art; center-icon art retired, DECISIONS.md); Star:
  30%-alpha star_06 overlay (no outline) plus the pulsing additive
  `gem_star.gdshader` bloom (StarGlow rect). The effect rects are scaled
  up (1.35×/1.5×) so halos bleed past the silhouette, with in-shader UV
  remap keeping the alpha masks aligned 1:1 (DECISIONS.md).
- `Hud.cs` / `MenuScreen.cs` / `DebugMenuScreen.cs` / `GameOverScreen.cs`:
  HUD strip, mode menu with per-mode high scores plus one Debug entry that
  opens a submenu for the gem/sound debug screens, and the game-over overlay
  (Best + New-high-score + Play again / Menu). Exit-to-menu goes through a
  confirm dialog.
- `Background.cs` on Game.tscn: full-screen night-sky photo on a CanvasLayer
  at layer -10 (behind board/HUD/overlay), new random photo per round; the
  menu scene has no such layer, so photos only show while the board is up.
- Easing note: Godot's Cubic+Out (fast-out-slow-in style).

## Engine notes (behavioral drift watchlist)

- `Board` copies: `Gem` is a `readonly record struct`, so `Gem?[,]` clones are
  automatically deep — `WithSwapped`/`WithGem` allocate a fresh grid, never
  mutate.
- Nullable `Special?`/`Gem?` need explicit property patterns
  (`is { Special: { } s }`, `.Value`).
- `FirstOrDefault` can't express "not found" for a value-type Position —
  `PositionOf` and birth-cell selection search explicitly.
- Trailing commas in argument lists are illegal in C#.
- `ToSignal` returns `SignalAwaiter`, not `Task` — wrap: `async Task` method
  that awaits it (needed for `Task.WhenAll`).

## Verification

- `dotnet test Tests/` — engine rules, no Godot runtime.
- Godot editor: `--import` + `--build-solutions` verified under 4.7.2 mono.
- Headless self-tests (user args after `--`, exit 0 = pass):
  `--selftest-swap`, `--selftest-reject`, `--selftest-burst=N`,
  `--selftest-timer`, `--selftest-special`, `--selftest-hypercube`,
  `--selftest-menu`, `--selftest-flow` (basic round lifecycle: HUD labels,
  view/engine sync, menu round-trip, game-over overlay, Play again).
  On Android, `scripts/selftest-android.sh --selftest-*` builds the current
  sources, patches them into the debug export and runs the test on the
  connected device (no Godot binary required); `--selftest-flow` is the
  regression test for the round lifecycle across menu round-trips.
- Board simulation metrics (150 boards, 9x9/6): avg legal moves ≈ 18.8,
  random-swap match probability ≈ 0.13.

### Rule verification checklist (MECHANICS.md rule -&gt; where it is verified)

Every rule below is verified by the test suite (plain `dotnet test`)
and/or a headless self-test; anything needing a human hand is called out
explicitly.

| MECHANICS.md rule | Verification |
|---|---|
| 9x9/6 board, tunable | BoardGeneratorTest (size + invariants); simulation metrics |
| Drag-to-swap, ~40% cell threshold; tap-tap fallback | BoardView.cs; touch feel needs a device |
| Input lock: buffer most-recent, no drops | --selftest-burst=10/20; drain-loop single consumer |
| Hypercube entered/left pair = stale drop | BufferedSwapGuardTest |
| Invalid swap there-and-back ~150ms | --selftest-reject (phase returns to Idle) |
| Matches: max H/V runs of 3+ | MatchDetectorTest |
| Cascade: match, clear, gravity, refill, re-check; depth 1+ | GameEngineTest (ordered steps, depth alternation) |
| Flame 3x3 / Star row+col / Hypercube colorless | SpecialComboTest (pure + integration) |
| Birth: per shape, one special, rest clear | SpecialComboTest per-shape-group tests |
| Precedence 5 &gt; T/L &gt; 4 &gt; 3 | SpecialComboTest precedence cases |
| Cascade-/blast-swept specials chain-detonate; blast-hit Hypercubes trigger on the detonator color | SpecialComboTest.flameSwept + SpecialComboTest.flameBlastChains + engine round loop |
| Hypercube trigger (plain/special partner) | SpecialComboTest + --selftest-hypercube |
| Six combos + ComboActivate before Destroy | SpecialComboTest (all six, step order) |
| H+H full clear + immediate regeneration | SpecialComboTest + --selftest-hypercube (810 pts, no specials) |
| Scoring 10/gem, linear depth, unique cells | ScorerTest |
| No special-creation bonus | Scorer has no such path (by construction) |
| Invariants checked only after settle | Engine loop structure + settled-board assertions |
| Generation: no pre-match, legal move, regenerate | BoardGeneratorTest (50 boards) |
| Reshuffle Fisher-Yates, ids/types preserved, 20 retries | GameEngineTest.reshuffle (multiset preserved, null on dead) |
| Classic 75 s timer ends round; failed reshuffle ends either mode | --selftest-timer (Time's up! -&gt; GameOver -&gt; Restart); Game.cs Attach (null reshuffle -&gt; EndGame) |
| Round lifecycle across menu round-trips: live HUD labels, view/engine board sync, game-over overlay, Play again resync | --selftest-flow (device run via scripts/selftest-android.sh); negative-checked by reverting the fix |
| Zen: ends on dead board + failed reshuffle | Engine test (reshuffle null) + Attach wiring; E2E needs a contrived dead board |
| Non-goals (no hints/bonus/mid-session persistence) | Honors by construction; only highscores persist |

Verification notes:
- All rule tests are structural; none depend on exact RNG values.
- MECHANICS.md was edited (one line) to say a failed reshuffle ends the
  round, matching the decision log ("dead board + failed reshuffle"
  game-over).
- Manual checks still open: touch feel (drag threshold, rejection bounce),
  HUD/gem visuals on a device, frame pacing on worst-case clears
  (Godot's profiler + Performance monitors).