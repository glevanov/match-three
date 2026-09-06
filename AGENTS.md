# Guide for agents working on Match Three

## Project shape

Godot 4.7.2 (.NET/C#) match-three game (Bejeweled-style), C# + GodotSharp.
Local APK builds, no store. Repo: github.com/glevanov/match-three.

This repo IS the Godot port — the Kotlin/Compose implementation was removed at
cutover (docs/GODOT_PORT_PLAN.md, all milestones complete) and survives only
in git history. `assets/` holds the original source art; `Assets/` holds what
the game actually loads.

## Reading order before touching code

1. [DESIGN.md](./DESIGN.md) — architecture and package layout
2. [MECHANICS.md](./docs/MECHANICS.md) — exact game rules (do not improvise)
3. [ROADMAP.md](./docs/ROADMAP.md) — milestone you're in
4. [GODOT_PORT_PLAN.md](./docs/GODOT_PORT_PLAN.md) — migration record and risk notes

## Non-negotiable conventions

- **Conventional Commits.** All commit messages use the conventional format:
  `<type>(<scope>): <subject>`. Types: feat, fix, docs, test, refactor, chore.
- **Engine is pure C#.** No Godot imports under `Engine/`. It compiles and
  tests on plain `dotnet` (`dotnet test Tests/`), no Godot runtime needed.
- **Steps, not state.** Engine emits ordered `Step` events; the view plays
  them back from ONE consumer task (`View/Game.cs` drain loop). Don't mutate a
  single mutable board in place and render it.
- **Gems have stable `Id`s.** Every fall/spawn/carried match keeps the id so
  animations can track.
- **Seeded RNG.** All randomness flows through `SeededRandom` for reproducible
  tests.
- **NUnit on the engine.** Any new mechanic gets a `dotnet test` before UI
  integration (precedent: precedence, unique-cell scoring).
- **One writer per branch.** When delegating, either use a worktree or hand off
  parameters; agents in this repo coordinate via AGENTS.md.

## When in doubt, check the decision log

ROADMAP.md has a "Decisions log" — consult it before proposing a change to
board size, input model, scoring, or game-over rules. If you change a
decision, update ROADMAP.md and MECHANICS.md in the same commit.