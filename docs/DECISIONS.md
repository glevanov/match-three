# Match Three — Decision Log

Settled product-level decisions. Keep this file short and focused on
player-visible or policy-level choices. Put exact rules in
[MECHANICS.md](MECHANICS.md), implementation details in
[DESIGN.md](DESIGN.md), and live issues or tooling gotchas in
[NOTES.md](NOTES.md).

| Decision | Value |
|---|---|
| Board size | The board is 9×9 for now; tunable later. |
| Music | Music is HUD-toggleable, persists across restarts, defaults on, and only plays during active rounds. |
| Input during lock | The most recent drag is buffered instead of dropped; the buffered intent is discarded if a Hypercube entered or left the pair before resolution. |
| Special birth | Each connected match shape births exactly one special per cascade round; the chosen gem transforms and the rest clear. |
| Cascade-/blast-swept specials | Flame and Star chain-detonate when swept by later effects; a blast-hit Hypercube resolves using the detonator's color. |
| Unique-cell scoring | Each cleared board cell scores at most once per step, even when effects overlap. |
| Game over | Classic ends on timer expiry. In either mode, if a dead board cannot be recovered by reshuffle, the round ends. |
| Classic timer | Classic rounds last 75 seconds; no time bonuses yet. |
| RNG | All randomness is seeded and deterministic per session/seed. |
| Flame aura | Flame uses an outer-band aura with a soft halo and no centered flame icon. |
| Star visuals | Star uses a translucent center overlay plus a subtle pulsing bloom. |
| Board background | Gameplay uses a random night-sky photo per round, visible behind the board and the game-over overlay; the menu stays flat. |
| Audio | Music is board-only. Swap whoosh plays only on accepted swaps. Clears, specials, births, and UI buttons each have distinct cues; the clear-pop pitch rises with cascade depth. |
