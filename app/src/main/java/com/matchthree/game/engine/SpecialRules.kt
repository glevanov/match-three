package com.matchthree.game.engine

import com.matchthree.game.model.Board
import com.matchthree.game.model.Gem
import com.matchthree.game.model.Position
import com.matchthree.game.model.Special

/**
 * Pure M4 special-gem rules (MECHANICS.md). All functions are deterministic and
 * JVM-testable — the engine calls these, the tests call these directly.
 *
 * Birth is per shape (MECHANICS.md): runs sharing cells form one shape, and
 * each shape births exactly one special by precedence **5-in-row > T/L >
 * 4-in-row > plain 3** — one matched gem transforms, the rest clear normally.
 * Non-overlapping shapes resolve independently, so one cascade round can birth
 * several specials.
 */
object SpecialRules {

    /** A gem chosen to transform into a special during a cascade round. */
    data class Birth(val special: Special, val gemId: Int, val cell: Position)

    /** True when the swap of [gemA] and [gemB] is legal even without a match. */
    fun swapContactLegal(gemA: Gem?, gemB: Gem?): Boolean {
        val specA = gemA?.special
        val specB = gemB?.special
        return (specA != null && specB != null) || specA == Special.HYPERCUBE || specB == Special.HYPERCUBE
    }

    /**
     * Decides the specials born from one cascade round: runs are clustered into
     * shapes by shared cells, and each shape births at most one special by
     * precedence (5-run > T/L > 4-run; plain 3-runs birth nothing). Groups are
     * ordered by their first run's index in [matches] — deterministic.
     */
    fun resolveBirths(board: Board, matches: List<Match>): List<Birth> =
        shapeGroups(matches).mapNotNull { birthForShape(board, it) }

    /**
     * Clusters runs into shapes: runs sharing any cell belong to the same shape
     * (a T/L is one shape of two intersecting runs; a shared cell implies the
     * same gem and color, since a cell holds one gem).
     */
    private fun shapeGroups(matches: List<Match>): List<List<Match>> {
        val parent = IntArray(matches.size) { it }
        fun find(i: Int): Int {
            var root = i
            while (parent[root] != root) root = parent[root]
            var cur = i
            while (parent[cur] != root) {
                val next = parent[cur]
                parent[cur] = root
                cur = next
            }
            return root
        }

        val cellOwner = HashMap<Position, Int>()
        for ((index, match) in matches.withIndex()) {
            for (pos in match.positions) {
                val owner = cellOwner.put(pos, index)
                if (owner != null) {
                    val a = find(owner)
                    val b = find(index)
                    if (a != b) parent[maxOf(a, b)] = minOf(a, b)
                }
            }
        }

        val groups = LinkedHashMap<Int, MutableList<Match>>()
        for ((index, match) in matches.withIndex()) {
            groups.getOrPut(find(index)) { mutableListOf() }.add(match)
        }
        return groups.values.toList()
    }

    /** The one special born from a single shape (or null for plain 3-runs). */
    private fun birthForShape(board: Board, runs: List<Match>): Birth? {
        // 1. 5-in-row wins over everything (including a crossing T/L).
        val five = runs.firstOrNull { it.positions.size >= 5 }
        if (five != null) {
            val cell = five.positions.firstOrNull { board.gemAt(it)?.special == null }
                ?: five.positions[2]
            val gem = checkNotNull(board.gemAt(cell)) { "matched run cell $cell is empty" }
            return Birth(Special.HYPERCUBE, gem.id, cell)
        }

        // 2. T/L: a position shared by a horizontal and a vertical run.
        val cross = intersectionCell(board, runs)
        if (cross != null) {
            val gem = checkNotNull(board.gemAt(cross)) { "intersection cell $cross is empty" }
            return Birth(Special.STAR, gem.id, cross)
        }

        // 3. 4-in-row becomes a Flame.
        val four = runs.firstOrNull { it.positions.size == 4 }
        if (four != null) {
            val cell = four.positions.firstOrNull { board.gemAt(it)?.special == null } ?: four.positions[1]
            val gem = checkNotNull(board.gemAt(cell)) { "matched run cell $cell is empty" }
            return Birth(Special.FLAME, gem.id, cell)
        }

        // 4. Plain 3-runs birth nothing.
        return null
    }

    /**
     * Cells cleared when [special] detonates around [center]: Flame = 3x3,
     * Star = its row + column. Hypercubes never reach this path (colorless:
     * they cannot be part of a match), so they add nothing here.
     */
    fun detonationCells(board: Board, center: Position, special: Special): Set<Position> =
        when (special) {
            Special.FLAME -> blast(center, radius = 1, board)
            Special.STAR -> starLines(center, board)
            Special.HYPERCUBE -> emptySet()
        }

