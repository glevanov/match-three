package com.matchthree.game.engine

import com.matchthree.game.model.Board
import com.matchthree.game.model.Gem
import com.matchthree.game.model.GemType
import com.matchthree.game.model.Position
import com.matchthree.game.model.Special
import com.matchthree.game.rng.SeededRandom
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * M4 tests: special-gem birth precedence, all six combos, cascade-swept
 * detonation, Hypercube+Hypercube regeneration, and unique-cell scoring with
 * combos. Pure rule tests call [SpecialRules] directly; integration tests run
 * full [GameEngine.resolveSwap] resolutions.
 */
class SpecialComboTest {

    // --- helpers ------------------------------------------------------------

    private fun engine() = GameEngine(
        rng = SeededRandom(1L),
    )

    private var nextId = 0
    private fun gem(type: GemType, special: Special? = null): Gem = Gem(nextId++, type, special)

    /** A 9x9 board populated only at [placements]; useful for deterministic rules. */
    private fun board9(placements: Map<Position, Gem>): Board {
        val cells = Array(9) { arrayOfNulls<Gem>(9) }
        for ((pos, g) in placements) cells[pos.row][pos.col] = g
        return Board.of(9, 9, cells)
    }

    private fun pos(row: Int, col: Int): Position = Position(row, col)

    // --- birth precedence (pure) --------------------------------------------

    @Test
    fun `5-run births a hypercube`() {
        val board = board9(mapOf(
            pos(0, 0) to gem(GemType.RED), pos(0, 1) to gem(GemType.RED),
            pos(0, 2) to gem(GemType.RED), pos(0, 3) to gem(GemType.RED),
            pos(0, 4) to gem(GemType.RED),
        ))
        val matches = MatchDetector.findMatches(board)
        val birth = SpecialRules.resolveBirths(board, matches).single()
        assertEquals(Special.HYPERCUBE, birth.special)
        assertEquals(pos(0, 0), birth.cell)
    }

    @Test
    fun `t-slash shape births a star`() {
        // L: horizontal run + vertical run sharing the corner (0,0).
        val board = board9(mapOf(
            pos(0, 0) to gem(GemType.RED), pos(0, 1) to gem(GemType.RED),
            pos(0, 2) to gem(GemType.RED),
            pos(1, 0) to gem(GemType.RED), pos(2, 0) to gem(GemType.RED),
        ))
        val matches = MatchDetector.findMatches(board)
        val birth = SpecialRules.resolveBirths(board, matches).single()
        assertEquals(Special.STAR, birth.special)
        assertEquals(pos(0, 0), birth.cell)
    }

    @Test
    fun `4-run births a flame`() {
        val board = board9(mapOf(
            pos(0, 0) to gem(GemType.RED), pos(0, 1) to gem(GemType.RED),
            pos(0, 2) to gem(GemType.RED), pos(0, 3) to gem(GemType.RED),
        ))
        val matches = MatchDetector.findMatches(board)
        val birth = SpecialRules.resolveBirths(board, matches).single()
        assertEquals(Special.FLAME, birth.special)
    }

    @Test
    fun `plain 3-run births nothing`() {
        val board = board9(mapOf(
            pos(0, 0) to gem(GemType.RED), pos(0, 1) to gem(GemType.RED),
            pos(0, 2) to gem(GemType.RED),
        ))
        val matches = MatchDetector.findMatches(board)
        assertTrue(SpecialRules.resolveBirths(board, matches).isEmpty())
    }

    @Test
    fun `precedence five beats t-slash when they intersect`() {
        // Vertical run of 3 crosses the horizontal run of 5 at (0,0).
        val board = board9(mapOf(
            pos(0, 0) to gem(GemType.RED), pos(0, 1) to gem(GemType.RED),
            pos(0, 2) to gem(GemType.RED), pos(0, 3) to gem(GemType.RED),
            pos(0, 4) to gem(GemType.RED),
            pos(1, 0) to gem(GemType.RED), pos(2, 0) to gem(GemType.RED),
        ))
        val matches = MatchDetector.findMatches(board)
        assertEquals(Special.HYPERCUBE, SpecialRules.resolveBirths(board, matches).single().special)
    }

