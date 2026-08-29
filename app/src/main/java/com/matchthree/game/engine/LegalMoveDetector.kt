package com.matchthree.game.engine

import com.matchthree.game.model.Board
import com.matchthree.game.model.Gem
import com.matchthree.game.model.Position

/**
 * Counts swaps that would create a match. A swap is legal iff it is orthogonal
 * adjacency between two present gems and the swapped board contains a match.
 */
object LegalMoveDetector {

    fun legalMoveCount(board: Board): Int {
        var count = 0
        for (row in 0 until board.height) {
            for (col in 0 until board.width) {
                val a = Position(row, col)
                if (board.gemAt(a) == null) continue
                // Test each orthogonal neighbor once (right and down).
                val neighbors = listOf(
                    Position(row, col + 1),
                    Position(row + 1, col),
                )
                for (b in neighbors) {
                    if (board.isInside(b) && board.gemAt(b) != null) {
                        val swapped = board.withSwapped(a, b)
                        val matchLegal = MatchDetector.findMatches(swapped).isNotEmpty()
                        val specialLegal = SpecialRules.swapContactLegal(swapped.gemAt(a), swapped.gemAt(b))
                        if (matchLegal || specialLegal) count++
                    }
                }
            }
        }
        return count
    }

    fun hasLegalMove(board: Board): Boolean = legalMoveCount(board) > 0
}