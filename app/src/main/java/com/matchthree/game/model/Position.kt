package com.matchthree.game.model

import kotlin.math.abs

/** A cell coordinate on the board. */
data class Position(val row: Int, val col: Int) {
    /** True if [other] is one step up, down, left, or right of this position. */
    fun isOrthogonallyAdjacentTo(other: Position): Boolean =
        (row == other.row && abs(col - other.col) == 1) ||
            (col == other.col && abs(row - other.row) == 1)
}