    @Test
    fun `precedence t-slash beats 4-run when they intersect`() {
        val board = board9(mapOf(
            pos(0, 0) to gem(GemType.RED), pos(0, 1) to gem(GemType.RED),
            pos(0, 2) to gem(GemType.RED), pos(0, 3) to gem(GemType.RED),
            pos(1, 0) to gem(GemType.RED), pos(2, 0) to gem(GemType.RED),
        ))
        val matches = MatchDetector.findMatches(board)
        assertEquals(Special.STAR, SpecialRules.resolveBirths(board, matches).single().special)
    }

    // --- per-shape-group births (pure) ---------------------------------------

    @Test
    fun `two non-overlapping 4-runs birth two flames`() {
        val board = board9(mapOf(
            pos(0, 0) to gem(GemType.RED), pos(0, 1) to gem(GemType.RED),
            pos(0, 2) to gem(GemType.RED), pos(0, 3) to gem(GemType.RED),
            pos(4, 0) to gem(GemType.BLUE), pos(4, 1) to gem(GemType.BLUE),
            pos(4, 2) to gem(GemType.BLUE), pos(4, 3) to gem(GemType.BLUE),
        ))
        val births = SpecialRules.resolveBirths(board, MatchDetector.findMatches(board))
        assertEquals(2, births.size)
        assertEquals(listOf(Special.FLAME, Special.FLAME), births.map { it.special })
        assertEquals(listOf(pos(0, 0), pos(4, 0)), births.map { it.cell })
    }

    @Test
    fun `a 4-run and a separate 5-run birth a flame and a hypercube`() {
        val board = board9(mapOf(
            pos(0, 0) to gem(GemType.RED), pos(0, 1) to gem(GemType.RED),
            pos(0, 2) to gem(GemType.RED), pos(0, 3) to gem(GemType.RED),
            pos(4, 0) to gem(GemType.BLUE), pos(4, 1) to gem(GemType.BLUE),
            pos(4, 2) to gem(GemType.BLUE), pos(4, 3) to gem(GemType.BLUE),
            pos(4, 4) to gem(GemType.BLUE),
        ))
        val births = SpecialRules.resolveBirths(board, MatchDetector.findMatches(board))
        assertEquals(2, births.size)
        assertEquals(Special.FLAME, births[0].special)
        assertEquals(pos(0, 0), births[0].cell)
        assertEquals(Special.HYPERCUBE, births[1].special)
        assertEquals(pos(4, 0), births[1].cell)
    }

    @Test
    fun `a t-slash group and a separate 4-run birth one star and one flame`() {
        val board = board9(mapOf(
            pos(0, 0) to gem(GemType.RED), pos(0, 1) to gem(GemType.RED),
            pos(0, 2) to gem(GemType.RED),
            pos(1, 0) to gem(GemType.RED), pos(2, 0) to gem(GemType.RED),
            pos(6, 0) to gem(GemType.BLUE), pos(6, 1) to gem(GemType.BLUE),
            pos(6, 2) to gem(GemType.BLUE), pos(6, 3) to gem(GemType.BLUE),
        ))
        val births = SpecialRules.resolveBirths(board, MatchDetector.findMatches(board))
        // The T/L is ONE group of two intersecting runs: it must not double-birth.
        assertEquals(2, births.size)
        assertEquals(Special.STAR, births[0].special)
        assertEquals(pos(0, 0), births[0].cell)
        assertEquals(Special.FLAME, births[1].special)
        assertEquals(pos(6, 0), births[1].cell)
    }

    @Test
    fun `hypercube is colorless and never part of a run`() {
        // If the hypercube counted as RED, R-R-H-R would be a 3-run.
        val board = board9(mapOf(
            pos(0, 0) to gem(GemType.RED), pos(0, 1) to gem(GemType.RED),
            pos(0, 2) to gem(GemType.RED, Special.HYPERCUBE),
            pos(0, 3) to gem(GemType.RED),
        ))
        assertEquals(0, MatchDetector.findMatches(board).size)
    }

    // --- combo affected cells (pure) ----------------------------------------

    @Test
    fun `flame plus flame clears a 5x5 area`() {
        val board = board9(mapOf(pos(4, 4) to gem(GemType.RED), pos(4, 5) to gem(GemType.RED)))
        val cells = SpecialRules.comboAffectedCells(board, pos(4, 4), pos(4, 5), Special.FLAME, Special.FLAME)
        assertEquals(25, cells.size)
        assertTrue(cells.containsAll(listOf(pos(4, 4), pos(4, 5))))
        // Edges of the 5x5 centered at (4,4).
        assertTrue(cells.contains(pos(2, 2)))
        assertTrue(cells.contains(pos(6, 6)))
    }

