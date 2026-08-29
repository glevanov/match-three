package com.matchthree.game.engine

import com.matchthree.game.model.BoardConfig
import com.matchthree.game.rng.SeededRandom
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class BoardGeneratorTest {

    @Test
    fun `generated boards have no pre-existing matches and at least one legal move`() {
        val config = BoardConfig()
        repeat(50) { i ->
            val rng = SeededRandom(10_000L + i)
            val board = GameEngine(config, rng).newGame()
            val matches = MatchDetector.findMatches(board)
            assertTrue("board $i has unexpected matches: $matches", matches.isEmpty())
            assertTrue("board $i has no legal move", LegalMoveDetector.hasLegalMove(board))
        }
    }

    @Test
    fun `generated boards use unique gem ids`() {
        val config = BoardConfig()
        val engine = GameEngine(config, SeededRandom(77L))
        repeat(10) {
            val board = engine.newGame()
            val ids = board.positions().mapNotNull { board.gemAt(it)?.id }
            assertEquals(config.width * config.height, ids.size)
            assertEquals(ids.size, ids.toSet().size)
        }
    }

    @Test
    fun `overall board size respects config`() {
        val engine = GameEngine(BoardConfig(), SeededRandom(5L))
        val board = engine.newGame()
        assertEquals(9, board.width)
        assertEquals(9, board.height)
    }
}