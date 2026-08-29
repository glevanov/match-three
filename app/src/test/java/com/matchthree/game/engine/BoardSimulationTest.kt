package com.matchthree.game.engine

import com.matchthree.game.model.BoardConfig
import com.matchthree.game.model.Position
import com.matchthree.game.rng.SeededRandom
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Simulates many generated 9x9/6 boards and reports the two tuning metrics named
 * in DESIGN.md: average legal-move count, and the probability that a random
 * adjacent swap is legal. Values print to the test stdout (both via the Fail
 * summary and standard stream) so they can be read from `./gradlew test`.
 */
class BoardSimulationTest {

    private val config = BoardConfig() // 9x9, 6 gem types

    @Test
    fun reportSimulationMetrics() {
        val boardCount = 150
        var totalLegalMoves = 0L
        var totalAdjacentPairs = 0L
        var totalLegalSwaps = 0L

        repeat(boardCount) { i ->
            val engine = GameEngine(config, SeededRandom(1000L + i))
            val board = engine.newGame()
            val legalCount = LegalMoveDetector.legalMoveCount(board)
            totalLegalMoves += legalCount

            for (row in 0 until config.height) {
                for (col in 0 until config.width) {
                    val a = Position(row, col)
                    if (board.gemAt(a) == null) continue
                    for (b in neighbours(row, col)) {
                        if (board.isInside(b) && board.gemAt(b) != null) {
                            totalAdjacentPairs++
                            if (engine.isLegalSwap(board, a, b)) totalLegalSwaps++
                        }
                    }
                }
            }
        }

        val avgLegalMoves = totalLegalMoves.toDouble() / boardCount
        val swapMatchProbability = totalLegalSwaps.toDouble() / totalAdjacentPairs

        println(
            "SIMULATION 9x9/6 over $boardCount boards -> " +
                "avg legal moves per board: $avgLegalMoves, " +
                "random-swap match probability: $swapMatchProbability",
        )

        // Loose sanity bounds so the test is a real gate but not flaky.
        assertTrue(avgLegalMoves > 1.0)
        assertTrue(avgLegalMoves < 80.0)
        assertTrue(swapMatchProbability > 0.0)
        assertTrue(swapMatchProbability < 0.5)
    }

    private fun neighbours(row: Int, col: Int): List<Position> = listOf(
        Position(row, col + 1),
        Position(row + 1, col),
    )
}