    @Test
    fun `flame plus star clears a thick cross`() {
        val board = board9(mapOf(pos(4, 4) to gem(GemType.RED), pos(4, 5) to gem(GemType.RED)))
        val cells = SpecialRules.comboAffectedCells(board, pos(4, 4), pos(4, 5), Special.FLAME, Special.STAR)
        // Rows 3..5 across + cols 3..5 down = 27 + 27 - 9 overlap = 45 unique cells.
        assertEquals(45, cells.size)
        assertTrue(cells.containsAll(listOf(pos(4, 4), pos(4, 5))))
        assertTrue(cells.contains(pos(3, 0)))
        assertTrue(cells.contains(pos(8, 4)))
    }

    @Test
    fun `star plus star clears both rows and columns deduped`() {
        val board = board9(mapOf(pos(4, 4) to gem(GemType.RED), pos(4, 6) to gem(GemType.RED)))
        val cells = SpecialRules.comboAffectedCells(board, pos(4, 4), pos(4, 6), Special.STAR, Special.STAR)
        // Row 4 (9) + col 4 (9) + col 6 (9), minus the two cells already in row 4.
        assertEquals(25, cells.size)
        assertTrue(cells.containsAll(listOf(pos(4, 4), pos(4, 6))))
    }

    @Test
    fun `flame plus hypercube powers every gem of the partner color`() {
        val board = board9(mapOf(
            pos(4, 4) to gem(GemType.BLUE),            // hypercube's cell (colorless)
            pos(4, 5) to gem(GemType.RED, Special.FLAME), // the partner flame
            pos(0, 0) to gem(GemType.RED),
            pos(8, 8) to gem(GemType.RED),
        ))
        val cells = SpecialRules.comboAffectedCells(
            board, pos(4, 4), pos(4, 5), Special.HYPERCUBE, Special.FLAME,
        )
        // Hypercube cell is inside the flame's 3x3, which covers (4,4);
        // + 3x3 around the flame (9), (0,0) (4), (8,8) (4) = 17 unique cells.
        assertEquals(17, cells.size)
        assertTrue(cells.containsAll(listOf(pos(4, 4), pos(4, 5), pos(0, 0), pos(8, 8))))
    }

    @Test
    fun `star plus hypercube clears rows and columns of every partner-color gem`() {
        val board = board9(mapOf(
            pos(4, 4) to gem(GemType.BLUE),
            pos(4, 5) to gem(GemType.RED, Special.STAR),
            pos(0, 0) to gem(GemType.RED),
            pos(8, 8) to gem(GemType.RED),
        ))
        val cells = SpecialRules.comboAffectedCells(
            board, pos(4, 4), pos(4, 5), Special.HYPERCUBE, Special.STAR,
        )
        // Rows {0,4,8} + cols {0,5,8} = 54 - 9 intersections.
        assertEquals(45, cells.size)
        assertTrue(cells.containsAll(listOf(pos(4, 4), pos(4, 5), pos(0, 0), pos(8, 8))))
    }

    @Test
    fun `hypercube plus hypercube clears the entire board`() {
        val board = board9(mapOf(
            pos(4, 4) to gem(GemType.BLUE, Special.HYPERCUBE),
            pos(4, 5) to gem(GemType.BLUE, Special.HYPERCUBE),
        ))
        val cells = SpecialRules.comboAffectedCells(
            board, pos(4, 4), pos(4, 5), Special.HYPERCUBE, Special.HYPERCUBE,
        )
        assertEquals(81, cells.size)
    }

    @Test
    fun `combo cells are always unique even when regions overlap`() {
        val board = board9(mapOf(
            pos(4, 4) to gem(GemType.RED),
            pos(4, 6) to gem(GemType.RED),
            pos(4, 8) to gem(GemType.RED),
        ))
        val cells = SpecialRules.comboAffectedCells(board, pos(4, 4), pos(4, 6), Special.STAR, Special.STAR)
        // Set semantics: never double-count a cell in the overlapping row.
        assertEquals(cells.size, cells.toSet().size)
        assertEquals(25, Scorer.roundScore(cells, 1) / Scorer.BASE_POINTS_PER_GEM)
    }

    // --- integration: cascade-swept detonation ------------------------------

