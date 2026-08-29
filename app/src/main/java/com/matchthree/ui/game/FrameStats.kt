package com.matchthree.ui.game

/**
 * Aggregate frame-interval statistics for one measured animation session.
 * Kept free of Android/Compose imports so the math is JVM-unit-testable.
 *
 * [withinBudget] is true when the 95th percentile frame duration stays within
 * the [FRAME_BUDGET_MILLIS] budget (roughly a 60 fps vsync).
 */
data class FrameStats(
    val sampleCount: Int,
    val avgMillis: Double,
    val p50Millis: Double,
    val p95Millis: Double,
    val p99Millis: Double,
    val maxMillis: Double,
    val withinBudget: Boolean,
) {
    companion object {
        const val FRAME_BUDGET_MILLIS = 16.667

        /** Computes stats from raw frame intervals (in nanoseconds). */
        fun compute(intervalsNanos: List<Long>): FrameStats {
            require(intervalsNanos.isNotEmpty()) { "need at least one frame interval" }
            val sorted = intervalsNanos.sorted()
            fun toMillis(nanos: Long) = nanos / 1_000_000.0

            fun percentile(p: Double): Double {
                val index = ((sorted.size - 1) * p).toInt()
                return toMillis(sorted[index])
            }

            val avg = intervalsNanos.average() / 1_000_000.0
            val p50 = percentile(0.50)
            val p95 = percentile(0.95)
            val p99 = percentile(0.99)
            val max = toMillis(sorted.last())
            return FrameStats(
                sampleCount = intervalsNanos.size,
                avgMillis = avg,
                p50Millis = p50,
                p95Millis = p95,
                p99Millis = p99,
                maxMillis = max,
                withinBudget = p95 <= FRAME_BUDGET_MILLIS,
            )
        }
    }
}