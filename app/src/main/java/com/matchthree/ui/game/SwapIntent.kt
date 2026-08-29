package com.matchthree.ui.game

import com.matchthree.game.model.Position

/**
 * A player-intended swap of two orthogonally adjacent cells, normalized so the
 * lower/left cell is always [a]. Pure Kotlin on purpose: JVM-testable, and the
 * engine/UI both consume the same canonical form (so input direction never
 * affects match resolution).
 */
data class SwapIntent internal constructor(
    val a: Position,
    val b: Position,
) {
    companion object {
        /** Builds a normalized [SwapIntent]; throws on non-adjacent cells. */
        fun of(first: Position, second: Position): SwapIntent {
            require(first.isOrthogonallyAdjacentTo(second)) {
                "swap requires orthogonal neighbors: $first, $second"
            }
            val (lo, hi) = if (first < second) first to second else second to first
            return SwapIntent(lo, hi)
        }
    }
}

private operator fun Position.compareTo(other: Position): Int =
    when {
        row != other.row -> row.compareTo(other.row)
        else -> col.compareTo(other.col)
    }