    /**
     * Extra detonations from specials swept into a cascade round's match cells.
     * The matched cells themselves are cleared anyway; this adds their blasts.
     */
    fun sweptBlastCells(board: Board, matched: Set<Position>): Set<Position> {
        val extra = mutableSetOf<Position>()
        for (pos in matched) {
            val gem = board.gemAt(pos) ?: continue
            if (gem.special != null && gem.special != Special.HYPERCUBE) {
                extra += detonationCells(board, pos, gem.special)
            }
        }
        return extra
    }

    /**
     * Cells cleared when [hyperPos]'s hypercube triggers against the gem at
     * [partnerPos] (MECHANICS.md trigger rule and combo table). A plain partner
     * clears every gem of its color; a special partner powers every gem of its
     * color up into that special, which then detonates. The hypercube itself is
     * always consumed and included.
     */
    fun hypercubeTriggerCells(
        board: Board,
        hyperPos: Position,
        partnerPos: Position,
        partnerSpecial: Special?,
    ): Set<Position> {
        val partnerGem = board.gemAt(partnerPos)
            ?: return setOf(hyperPos)
        val color = partnerGem.type
        val affected = mutableSetOf(hyperPos)
        for (pos in board.positions()) {
            if (board.gemAt(pos)?.type != color) continue
            if (partnerSpecial == null) affected += pos
            else affected += detonationCells(board, pos, partnerSpecial)
        }
        return affected
    }

    /**
     * The cells cleared by a player-swap combo (MECHANICS.md combo table).
     * Both swapped positions count as cleared (the specials are consumed).
     */
    fun comboAffectedCells(
        board: Board,
        swapA: Position,
        swapB: Position,
        specA: Special,
        specB: Special,
    ): Set<Position> {
        // Hypercube combos: every gem of the swapped partner's color powers up
        // into the partner's special, then detonates simultaneously; the
        // hypercube itself is consumed too.
        if (specA == Special.HYPERCUBE || specB == Special.HYPERCUBE) {
            if (specA == Special.HYPERCUBE && specB == Special.HYPERCUBE) {
                return board.positions().toSet()
            }
            val hyperPos = if (specA == Special.HYPERCUBE) swapA else swapB
            val partnerPos = if (hyperPos == swapA) swapB else swapA
            val partnerSpec = if (specA == Special.HYPERCUBE) specB else specA
            return hypercubeTriggerCells(board, hyperPos, partnerPos, partnerSpec)
        }

        return when (setOf(specA, specB)) {
            setOf(Special.FLAME, Special.FLAME) -> blast(swapA, radius = 2, board)
            setOf(Special.FLAME, Special.STAR) -> thickCross(swapA, board)
            setOf(Special.STAR, Special.STAR) ->
                starLines(swapA, board) + starLines(swapB, board)
            else -> emptySet()
        }
    }

    // --- shape analysis -----------------------------------------------------

    private fun intersectionCell(board: Board, matches: List<Match>): Position? {
        val horizontal = matches.filter { it.positions.all { p -> p.row == it.positions.first().row } }
        val vertical = matches.filter { it.positions.all { p -> p.col == it.positions.first().col } }
        for (h in horizontal) {
            for (v in vertical) {
                for (pos in h.positions) {
                    if (pos in v.positions && board.gemAt(pos)?.special == null) return pos
                }
            }
        }
        return null
    }

    // --- area helpers -------------------------------------------------------

    private fun blast(center: Position, radius: Int, board: Board): Set<Position> {
        val cells = mutableSetOf<Position>()
        for (dr in -radius..radius) {
            for (dc in -radius..radius) {
                val pos = Position(center.row + dr, center.col + dc)
                if (board.isInside(pos)) cells += pos
            }
        }
        return cells
    }

    private fun starLines(center: Position, board: Board): Set<Position> {
        val cells = mutableSetOf<Position>()
        for (col in 0 until board.width) cells += Position(center.row, col)
        for (row in 0 until board.height) cells += Position(row, center.col)
        return cells
    }

    /** The 3-wide row + 3-wide column "thick cross" through [center]. */
    private fun thickCross(center: Position, board: Board): Set<Position> {
        val cells = mutableSetOf<Position>()
        for (dc in -1..1) {
            for (col in 0 until board.width) {
                val pos = Position(center.row + dc, col)
                if (board.isInside(pos)) cells += pos
            }
        }
        for (dr in -1..1) {
            for (row in 0 until board.height) {
                val pos = Position(row, center.col + dr)
                if (board.isInside(pos)) cells += pos
            }
        }
        return cells
    }
}