    @Test
    fun `flame swept into a cascade match detonates and blasts the area`() {
        val engine = engine()
        // Swap (4,5)Y <-> (5,5)R creates R-R(R)+FLAME-R on row 4; the RED flame
        // at (4,4) is now inside the matched run and must detonate (3x3).
        val board = board9(mapOf(
            pos(4, 3) to gem(GemType.RED),
            pos(4, 4) to gem(GemType.RED, Special.FLAME),
            pos(4, 5) to gem(GemType.YELLOW),
            pos(5, 5) to gem(GemType.RED),
        ))
        val resolution = engine.resolveSwap(board, pos(4, 5), pos(5, 5))
        val steps = requireNotNull(resolution).steps
        val firstDestroy = steps.filterIsInstance<Step.Destroy>().first()

        // Match (3 cells) + flame blast (3x3 = 9 cells, overlapping the match).
        assertEquals(9, firstDestroy.positions.size)
        assertTrue(firstDestroy.positions.contains(pos(3, 4)))   // blast cell outside the run
        assertTrue(firstDestroy.positions.contains(pos(5, 4)))
    }

    // --- integration: swaps of specials -------------------------------------

    @Test
    fun `swapping two flames fires the flame-flame combo`() {
        val engine = engine()
        val board = board9(mapOf(
            pos(4, 4) to gem(GemType.RED, Special.FLAME),
            pos(4, 5) to gem(GemType.RED, Special.FLAME),
        ))
        val resolution = engine.resolveSwap(board, pos(4, 4), pos(4, 5))
        val combo = requireNotNull(resolution).steps.filterIsInstance<Step.ComboActivate>().first()
        assertEquals(Special.FLAME, combo.specialA)
        assertEquals(Special.FLAME, combo.specialB)
        assertEquals(25, combo.affectedCells.size)

        val firstDestroy = resolution.steps.filterIsInstance<Step.Destroy>().first()
        assertEquals(combo.affectedCells, firstDestroy.positions)
    }

    @Test
    fun `swapping a flame and a star fires the thick-cross combo`() {
        val engine = engine()
        val board = board9(mapOf(
            pos(4, 4) to gem(GemType.RED, Special.FLAME),
            pos(4, 5) to gem(GemType.RED, Special.STAR),
        ))
        val resolution = engine.resolveSwap(board, pos(4, 4), pos(4, 5))
        val combo = requireNotNull(resolution).steps.filterIsInstance<Step.ComboActivate>().first()
        assertEquals(setOf(Special.FLAME, Special.STAR), setOf(combo.specialA, combo.specialB))
        assertEquals(45, combo.affectedCells.size)

        val firstDestroy = resolution.steps.filterIsInstance<Step.Destroy>().first()
        assertEquals(combo.affectedCells, firstDestroy.positions)
    }

    @Test
    fun `swapping two stars fires the dual row-column combo`() {
        val engine = engine()
        val board = board9(mapOf(
            pos(4, 4) to gem(GemType.RED, Special.STAR),
            pos(4, 5) to gem(GemType.RED, Special.STAR),
        ))
        val resolution = engine.resolveSwap(board, pos(4, 4), pos(4, 5))
        val combo = requireNotNull(resolution).steps.filterIsInstance<Step.ComboActivate>().first()
        assertEquals(Special.STAR, combo.specialA)
        assertEquals(Special.STAR, combo.specialB)
        // Row 4 (9) + col 4 (9) + col 5 (9) minus the two row cells already counted.
        assertEquals(25, combo.affectedCells.size)
    }

    @Test
    fun `swapping a flame and a hypercube fires the all-flames combo`() {
        val engine = engine()
        val board = board9(mapOf(
            pos(4, 4) to gem(GemType.RED, Special.FLAME),
            pos(4, 5) to gem(GemType.BLUE, Special.HYPERCUBE),
            pos(0, 0) to gem(GemType.RED),
            pos(8, 8) to gem(GemType.RED),
        ))
        val resolution = engine.resolveSwap(board, pos(4, 4), pos(4, 5))
        val combo = requireNotNull(resolution).steps.filterIsInstance<Step.ComboActivate>().first()
        assertTrue(setOf(Special.FLAME, Special.HYPERCUBE) ==
            setOf(combo.specialA ?: error("null"), combo.specialB ?: error("null")))
        // Hypercube cell is already inside the flame's 3x3 blast; 3x3 blasts of
        // the RED flame, (0,0), and (8,8) total 9 + 4 + 4 = 17 unique cells.
        assertEquals(17, combo.affectedCells.size)
    }

