package com.matchthree.game.engine

import com.matchthree.game.Boards
import com.matchthree.game.model.GemType
import com.matchthree.game.model.Position
import org.junit.Assert.assertEquals
import org.junit.Test

class RefillTest {

    private val idSource = IdSource(100)

    @Test
    fun `refill spawns gems only in top gaps`() {
        // Gaps: entirely empty column 0 (3 cells) and top gap in column 1 (2 cells).
        val board = Boards.fromRows(
            "..G",
            "..B",
            ".RY",
        )
        val result = Refill.fill(board, 6, idSource::next) { GemType.RED }

        assertEquals(5, result.spawned.size)
        assertEquals(
            setOf(Position(0, 0), Position(1, 0), Position(2, 0), Position(0, 1), Position(1, 1)),
            result.spawned.map { it.position }.toSet(),
        )
        // New ids are unique and issued from the source.
        assertEquals(5, result.spawned.map { it.gem.id }.toSet().size)
        assertEquals(105, idSource.next()) // next id after the five spawns
        // Survivors keep their identity.
        assertEquals(GemType.GREEN, result.board.gemAt(0, 2)?.type)
        assertEquals(GemType.BLUE, result.board.gemAt(1, 2)?.type)
        assertEquals(GemType.YELLOW, result.board.gemAt(2, 2)?.type)
    }

    @Test
    fun `full board needs no spawns`() {
        val board = Boards.fromRows("RYR", "BYB")
        val result = Refill.fill(board, 6, idSource::next) { GemType.RED }
        assertEquals(0, result.spawned.size)
    }
}