# Mechanics — Match Three

Concrete rules. If a behavior isn't listed here, it's unspecified — add it before implementing.

## Board

- **9×9, 6 gem types** — locked as initial constant, tunable later. Validated via simulated engine tests (random-swap match probability, average legal-move count). Player-feel tuning deferred to a later tuning pass.
- Swaps: orthogonal adjacency only, via **drag-to-swap** (threshold ~40% of cell size measured at runtime). Tap-tap fallback supported. Both gestures are **mutually exclusive**: any committed swipe clears a pending tap-tap selection, and a committed tap-tap swap clears the selection (invalid swaps included — the marker never survives a swap attempt). Starting a new round ("Play again") also clears the selection.
- **Input lock:** the engine ignores new swaps while resolving steps. Drag gestures during lock are **buffered** (most recent only) and executed after resolution — no silent drops. Exception: if a **Hypercube entered or left** the buffered swap's two cells during the resolution (by gem id), the intent is **stale and dropped** — a Hypercube is only ever consumed by a gesture made while that Hypercube already sat in the swapped pair.
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
- **Chain-detonation rule:** when a **Flame** or **Star** is cleared by a blast/line effect (including one swept up by a combo), it detonates in the same step; chain recursively until no new Flame/Stars are hit.
- **Blast-hit Hypercube rule:** when a detonating Flame or Star clears a Hypercube, that Hypercube activates once using the detonator's color.
- **Hypercube trigger:** has no color; swapping with a normal gem clears all gems of the swapped color. Outside the blast-hit rule above, a Hypercube only activates on a gesture that targeted it — see Input lock (a stale buffered swap never consumes a newly-arrived Hypercube).

## Combos

Fire when a player swap swaps two specials. Emits `Step.ComboActivate(specialA, specialB, affectedCells)` before normal `Destroy → Fall → Spawn`. The swapped pair is consumed by the combo effect itself; any other Flame/Star swept up by that combo then follows the chain-detonation rule above.

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
- Reshuffle: Fisher–Yates shuffle of the full gem multiset **in place, specials included**. Re-validate; retry up to 20×; on persistent failure, the round ends (decisions log: dead board + failed reshuffle = game over).

## Game over

- **Classic mode:** ends on timer expiry. Timer spec: **75 s per round**, a tunable constant (`Game.ClassicTimerSeconds` in `View/Game.cs`); no time bonuses in v1 (candidate tuning item for later).
- **Zen mode:** ends only when the board is dead AND reshuffle has failed after 20 retries.
- Both modes: a dead board (no legal moves) triggers a reshuffle; if the reshuffle cannot find a playable layout after 20 attempts, the round ends.

## Explicit non-goals for v1

- No mid-session persistence (only high scores via `HighScoreStore`, a JSON file).
- No anti-frustration mechanics (no hints, no guaranteed specials).
- No special-creation bonus scoring.
- No Android lifecycle handling mid-animation (rotation/process-death loses round).
