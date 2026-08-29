package com.matchthree.ui.game

import android.util.Log
import androidx.compose.runtime.withFrameNanos
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import java.util.Locale

/**
 * Measures real rendered frame intervals (nanos between withFrameNanos ticks)
 * while [block] runs, then logs the aggregate via [TAG]. Used to validate the
 * worst-case full-board clear animation (M2 acceptance): `adb logcat -s
 * MatchThree.FrameTiming` prints one line per measured session.
 *
 * Deliberately debug-visible and not wired into everyday play: sessions are
 * only started by explicit callers (the debug worst-case control).
 */
object FrameTimeTracker {

    const val TAG = "MatchThree.FrameTiming"

    suspend fun measure(session: String, block: suspend () -> Unit): FrameStats {
        val intervals = mutableListOf<Long>()
        var stats: FrameStats
        coroutineScope {
            val sampler = launch {
                var lastNanos = -1L
                while (isActive) {
                    val now = withFrameNanos { it }
                    if (lastNanos != -1L) intervals += now - lastNanos
                    lastNanos = now
                }
            }
            try {
                block()
            } finally {
                sampler.cancel()
            }
        }
        stats = FrameStats.compute(intervals)
        Log.i(
            TAG,
            String.format(
                Locale.US,
                "%s frames=%d avg=%.2fms p50=%.2fms p95=%.2fms p99=%.2fms max=%.2fms budget=%.2fms withinBudget=%b",
                session,
                stats.sampleCount,
                stats.avgMillis,
                stats.p50Millis,
                stats.p95Millis,
                stats.p99Millis,
                stats.maxMillis,
                FrameStats.FRAME_BUDGET_MILLIS,
                stats.withinBudget,
            ),
        )
        return stats
    }
}