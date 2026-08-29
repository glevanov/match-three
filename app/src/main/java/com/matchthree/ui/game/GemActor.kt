package com.matchthree.ui.game

import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.AnimationSpec
import androidx.compose.ui.geometry.Offset
import com.matchthree.game.model.GemType
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.launch

/**
 * Animatable draw state for a single gem, keyed by the gem's stable [id].
 *
 * All 81 gems are rendered by ONE Canvas composable that reads these values;
 * the animatables are per-gem but never per-composable (DESIGN.md:
 * single-Canvas-first).
 *
 * [x]/[y] are in board-cell units (0..8, centers at `col + 0.5` / `row + 0.5`)
 * so the Canvas resolves them to pixels per frame — animation is independent of
 * the actual viewport size.
 */
class GemActor internal constructor(
    val id: Int,
    val type: GemType,
    start: Offset,
) {
    val x = Animatable(start.x)
    val y = Animatable(start.y)
    val scale = Animatable(1f)
    val alpha = Animatable(1f)

    /** Instantly places the gem at [target] (used when snapping settled boards). */
    suspend fun snapTo(target: Offset) {
        x.snapTo(target.x)
        y.snapTo(target.y)
    }

    /** Animates the gem's position to [target] under [spec]. */
    suspend fun moveTo(target: Offset, spec: AnimationSpec<Float>) {
        coroutineScope {
            launch { x.animateTo(target.x, spec) }
            launch { y.animateTo(target.y, spec) }
        }
    }

    /** Animates the gem away (clear/destroy); used before dropping the actor. */
    suspend fun vanish(spec: AnimationSpec<Float>) {
        coroutineScope {
            launch { scale.animateTo(0f, spec) }
            launch { alpha.animateTo(0f, spec) }
        }
    }
}