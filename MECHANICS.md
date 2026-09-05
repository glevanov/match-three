# Mechanics — Match Three

Concrete rules. If a behavior isn't listed here, it's unspecified — add it before implementing.

## Board

- **9×9, 6 gem types** — locked as initial constant, tunable later. Validated via simulated JVM tests (random-swap match probability, average legal-move count). Player-feel tuning deferred to post-Milestone 2.
- Swaps: orthogonal adjacency only, via **drag-to-swap** (threshold ~40% of cell size measured at runtime). Tap-tap fallback supported.
- **Input lock:** the engine ignores new swaps while resolving steps. Drag gestures during lock are **buffered** (most recent only) and executed after resolution — no silent drops.
- Invalid swaps animate there-and-back (~150ms).

## Matching

- Matches: horizontal/vertical runs of 3+.
- Resolve loop: match → clear → gravity → refill → re-check (cascade) → repeat until stable. Cascade depth starts at 1.

## Special gems

| Pattern | Special | Effect |
|---|---|---|
| 4-in-row | **Flame** | Explodes 3×3 centered on itself |
| T or L shape | **Star** | Clears full row + full column |
| 5-in-row | **Hypercube** | Wild — see trigger rule |

- **Birth rule:** runs sharing cells form one **shape** (a T/L is one shape of two intersecting runs); **each shape births one special** — one gem from the winning pattern of that shape **transforms**, the rest clear normally. Non-overlapping shapes in the same cascade round each birth their own special. Hypercube stays colorless.
- **Precedence:** 5-in-row > T/L > 4-in-row > plain 3, applied within a shape. Max shape wins when multiple patterns share a cell; shapes that share no cells resolve independently. Deterministic and testable.
- **Cascade rule:** a special caught in any later cascade match **detonates**; no silently-destroyed specials.
- **Hypercube trigger:** has no color; swapping with a normal gem clears all gems of the swapped color.

## Combos

Fire when a player swap swaps two specials. Emits `Step.ComboActivate(specialA, specialB, affectedCells)` before normal `Destroy → Fall → Spawn`.

| Combo | Effect |
|---|---|
| Flame + Flame | Single 5×5 explosion |
| Flame + Star | 3-wide row + 3-wide column (thick cross) through swap point |
| Star + Star | Clears row + column of **each** swapped cell (2 rows + 2 columns) |
| Flame + Hypercube | All gems of swapped color become Flames, then detonate simultaneously |
| Star + Hypercube | All gems of swapped color become Stars, then clear simultaneously |
| Hypercube + Hypercube | Clears entire board, then **immediate regeneration** (re-run invariant checks) |

## Scoring

- Base: **10 points** per cleared gem.
- Cascade multiplier: linear by depth, uncapped.
- Unique-cell scoring: `gemsClearedThisStep` = count of **unique cleared positions** (Set<Position>). Overlapping regions never double-count; add a unit test for this.
- `stepScore = uniqueCells * 10 * cascadeDepth`; total = sum over cascade chain.
- No special-creation bonus in v1 (future tuning candidate).

## Board invariants & reshuffle

- Invariant checks (no pre-existing match, ≥1 legal move) run **only after a cascade fully settles**, not per refill step inside a loop.
- Generation: place left-to-right, top-to-bottom, excluding colors that would complete a run of 3; then legal-move detection; regenerate if degenerate.
- Reshuffle: Fisher–Yates shuffle of the full gem multiset **in place, specials included**. Re-validate; retry up to 20×; on persistent failure, fall back to full regeneration.

## Game over

- **Classic mode:** ends on timer expiry. Timer spec (defined in M3): **75 s per round**, a tunable constant (`GameViewModel.CLASSIC_TIMER_SECONDS`); no time bonuses in v1 (candidate tuning item for later).
- **Zen mode:** ends only when the board is dead AND reshuffle has failed after 20 retries.
- Both modes: a dead board (no legal moves) triggers a reshuffle; if the reshuffle cannot find a playable layout after 20 attempts, the round ends.

## Explicit non-goals for v1

- No mid-session persistence (only high scores via DataStore).
- No anti-frustration mechanics (no hints, no guaranteed specials).
- No special-creation bonus scoring.
- No Android lifecycle handling mid-animation (rotation/process-death loses round).
