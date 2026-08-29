package com.matchthree.ui.game

import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.AnimationSpec
import androidx.compose.ui.geometry.Offset
import com.matchthree.game.engine.Step
import com.matchthree.game.model.GemType
import com.matchthree.game.model.Special
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.launch

/**
 * Animatable draw state for a single gem, keyed by the gem's stable [id].
 *
 * All 81 gems are rendered by ONE Canvas composable that reads these values.
 * Rendering is progress-driven: Canvas computes per-frame positions from
 * [startPos]..[endPos] * [progress] with scale/alpha alongside — so even with
 * hundreds of active gems the state is just 81 Animatables of progress.
 */
class GemActor internal constructor(
    val id: Int,
    val type: GemType,
    start: Offset,
    var special: Special? = null,
) {
    // Position progress 0..1 between startPos and endPos.
    private val progress = Animatable(0f)
    // Scale 0..1 and alpha 0..1 driven together in a single coroutine per step.
    private val scaleAlpha = Animatable(1f)

    private var startPos = start
    var endPos = start

    /** Board-cell position as progress interpolates startPos..endPos. */
    val y get() = startPos.y + (endPos.y - startPos.y) * progress.value
    /** Board-cell position as progress interpolates startPos..endPos. */
    val x get() = startPos.x + (endPos.x - startPos.x) * progress.value
    /** Scale factor (0..1). */
    val scale get() = scaleAlpha.value
    /** Alpha (0..1). */
    val alpha get() = scaleAlpha.value

    /** Snaps to a settled board position and resets scale/alpha to idle. */
    suspend fun snapTo(target: Offset) {
        progress.snapTo(1f)
        scaleAlpha.snapTo(1f)
        startPos = target
        endPos = target
    }

    /** Move from current position to [target] over [spec] (progress 0->1). */
    suspend fun moveTo(target: Offset, spec: AnimationSpec<Float>) {
        progress.snapTo(0f)
        startPos = Offset(x, y)
        endPos = target
        progress.animateTo(1f, spec)
    }

    /**
     * Places the gem at [position] with progress already settled.
     * Used for batch-pinning before batch fall/spawn (keeps per-actor bookkeeping).
     */
    suspend fun setPosition(position: Offset) {
        progress.snapTo(1f)
        startPos = position
        endPos = position
    }

    /** Begin fall from current position toward [target] over [spec], progress 0->1. */
    suspend fun fallTo(target: Offset, spec: AnimationSpec<Float>) {
        progress.snapTo(0f)
        startPos = Offset(x, y)
        endPos = target
        progress.animateTo(1f, spec)
    }

    /** Vanish (scale+alpha -> 0) over [spec]. */
    suspend fun vanish(spec: AnimationSpec<Float>) {
        scaleAlpha.animateTo(0f, spec)
    }

    /** Restore visible state (used at start of vanish if re-shown). */
    suspend fun show() {
        scaleAlpha.snapTo(1f)
    }
}
