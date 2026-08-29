package com.matchthree.game.engine

import com.matchthree.game.model.Position

/**
 * Scoring per MECHANICS.md:
 *
 * - Base: **10 points** per cleared gem.
 * - Cascade multiplier: linear by depth, uncapped (`multiplier = cascadeDepth`).
 * - Unique-cell scoring: `gemsClearedThisStep` is the count of **unique**
 *   cleared positions. Overlapping regions (row+column clears, intersecting
 *   matches) never double-count — [roundScore] dedupes via [Set].
 * - `stepScore = uniqueCells * 10 * cascadeDepth`; total = sum over the chain.
 *
 * No special-creation bonus in v1 (MECHANICS.md non-goal).
 */
object Scorer {

    /** Base points per unique cleared gem at depth 1. */
    const val BASE_POINTS_PER_GEM = 10

    /**
     * Points for one cascade round clearing [clearedCells] at [cascadeDepth].
     * Duplicate positions are counted once (unique-cell scoring).
     */
    fun roundScore(clearedCells: Collection<Position>, cascadeDepth: Int): Int {
        require(cascadeDepth >= 1) { "cascadeDepth must be >= 1" }
        return clearedCells.toSet().size * BASE_POINTS_PER_GEM * cascadeDepth
    }

    /** Total score for a full resolution, summing each round's score. */
    fun totalScore(roundScores: Collection<Int>): Int = roundScores.sum()
}