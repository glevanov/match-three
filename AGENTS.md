# Match Three — Agent Guide

Godot 4.7.2 (.NET/C#) match-three game.

## Repo map

- `Engine/` — pure C# rules and board logic
- `View/` — Godot orchestration, rendering, and UI
- `Scenes/` — Godot scenes
- `Tests/` — NUnit engine tests
- `Assets/` — runtime-loaded assets
- `original-art/` — original source art kept for reference
- `scripts/` — Android export and self-test helpers
- `docs/` — design, mechanics, decisions, notes

## Read first

1. `docs/DESIGN.md`
2. `docs/MECHANICS.md`
3. `docs/DECISIONS.md`

## Hard rules

- **Commit messages:** `<type>(<scope>): <subject>` only. Types: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`.
- **Keep `Engine/` Godot-free.** No `Godot` imports under `Engine/`.
- **Steps, not view-owned state.** Gameplay state crosses to the view as ordered `Step`s.
- **Gem `Id`s stay stable.** Preserve ids across falls, spawns, and carried matches.
- **Use `SeededRandom`.** Randomness must stay reproducible.
- **Test mechanics in the engine first.** Run `dotnet test Tests/`.
- **Android/device builds:** use `scripts/export-android.sh`; do not combine export with `--build-solutions`.
- **Target `net9.0` everywhere.**
- **If rules or decisions change, update both** `docs/MECHANICS.md` **and** `docs/DECISIONS.md` **in the same commit.**
- **One writer per branch/worktree** when delegating.
