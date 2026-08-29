package com.matchthree.game.engine

import com.matchthree.game.Boards
import com.matchthree.game.model.Position
import com.matchthree.game.rng.SeededRandom
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class SwapValidationTest {

    private val engine by lazy { GameEngine(rng = SeededRandom(1L)) }

    @Test
    fun `swap completing a horizontal run is legal`() {
        // Swap (0,1) G with (1,1) R -> row 0 turns into R,R,R.
        val board = Boards.fromRows(
            "RGR",
            "BRB",
            "RYR",
        )
        assertTrue(engine.isLegalSwap(board, Position(0, 1), Position(1, 1)))
    }

    @Test
    fun `swap that creates nothing is illegal`() {
        val board = Boards.fromRows(
            "RGR",
            "BPP",
            "RYR",
        )
        assertFalse(engine.isLegalSwap(board, Position(0, 1), Position(1, 1)))
    }

    @Test
    fun `non adjacent swap is illegal`() {
        val board = Boards.fromRows("RYR", "BGP", "RYR")
        assertFalse(engine.isLegalSwap(board, Position(0, 0), Position(2, 2)))
    }

    @Test
    fun `position outside board is illegal`() {
        val board = Boards.fromRows("RYR", "BGP", "RYR")
        assertFalse(engine.isLegalSwap(board, Position(3, 0), Position(2, 0)))
        assertFalse(engine.isLegalSwap(board, Position(-1, 0), Position(0, 0)))
    }

    @Test
    fun `swapping an empty cell is illegal`() {
        val board = Boards.fromRows(
            ".YR",
            "BGP",
            "RYR",
        )
        assertFalse(engine.isLegalSwap(board, Position(0, 0), Position(1, 0)))
    }

    @Test
    fun `swap completing a vertical run is legal`() {
        // Swap (0,1) P with (0,2) G -> column 2 turns into P,P,P.
        val board = Boards.fromRows(
            "RPG",
            "BGP",
            "RGP",
        )
        assertTrue(engine.isLegalSwap(board, Position(0, 1), Position(0, 2)))
    }

    @Test
    fun `fixture board legal move count matches brute-force scan`() {
        // Verified by exhaustive scan: exactly four out of twelve adjacent pairs
        // create a match (row0 RRR, col0 RRR, col2 RRR, row2 RRR).
        val board = Boards.fromRows(
            "RGR",
            "BRB",
            "RYR",
        )
        assertEquals(4, LegalMoveDetector.legalMoveCount(board))
        val bruteForce = countLegalSwapsByBruteForce(board)
        assertEquals(bruteForce, LegalMoveDetector.legalMoveCount(board))
    }

    private fun countLegalSwapsByBruteForce(board: com.matchthree.game.model.Board): Int {
        var count = 0
        for (row in 0 until board.height) {
            for (col in 0 until board.width) {
                val a = Position(row, col)
                if (board.gemAt(a) == null) continue
                for (b in listOf(Position(row, col + 1), Position(row + 1, col))) {
                    if (board.isInside(b) && board.gemAt(b) != null) {
                        if (MatchDetector.findMatches(board.withSwapped(a, b)).isNotEmpty()) count++
                    }
                }
            }
        }
        return count
    }
}