# Match Three — Design Doc

Bejeweled-style match-three game for Android. Local APK builds only, no store.

## Tech stack

- **Kotlin + Jetpack Compose** — no game engine; grid + sprites are well within Compose's capabilities. Keeps the APK small and the build fast. **Rendering is single-`Canvas`-first** (all 81 gem positions driven from animatable state); per-composable-per-gem is the perf trap and is fallback only.
- **Gradle / AGP** — `./gradlew assembleDebug`, `adb install`.
- **JUnit** — engine is pure Kotlin with no Android imports; game logic is JVM-testable.

## Mechanics

### Board

- **9×9, 6 gem types** — locked as the initial constant. Still tunable, but validated via **simulation in JVM unit tests** (probability a random swap yields a match; average legal-move count). "Moves-per-second feel" is explicitly deferred to post-Milestone 2 tuning with real input, not a gate on board size.
- Orthogonal adjacent swaps only, via **drag-to-swap**: touch a gem and drag in one of 4 directions past a threshold (~40% of cell size, measured at runtime against actual gem-cell dimensions). Tap-tap fallback supported.
- **Input is locked while the engine is resolving steps** — no false drags during cascade animations.
- Invalid swaps animate there-and-back over ~150ms.

### Matching

- Horizontal/vertical runs of 3+.
- Resolve loop: match → clear → gravity → refill → re-check (cascade) → repeat until stable. Cascade depth starts at 1.

### Special gems

| Pattern | Special | Effect |
|---|---|---|
| 4 in a row | **Flame** | Explodes 3×3 centered on itself |
| T or L shape | **Star** | Clears full row + full column |
| 5 in a row | **Hypercube** | Wild — see trigger rule |

- **Hypercube trigger:** has no color. Swapping with a normal gem clears **every gem matching the swapped gem's color**.

### Combos (fire when a player swap swaps two specials)

| Combo | Effect |
|---|---|
| Flame + Flame | Single 5×5 explosion |
| Flame + Star | 3-wide row **and** 3-wide column (thick cross) through swap point |
| Star + Star | Clears the row and column of **each** swapped cell (2 rows + 2 columns, deterministic) |
| Flame + Hypercube | All gems of swapped color become Flames, then detonate simultaneously |
| Star + Hypercube | All gems of swapped color become Stars, then clear simultaneously |
| Hypercube + Hypercube | Clears entire board |

Combos emit `Step.ComboActivate(specialA, specialB, affectedCells)` before falling through to normal `Destroy → Fall → Spawn`.

### Scoring

- Base: **10 points** per cleared gem.
- Cascade multiplier: linear by depth, uncapped (`multiplier = cascadeDepth`).
- Specials clear at the same base rate; no separate special-bonus in v1 (flagged as a future tuning candidate).
- `stepScore = gemsClearedThisStep * 10 * cascadeDepth`; total = sum over the cascade chain.

### Board invariants & reshuffle

- Fresh boards and refills: **no pre-existing match**, **≥1 legal move**.
- Generation: place left-to-right, top-to-bottom, excluding colors that would complete a run of 3; then legal-move detection; regenerate if degenerate.
- Reshuffle: Fisher–Yates shuffle of the full gem multiset **in place, specials included** (decision: specials move too — simpler, keeps counts). Re-validate; retry up to 20×; on persistent failure, fall back to full regeneration.

### Explicit non-goals for v1

- No mid-session persistence (only high scores via DataStore).
- No anti-frustration mechanics (no hints, no guaranteed specials).
- No special-creation bonus scoring.

## Architecture

```
UI (Compose)                     Engine (pure Kotlin)
─────────────────                ─────────────────
Canvas board, HUD,               GameEngine: swap → detect →
GameViewModel (StateFlow)   ◄──  resolve → gravity → refill
                                 Emits ordered Step events:
                                 Swap / Match / ComboActivate /
                                 Destroy / Fall / Spawn / Score
```

Engine resolves input into an **ordered list of Steps**; the ViewModel applies steps and sequences animations. Gems carry stable `id`s for animation tracking.

### Package layout

```
app/src/main/java/com/matchthree/
├── game/                      // pure Kotlin, JVM-testable
│   ├── model/   Gem, GemType, Special, Position, Board
│   ├── engine/  GameEngine, MatchDetector, Gravity, Refill, Scorer, Step
│   └── rng/     SeededRandom
├── ui/
│   ├── game/    Board (Canvas), Hud, animation playback
│   ├── screens/ MenuScreen, GameScreen, GameOverScreen
│   ├── GameViewModel.kt
│   └── MainActivity.kt
└── data/        HighScoreStore (DataStore)
```

### Milestones

1. Engine: board, swap validation, match detection, gravity, refill — with tests (including simulated board validation of 9×9/6)
2. UI: single-Canvas board render, drag-to-swap, animations
3. Cascade loop, scoring, HUD, timer
4. Special gems + combos
5. Menus, modes, high scores, polish
