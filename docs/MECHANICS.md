# Match Three — Mechanics

Gameplay rules. If a behavior is not listed here, it is unspecified and
should be added before implementation.

## Board

- The board is **9×9** with **6 gem types**.
- Swaps are orthogonal-adjacent only. Input supports **drag-to-swap**
  (commit at ~40% of a cell) with a tap-tap fallback.
- The two gesture modes are **mutually exclusive**: any committed swipe clears
  a pending tap selection, and any committed tap-tap swap also clears the
  selection, including on an invalid swap. Starting a new round also clears
  the selection.
- **Input lock:** while steps are resolving, the engine ignores new swaps.
  Drag gestures during lock are buffered **most-recent only** and executed
  after resolution.
- Buffered-swap exception: if a **Hypercube entered or left** the buffered
  pair during resolution, that buffered intent is **stale and dropped**.
- Invalid swaps animate there-and-back (~150 ms).

## Matching

- Matches are horizontal or vertical runs of **3+**.
- Resolution loop: **match → clear → gravity → refill → re-check** until the
  board is stable. Cascade depth starts at 1.

## Special gems

| Pattern | Special | Effect |
|---|---|---|
| 4-in-row | **Flame** | Explodes a 3×3 area centered on itself |
| T or L shape | **Star** | Clears its full row and full column |
| 5-in-row | **Hypercube** | Wild; see trigger rule |

- **Birth rule:** runs that share cells form one **shape**. Each shape births
  exactly one special per cascade round: one gem from the winning pattern
  transforms, and the rest clear normally. Non-overlapping shapes in the same
  cascade round each birth their own special. Hypercubes are colorless.
- **Precedence within a shape:** **5-in-row > T/L > 4-in-row > plain 3**.
  When multiple patterns share a cell, the highest-precedence shape wins.
  Disjoint shapes resolve independently.
- **Cascade rule:** a special caught in a later cascade match detonates; it is
  not silently destroyed.
- **Chain-detonation rule:** if a **Flame** or **Star** is cleared by a blast
  or line effect, including by a combo, it detonates in the same step. Chain
  recursively until no new Flame or Star is hit.
- **Blast-hit Hypercube rule:** if a detonating Flame or Star clears a
  Hypercube, that Hypercube activates once using the detonator's color.
- **Hypercube trigger:** a Hypercube has no color. Swapping it with a normal
  gem clears all gems of the swapped color. Outside the blast-hit rule above,
  a Hypercube only activates on a gesture that targeted it.

## Combos

When a player swap swaps two specials, emit
`Step.ComboActivate(specialA, specialB, affectedCells)` before normal
`Destroy → Fall → Spawn` resolution. The swapped pair is consumed by the combo
itself. Any other Flame or Star swept up by that combo then follows the
chain-detonation rule above.

| Combo | Effect |
|---|---|
| Flame + Flame | Single 5×5 explosion |
| Flame + Star | 3-wide row + 3-wide column through the swap point |
| Star + Star | Clears the row and column of **each** swapped cell |
| Flame + Hypercube | All gems of the swapped color become Flames, then detonate simultaneously |
| Star + Hypercube | All gems of the swapped color become Stars, then clear simultaneously |
| Hypercube + Hypercube | Clears the entire board, then immediately regenerates it and re-runs invariant checks |

## Scoring

- Each cleared gem is worth **10 points**.
- The cascade multiplier is linear by depth and uncapped.
- **Unique-cell scoring:** each board cell scores at most once per step, even
  when effects overlap.
- `stepScore = uniqueCells * 10 * cascadeDepth`; total score is the sum over
  the full cascade chain.
- There is **no special-creation bonus**.

## Board invariants and reshuffle

- After a cascade fully settles, the board must have **no pre-existing match**
  and **at least one legal move**. Do not run invariant checks on intermediate
  refill states.
- Board generation fills left-to-right, top-to-bottom, excluding colors that
  would complete a run of 3. Then it checks for a legal move and regenerates
  if the result is degenerate.
- Reshuffle uses a Fisher–Yates shuffle of the full gem multiset, including
  specials. Revalidate after each shuffle. Retry up to **20** times; if none
  produce a playable board, the round ends.

## Game over

- **Classic mode** ends on timer expiry. The timer is **75 seconds** per
  round, with **no time bonuses**.
- **Zen mode** has no timer.
- In either mode, a dead board triggers a reshuffle. If reshuffle cannot
  produce a playable board after 20 attempts, the round ends.

## Explicit non-goals for v1

- No mid-session persistence beyond high scores.
- No anti-frustration mechanics such as hints or guaranteed specials.
- No Android lifecycle recovery mid-animation; rotation or process death
  loses the round.
