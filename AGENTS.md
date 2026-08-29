# Guide for agents working on Match Three

## Project shape

Android match-three game (Bejeweled-style). Kotlin + Jetpack Compose, no game engine. Built as local APK, no store. Repo: github.com/glevanov/match-three.

## Reading order before touching code

1. `DESIGN.md` — architecture and package layout
2. `MECHANICS.md` — exact game rules (do not improvise)
3. `ROADMAP.md` — milestone you're in

## Non-negotiable conventions

- **Engine is pure Kotlin.** No Android imports under `game/`. It compiles and tests on the JVM.
- **Steps, not state.** Engine emits ordered `Step` events; UI plays them back. Don't mutate a single mutable board in place and render it.
- **Gems have stable `id`s.** Every fall/spawn/carried match keeps the id so animations can track.
- **Seeded RNG.** All randomness flows through `SeededRandom` for reproducible tests.
- **JUnit on engine.** Any new mechanic gets a JVM test (e.g. precedence, unique-cell scoring) before UI integration.
- **One writer per branch.** When delegating, either use a worktree or hand off parameters; agents in this repo coordinate via AGENTS.md.

## When in doubt, check the decision log

ROADMAP.md has a "Decisions log" — consult it before proposing a change to board size, input model, scoring, or game-over rules. If you change a decision, update ROADMAP.md and MECHANICS.md in the same commit.
