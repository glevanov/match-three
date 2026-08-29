package com.matchthree.game.engine

import com.matchthree.game.Boards
import com.matchthree.game.model.Board
import com.matchthree.game.model.Gem
import com.matchthree.game.model.GemType
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

    @Test
    fun `each cascade round emits a score step with unique-cell delta and depth`() {
        val engine = engine()
        val resolution = engine.resolveSwap(cascadeFixture(), Position(5, 3), Position(4, 3))!!

        val destroys = resolution.steps.filterIsInstance<Step.Destroy>()
        val scores = resolution.steps.filterIsInstance<Step.Score>()
        assertTrue("expected a cascade", destroys.size >= 2)
        assertEquals(destroys.size, scores.size)

        scores.forEachIndexed { index, score ->
            val destroyed = destroys[index].positions
            assertEquals(
                "round ${index + 1}: delta = uniqueCells * 10 * depth",
                destroyed.size * Scorer.BASE_POINTS_PER_GEM * (index + 1),
                score.delta,
            )
            assertEquals(index + 1, score.cascadeDepth)
        }

        assertEquals(scores.sumOf { it.delta }, Scorer.totalScore(scores.map { it.delta }))
    }

    @Test
    fun `score steps alternate with destroy steps in cascade order`() {
        val engine = engine()
        val resolution = engine.resolveSwap(cascadeFixture(), Position(5, 3), Position(4, 3))!!

        val paired = resolution.steps.filter { it is Step.Destroy || it is Step.Score }
        assertTrue(paired.size >= 4)
        paired.forEachIndexed { index, step ->
            // Destroy and Score strictly alternate, Destroy first, per cascade round.
            val expected = if (index % 2 == 0) "Destroy" else "Score"
            assertEquals(expected, step::class.simpleName)
        }
    }

    @Test
    fun `reshuffle preserves the gem multiset and restores legal moves`() {
        val engine = engine()
        // A 3x6 checker pattern: no pre-existing matches, and the balanced
        // 6-color multiset makes a match-free legal rearrangement easy to hit.
        val board = Boards.fromRows(
            "RYRYRY",
            "GBGBGB",
            "YOYOYP",
        )

        val reshuffled = engine.reshuffle(board)
        assertTrue("reshuffle should fix a small board", reshuffled != null)

        val originalIds = board.positions().mapNotNull { board.gemAt(it)?.id }.sorted()
        val shuffledIds = reshuffled!!.positions().mapNotNull { reshuffled.gemAt(it)?.id }.sorted()
        assertEquals("same gem multiset (ids preserved)", originalIds, shuffledIds)

        assertEquals(0, MatchDetector.findMatches(reshuffled).size)
        assertTrue(LegalMoveDetector.hasLegalMove(reshuffled))
    }

    @Test
    fun `reshuffle returns null when the board cannot be fixed`() {
        val engine = engine()
        // A 2x2 board of a single type: every shuffle has matches from the start
        // and no 3-run can ever be formed, so no legal move can appear.
        val dead = Board.create(2, 2) { pos -> Gem(pos.row * 2 + pos.col, GemType.fromIndex(0)) }
        assertNull(engine.reshuffle(dead))
    }
}