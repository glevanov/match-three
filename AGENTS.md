# Guide for agents working on Match Three

## Project shape

Godot 4.7.2 (.NET/C#) match-three game (Bejeweled-style), C# + GodotSharp.
Local APK builds, no store. Repo: github.com/glevanov/match-three.

The original app sources survive only in git history. `assets/` holds the
original source art; `Assets/` holds what the game actually loads.

## Reading order before touching code

1. [DESIGN.md](./docs/DESIGN.md) — architecture and package layout
2. [MECHANICS.md](./docs/MECHANICS.md) — exact game rules (do not improvise)
3. [DECISIONS.md](./docs/DECISIONS.md) — decision log (consult before changing rules)

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
- **Android debug export.** Use `scripts/export-android.sh` for APK device
  builds/deploys (for example `--install --run`). Do **not** combine Godot
  `--export-debug` with `--build-solutions`; that can produce an APK missing
  managed assemblies and crash on startup.
- **Android exports target net9.0.** `MatchThree.csproj` keeps Godot's
  generated conditional TFM: `net8.0` for desktop/editor builds, `net9.0`
  when `GodotTargetPlatform=android`. Godot 4.5+ requires `net9.0` on Android
  (.NET 9 Mono libs are 16 KB page aligned), and Android builds need the
  .NET 9 SDK. Don't flatten the condition, and don't re-add page-size compat
  workarounds (docs/NOTES.md "16 KB page alignment").
- **One writer per branch.** When delegating, either use a worktree or hand off
  parameters; agents in this repo coordinate via AGENTS.md.

## When in doubt, check the decision log

DECISIONS.md records settled decisions — consult it before proposing a
change to board size, input model, scoring, or game-over rules. If you
change a decision, update DECISIONS.md and MECHANICS.md in the same commit.