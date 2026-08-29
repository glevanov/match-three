package com.matchthree.game.engine

import com.matchthree.game.model.Position

/**
 * A maximal horizontal or vertical run of 3+ same-type gems.
 * Shape precedence (5-run > T/L > 4-run > 3-run) is Milestone 4.
 */
data class Match(val positions: List<Position>) {
    init {
        require(positions.size >= 3) { "match must cover at least 3 cells" }
    }

    val size: Int get() = positions.size
}