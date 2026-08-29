package com.matchthree.game.engine

import com.matchthree.game.Boards
import com.matchthree.game.model.Position
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class MatchDetectorTest {

    private val detector = MatchDetector

    @Test
    fun `horizontal run of 3 is found`() {
        val board = Boards.fromRows("RRR")
        val matches = detector.findMatches(board)
        assertEquals(1, matches.size)
        assertEquals(listOf(Position(0, 0), Position(0, 1), Position(0, 2)), matches[0].positions)
    }

    @Test
    fun `run of 4 is a single maximal match`() {
        val board = Boards.fromRows("RRRR")
        val matches = detector.findMatches(board)
        assertEquals(1, matches.size)
        assertEquals(4, matches[0].size)
        assertEquals(listOf(Position(0, 0), Position(0, 1), Position(0, 2), Position(0, 3)), matches[0].positions)
    }

    @Test
    fun `vertical run of 3 is found`() {
        val board = Boards.fromRows("R..", "R..", "R..")
        val matches = detector.findMatches(board)
        assertEquals(1, matches.size)
        assertEquals(listOf(Position(0, 0), Position(1, 0), Position(2, 0)), matches[0].positions)
    }

    @Test
    fun `L shape yields horizontal and vertical matches`() {
        val board = Boards.fromRows(
            "RRR",
            "..R",
            "..R",
        )
        val matches = detector.findMatches(board)
        assertEquals(2, matches.size)
        assertTrue(matches.any { it.positions.all { p -> p.row == 0 } })
        assertTrue(matches.any { it.positions.all { p -> p.col == 2 } })
    }

    @Test
    fun `two separate runs in one row are both found`() {
        val board = Boards.fromRows("RRRBBRRR")
        val matches = detector.findMatches(board)
        assertEquals(2, matches.size)
        assertEquals(3, matches[0].size)
        assertEquals(3, matches[1].size)
    }

    @Test
    fun `alternating board has no matches`() {
        val board = Boards.fromRows(
            "RYRYRYRYR",
            "YRYRYRYRY",
            "RYRYRYRYR",
        )
        assertEquals(0, detector.findMatches(board).size)
    }

    @Test
    fun `empty cells break runs`() {
        val board = Boards.fromRows("RR.RR")
        assertEquals(0, detector.findMatches(board).size)
    }
}