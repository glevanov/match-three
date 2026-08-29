# Match Three — Design

Bejeweled-style match-three game for Android. Local APK builds only, no store.
Game rules live in [MECHANICS.md](MECHANICS.md); plan and progress in [ROADMAP.md](ROADMAP.md).

## Tech stack

- **Kotlin + Jetpack Compose** — no game engine; keeps the APK small and builds fast. Rendering is **single-`Canvas`-first** (all gem positions driven from animatable state); per-composable-per-gem is a known perf trap, fallback only.
- **Gradle / AGP** — `./gradlew assembleDebug`, `adb install`.
- **JUnit** — the engine is pure Kotlin with no Android imports; game logic is JVM-testable.

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

The engine resolves input into an **ordered list of Steps**; the ViewModel applies steps to state and sequences animations. Gems carry stable `id`s so animations track them through falls and spawns.

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
