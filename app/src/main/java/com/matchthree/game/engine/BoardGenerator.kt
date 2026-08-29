package com.matchthree.game.engine

import com.matchthree.game.model.Board
import com.matchthree.game.model.Gem
import com.matchthree.game.model.GemType
import com.matchthree.game.model.Position
import com.matchthree.game.rng.SeededRandom

/**
 * Generates fresh boards that satisfy MECHANICS.md invariants:
 * - no pre-existing match (guaranteed by placement rule), and
 * - at least one legal move (validated; regenerate while none exists).
 *
 * Placement: left-to-right, top-to-bottom, excluding any color that would extend
 * a run of 2 with the already-placed neighbors above/left.
 */
class BoardGenerator(
    private val width: Int,
    private val height: Int,
    private val gemTypeCount: Int,
    private val rng: SeededRandom,
    private val idSource: IdSource,
) {
    fun newBoard(): Board {
        while (true) {
            val board = generateOnce()
            if (LegalMoveDetector.hasLegalMove(board)) return board
        }
    }

    private fun generateOnce(): Board {
        val cells = Array(height) { arrayOfNulls<Gem>(width) }
        for (row in 0 until height) {
            for (col in 0 until width) {
                val type = pickTypeAvoidingRun(cells, row, col)
                cells[row][col] = Gem(idSource.next(), type)
            }
        }
        return Board.of(width, height, cells)
    }

    private fun pickTypeAvoidingRun(
        cells: Array<Array<Gem?>>,
        row: Int,
        col: Int,
    ): GemType {
        val forbidden = mutableSetOf<GemType>()
        // Left: cells at (row, col-2) and (row, col-1) both present and equal?
        if (col >= 2) {
            val left1 = cells[row][col - 1]?.type
            if (left1 != null && left1 == cells[row][col - 2]?.type) forbidden += left1
        }
        // Above: cells at (row-2, col) and (row-1, col) both present and equal?
        if (row >= 2) {
            val up1 = cells[row - 1][col]?.type
            if (up1 != null && up1 == cells[row - 2][col]?.type) forbidden += up1
        }
        val allowed = GemType.entries.filter { it !in forbidden }
        return allowed[rng.nextInt(allowed.size)]
    }
}