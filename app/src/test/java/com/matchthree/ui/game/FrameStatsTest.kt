package com.matchthree.ui.game

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class FrameStatsTest {

    @Test
    fun `percentiles and aggregates are correct for a known sample`() {
        // 10ms, 12ms, 16ms, 20ms, 30ms
        val intervalsNanos = listOf(10_000_000L, 12_000_000L, 16_000_000L, 20_000_000L, 30_000_000L)

        val stats = FrameStats.compute(intervalsNanos)

        assertEquals(5, stats.sampleCount)
        assertEquals(17.6, stats.avgMillis, 1e-9)
        assertEquals(16.0, stats.p50Millis, 1e-9) // index 2 of sorted
        assertEquals(20.0, stats.p95Millis, 1e-9) // index 3 of sorted
        assertEquals(20.0, stats.p99Millis, 1e-9) // index 3 of sorted
        assertEquals(30.0, stats.maxMillis, 1e-9)
        assertFalse("p95=20ms exceeds the 16.67ms budget", stats.withinBudget)
    }

    @Test
    fun `all-frames-within-budget reports withinBudget`() {
        val frames = List(60) { 16_000_000L } // ~16ms frames, under budget
        val stats = FrameStats.compute(frames)

        assertTrue(stats.withinBudget)
        assertEquals(16.0, stats.p95Millis, 1e-9)
        assertEquals(16.0, stats.maxMillis, 1e-9)
    }

    @Test(expected = IllegalArgumentException::class)
    fun `empty sample is rejected`() {
        FrameStats.compute(emptyList())
    }
}