    @Test
    fun `swapping a hypercube with a normal gem clears all gems of its color`() {
        val engine = engine()
        val board = board9(mapOf(
            pos(4, 4) to gem(GemType.BLUE, Special.HYPERCUBE),
            pos(4, 5) to gem(GemType.RED),
            pos(0, 0) to gem(GemType.RED),
        ))
        val resolution = engine.resolveSwap(board, pos(4, 4), pos(4, 5))
        val steps = requireNotNull(resolution).steps
        val combo = steps.filterIsInstance<Step.ComboActivate>().first()
        assertEquals(Special.HYPERCUBE, combo.specialA)
        assertNull(combo.specialB)
        assertEquals(setOf(pos(4, 4), pos(4, 5), pos(0, 0)), combo.affectedCells)
    }

    @Test
    fun `hypercube plus hypercube regenerates the board`() {
        val engine = engine()
        val board = board9(mapOf(
            pos(4, 4) to gem(GemType.BLUE, Special.HYPERCUBE),
            pos(4, 5) to gem(GemType.BLUE, Special.HYPERCUBE),
        ))
        val resolution = engine.resolveSwap(board, pos(4, 4), pos(4, 5))
        val steps = requireNotNull(resolution).steps
        val combo = steps.filterIsInstance<Step.ComboActivate>().first()
        assertEquals(Special.HYPERCUBE, combo.specialA)
        assertEquals(Special.HYPERCUBE, combo.specialB)
        assertEquals(81, combo.affectedCells.size)

        // Full-board clear: the destroy covers everything and no refill spawns;
        // the resolution ends directly on a regenerated, invariant-clean board.
        assertTrue(steps.filterIsInstance<Step.Destroy>().last().positions.size == 81)
        assertEquals(0, steps.filterIsInstance<Step.Spawn>().size)

        val settled = steps.last() as Step.Settled
        val fresh = settled.board
        assertEquals(81, fresh.positions().count { fresh.gemAt(it) != null })
        assertTrue(fresh.positions().all { fresh.gemAt(it)?.special == null })
        assertEquals(0, MatchDetector.findMatches(fresh).size)
        assertTrue(LegalMoveDetector.hasLegalMove(fresh))
    }

    @Test
    fun `one swap creating two separate 4-runs births two flames`() {
        val engine = engine()
        // Swap (4,5)G <-> (5,5)R: RED lands at (4,5) completing the RED 4-run on
        // row 4 (cols 3-6); GREEN lands at (5,5) completing the GREEN 4-run on
        // col 5 (rows 5-8). The runs share no cells: two shapes, two births.
        val board = board9(mapOf(
            pos(4, 3) to gem(GemType.RED),
            pos(4, 4) to gem(GemType.RED),
            pos(4, 6) to gem(GemType.RED),
            pos(4, 5) to gem(GemType.GREEN),
            pos(5, 5) to gem(GemType.RED),
            pos(6, 5) to gem(GemType.GREEN),
            pos(7, 5) to gem(GemType.GREEN),
            pos(8, 5) to gem(GemType.GREEN),
        ))
        // Note: the GREEN cells (6,5)-(8,5) are a pre-existing 3-run — any
        // straight 4-run completed by one displaced gem contains three
        // contiguous pre-swap cells, so a match-free fixture is impossible here
        // (same situation as the 5-run birth test above). The engine resolves
        // pre-existing matches in round 1 together with the swap-created runs;
        // here they merge into the GREEN 4-run, so round 1 is still exactly the
        // two intended shapes.
        val steps = requireNotNull(engine.resolveSwap(board, pos(4, 5), pos(5, 5))).steps

        val births = steps.filterIsInstance<Step.SpecialBirth>()
        assertEquals(2, births.size)
        assertTrue(births.all { it.special == Special.FLAME })
        assertTrue("distinct birth gems", births[0].gemId != births[1].gemId)
        // Birth gems fall with gravity: RED flame to the bottom of col 3, GREEN
        // flame to the bottom of col 5 (all other column gems were cleared).
        assertEquals(listOf(pos(8, 3), pos(8, 5)), births.map { it.position })

        // Round 1 clears exactly the six non-birth matched cells.
        val firstDestroy = steps.filterIsInstance<Step.Destroy>().first()
        assertEquals(
            setOf(pos(4, 4), pos(4, 5), pos(4, 6), pos(6, 5), pos(7, 5), pos(8, 5)),
            firstDestroy.positions,
        )
        val firstScore = steps.filterIsInstance<Step.Score>().first()
        assertEquals(6 * Scorer.BASE_POINTS_PER_GEM, firstScore.delta)
        assertEquals(1, firstScore.cascadeDepth)
    }

