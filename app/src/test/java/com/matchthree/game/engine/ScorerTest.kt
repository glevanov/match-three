package com.matchthree.game.engine

import com.matchthree.game.model.Position
import org.junit.Assert.assertEquals
import org.junit.Test

class ScorerTest {

    @Test
    fun `base rate is 10 points per unique gem at depth 1`() {
        assertEquals(30, Scorer.roundScore(setOf(pos(0, 0), pos(0, 1), pos(0, 2)), 1))
        assertEquals(50, Scorer.roundScore(setOf(pos(1, 0), pos(1, 1), pos(1, 2), pos(2, 2), pos(3, 2)), 1))
    }

    @Test
    fun `cascade multiplier scales linearly and uncapped by depth`() {
        val cells = setOf(pos(0, 0), pos(0, 1), pos(0, 2))
        assertEquals(30, Scorer.roundScore(cells, 1))
        assertEquals(60, Scorer.roundScore(cells, 2))
        assertEquals(90, Scorer.roundScore(cells, 3))
        assertEquals(300, Scorer.roundScore(cells, 10))
    }

    @Test
    fun `overlapping clear regions are counted once (unique-cell scoring)`() {
        // A row clear and a column clear sharing the crossing cell (2,2):
        // 5 unique cells, not 6.
        val rowClear = listOf(pos(2, 0), pos(2, 1), pos(2, 2))
        val columnClear = listOf(pos(0, 2), pos(1, 2), pos(2, 2))
        val combined = rowClear + columnClear
        assertEquals(5, combined.toSet().size)
        assertEquals(50, Scorer.roundScore(combined, 1))
    }

    @Test
    fun `duplicates passed to a single round are deduped`() {
        assertEquals(20, Scorer.roundScore(listOf(pos(0, 0), pos(0, 1), pos(0, 0)), 1))
    }

    @Test
    fun `total score sums every round`() {
        assertEquals(0, Scorer.totalScore(emptyList()))
        assertEquals(30, Scorer.totalScore(listOf(30)))
        assertEquals(30 + 60 + 90, Scorer.totalScore(listOf(30, 60, 90)))
    }

    @Test(expected = IllegalArgumentException::class)
    fun `depth below 1 is rejected`() {
        Scorer.roundScore(emptySet(), 0)
    }

    private fun pos(row: Int, col: Int) = Position(row, col)
}