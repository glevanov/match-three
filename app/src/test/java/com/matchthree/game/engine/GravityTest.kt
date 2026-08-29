package com.matchthree.game.engine

import com.matchthree.game.Boards
import com.matchthree.game.model.Position
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class GravityTest {

    @Test
    fun `gems fall to fill cleared bottom row`() {
        val board = Boards.fromRows("RYR", "BYB", "GYG")
        val result = Gravity.apply(board, setOf(Position(2, 0), Position(2, 1), Position(2, 2)))

        // All six survivors drop by one: three row-1 gems to the bottom, three row-0 gems to the middle.
        assertEquals(6, result.falls.size)
        assertEquals((0..5).toList(), result.falls.map { it.gemId }.sorted())
        assertEquals("BYB", charRow(result, 2))   // row-1 gems now sit on the bottom
        assertEquals("RYR", charRow(result, 1))   // row-0 gems moved down one
        assertNull(result.board.gemAt(0, 0))      // top row is free for refill
        assertNull(result.board.gemAt(0, 1))
        assertNull(result.board.gemAt(0, 2))
    }

    @Test
    fun `gaps inside a column collapse preserving order`() {
        // One column: O(0) B(1) Y(2) G(3) R(4); destroy B(row1) and G(row3).
        val board = Boards.fromRows(
            "O",
            "B",
            "Y",
            "G",
            "R",
        )
        val result = Gravity.apply(board, setOf(Position(1, 0), Position(3, 0)))

        // Y(id2) moves 2->3, O(id0) moves 0->2.
        assertEquals(setOf(0, 2), result.falls.map { it.gemId }.toSet())
        assertEquals("O", charAt(result, 2)) // O dropped from row0 to row2
        assertEquals("Y", charAt(result, 3)) // Y dropped from row2 to row3
        assertEquals("R", charAt(result, 4)) // R stays put
        assertNull(result.board.gemAt(0, 0))
        assertNull(result.board.gemAt(1, 0))
    }

    @Test
    fun `destroying a whole column produces no falls in it`() {
        val board = Boards.fromRows("O", "B", "Y")
        val result = Gravity.apply(board, setOf(Position(0, 0), Position(1, 0), Position(2, 0)))
        assertEquals(0, result.falls.size)
        assertNull(result.board.gemAt(0, 0))
    }

    private fun charRow(result: Gravity.Result, row: Int): String =
        (0 until result.board.width).joinToString("") { col -> charAt(result, row, col) }

    private fun charAt(result: Gravity.Result, row: Int, col: Int = 0): String {
        val gem = result.board.gemAt(row, col)
        return gem?.let { Boards.typeToChar(it.type).toString() } ?: "."
    }
}