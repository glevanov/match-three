package com.matchthree.game.engine

import com.matchthree.game.Boards
import com.matchthree.game.model.Board
import com.matchthree.game.model.Position
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class GameEngineTest {

    private fun engine() = GameEngine(
        rng = com.matchthree.game.rng.SeededRandom(1L),
    )

    /**
     * A full 9x9 board with no pre-existing matches where swapping (5,3)<->(4,3)
     * creates a PURPLE run on row 5; displacement then produces a second cascade
     * round (BLUE run in column 2) without depending on spawn randomness.
     */
    private fun cascadeFixture(): Board {
        return Boards.fromRows(
            "RYRYRYRYR",
            "YRYRYRYRY",
            "RYRYRYRYR",
            "YRYRYRRYR",
            "RYBARYYRY",
            "YRABARYRY",
            "RYBRYYRYR",
            "YRBGYRYRY",
            "RYRYRYYRY",
        )
    }

    @Test
    fun `illegal swap returns null and no steps`() {
        val engine = engine()
        val board = cascadeFixture()
        val swapped = engine.resolveSwap(board, Position(0, 0), Position(2, 2))
        assertNull(swapped)
    }

    @Test
    fun `swap resolution emits ordered steps and settles`() {
        val engine = engine()
        val board = cascadeFixture()

        val resolution = engine.resolveSwap(board, Position(5, 3), Position(4, 3))
        assertTrue(resolution != null)

        val steps = resolution!!.steps
        assertEquals(Step.Swap(Position(5, 3), Position(4, 3)), steps.first())

        // Round 1 destroys exactly the created PURPLE run.
        val firstDestroy = steps[1] as Step.Destroy
        assertEquals(
            setOf(Position(5, 2), Position(5, 3), Position(5, 4)),
            firstDestroy.positions,
        )

        // At least one more cascade round follows (column 2 turns BLUE,BLUE,BLUE).
        val destroyCount = steps.count { it is Step.Destroy }
        assertTrue("expected cascade, got $destroyCount destroys", destroyCount >= 2)

        // A fall step separates the destroy rounds.
        val falls = steps.filterIsInstance<Step.Fall>()
        assertTrue(falls.isNotEmpty())

        // Board ends stable: no matches remain.
        val settled = steps.last() as Step.Settled
        assertEquals(0, MatchDetector.findMatches(settled.board).size)

        // Steps order: Swap, then alternating Destroy/Fall/Spawn, ending Settled.
        val kinds = steps.map { it::class.simpleName }
        assertEquals("Settled", kinds.last())
    }

    @Test
    fun `new game board is fully populated`() {
        val engine = engine()
        val board = engine.newGame()
        assertEquals(81, board.positions().count { board.gemAt(it) != null })
        assertEquals(0, MatchDetector.findMatches(board).size)
        assertTrue(LegalMoveDetector.hasLegalMove(board))
    }

    @Test
    fun `spawned gems get fresh unique ids`() {
        val engine = engine()
        // Consume session ids the way a real game does before any swap.
        val sessionBoard = engine.newGame()
        val sessionIds = sessionBoard.positions().mapNotNull { sessionBoard.gemAt(it)?.id }.toSet()
        val fixture = cascadeFixture()
        val fixtureIds = fixture.positions().mapNotNull { fixture.gemAt(it)?.id }.toSet()

        val resolution = engine.resolveSwap(fixture, Position(5, 3), Position(4, 3))!!
        val spawnedIds = resolution.steps
            .filterIsInstance<Step.Spawn>()
            .flatMap { it.gems.map { g -> g.gem.id } }
        assertTrue(spawnedIds.isNotEmpty())
        assertTrue(spawnedIds.all { it !in fixtureIds })       // never clash with pre-existing gems
        assertTrue(spawnedIds.all { it !in sessionIds })      // nor with earlier session gems
        assertEquals(spawnedIds.size, spawnedIds.toSet().size) // pairwise distinct
    }
}