    // --- integration: births and scoring ------------------------------------

    @Test
    fun `a swap creating a 5-run transforms a gem into a hypercube`() {
        val engine = engine()
        // Swap (0,0)Y <-> (1,0)X: row 0 becomes a 5-run of X.
        val board = board9(mapOf(
            pos(0, 0) to gem(GemType.YELLOW),
            pos(0, 1) to gem(GemType.RED), pos(0, 2) to gem(GemType.RED),
            pos(0, 3) to gem(GemType.RED), pos(0, 4) to gem(GemType.RED),
            pos(0, 5) to gem(GemType.RED),
            pos(1, 0) to gem(GemType.RED),
        ))
        val resolution = engine.resolveSwap(board, pos(0, 0), pos(1, 0))
        val steps = requireNotNull(resolution).steps
        val birth = steps.filterIsInstance<Step.SpecialBirth>().first()
        assertEquals(Special.HYPERCUBE, birth.special)

        // The born hypercube survives the resolution on the settled board.
        val settled = steps.last() as Step.Settled
        val survivors = settled.board.positions()
            .mapNotNull { settled.board.gemAt(it) }
            .filter { it.special == Special.HYPERCUBE }
        assertTrue(survivors.isNotEmpty())
        assertTrue(survivors.any { it.id == birth.gemId })
    }

    @Test
    fun `a swap creating a t-slash transforms a gem into a star`() {
        val engine = engine()
        // A pre-existing L match (col 0 + row 0) is processed in the first
        // cascade round along with the trivial adjacent swap elsewhere; the
        // perpendicular runs intersect at the corner and birth a Star.
        // (A plus/T cannot be completed by a single swap — same as Bejeweled.)
        val board = board9(mapOf(
            pos(0, 0) to gem(GemType.RED), pos(0, 1) to gem(GemType.RED),
            pos(0, 2) to gem(GemType.RED),
            pos(1, 0) to gem(GemType.RED), pos(2, 0) to gem(GemType.RED),
            pos(8, 0) to gem(GemType.GREEN), pos(8, 1) to gem(GemType.BLUE),
        ))
        // Swapping any two gems is legal here: the L already guarantees matches.
        val resolution = engine.resolveSwap(board, pos(8, 0), pos(8, 1))
        val steps = requireNotNull(resolution).steps
        val birth = steps.filterIsInstance<Step.SpecialBirth>().first()
        assertEquals(Special.STAR, birth.special)

        val settled = steps.last() as Step.Settled
        assertTrue(settled.board.positions().any { settled.board.gemAt(it)?.special == Special.STAR })
    }

    @Test
    fun `combo round scores unique cells at depth one`() {
        val engine = engine()
        val board = board9(mapOf(
            pos(4, 4) to gem(GemType.RED, Special.STAR),
            pos(4, 5) to gem(GemType.RED, Special.STAR),
        ))
        val steps = engine.resolveSwap(board, pos(4, 4), pos(4, 5))!!.steps
        val firstScore = steps.filterIsInstance<Step.Score>().first()
        assertEquals(1, firstScore.cascadeDepth)
        // 25 unique cells * 10 base * depth 1.
        assertEquals(25 * Scorer.BASE_POINTS_PER_GEM, firstScore.delta)
    }

    @Test
    fun `full-board hypercube clear scores 81 unique cells`() {
        val engine = engine()
        val board = board9(mapOf(
            pos(4, 4) to gem(GemType.BLUE, Special.HYPERCUBE),
            pos(4, 5) to gem(GemType.BLUE, Special.HYPERCUBE),
        ))
        val res = engine.resolveSwap(board, pos(4, 4), pos(4, 5))!!
        val comboScore = res.steps.filterIsInstance<Step.Score>().first()
        assertEquals(81 * Scorer.BASE_POINTS_PER_GEM, comboScore.delta)
    }
}