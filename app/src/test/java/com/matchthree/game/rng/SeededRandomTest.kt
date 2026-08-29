package com.matchthree.game.rng

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Test

class SeededRandomTest {

    @Test
    fun `same seed reproduces same sequence`() {
        val a = SeededRandom(42L).let { r -> (0 until 100).map { r.nextInt(6) } }
        val b = SeededRandom(42L).let { r -> (0 until 100).map { r.nextInt(6) } }
        assertEquals(a, b)
    }

    @Test
    fun `different seed produces different sequence`() {
        val a = SeededRandom(1L).let { r -> (0 until 50).map { r.nextInt(6) } }
        val b = SeededRandom(2L).let { r -> (0 until 50).map { r.nextInt(6) } }
        assertNotEquals(a, b)
    }

    @Test
    fun `nextInt respects bounds`() {
        val rng = SeededRandom(7L)
        repeat(1_000) {
            val value = rng.nextInt(6)
            assert(value in 0..5) { "out of bounds: $value" }
        }
    }
}