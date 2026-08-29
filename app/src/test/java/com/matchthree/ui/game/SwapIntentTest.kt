package com.matchthree.ui.game

import com.matchthree.game.model.Position
import org.junit.Assert.assertEquals
import org.junit.Assert.assertThrows
import org.junit.Test

class SwapIntentTest {

    @Test
    fun `horizontal swap normalizes to left cell first`() {
        val intent = SwapIntent.of(Position(3, 5), Position(3, 6))
        assertEquals(Position(3, 5), intent.a)
        assertEquals(Position(3, 6), intent.b)
    }

    @Test
    fun `reversed horizontal swap yields identical intent`() {
        assertEquals(
            SwapIntent.of(Position(3, 5), Position(3, 6)),
            SwapIntent.of(Position(3, 6), Position(3, 5)),
        )
    }

    @Test
    fun `vertical swap normalizes to upper cell first`() {
        val intent = SwapIntent.of(Position(8, 2), Position(7, 2))
        assertEquals(Position(7, 2), intent.a)
        assertEquals(Position(8, 2), intent.b)
    }

    @Test
    fun `non-adjacent cells are rejected`() {
        assertThrows(IllegalArgumentException::class.java) {
            SwapIntent.of(Position(0, 0), Position(2, 2))
        }
    }
}