package com.matchthree.ui.game

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.gestures.detectDragGestures
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.Matrix
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.withTransform
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.IntSize
import kotlin.math.roundToInt
import com.matchthree.game.model.BoardConfig
import com.matchthree.game.model.GemType
import com.matchthree.game.model.Position
import com.matchthree.game.model.Special
import kotlin.math.abs
import kotlin.math.max
import kotlin.math.min

/**
 * The single Canvas that renders every gem on the board. Reads per-gem
 * [GemActor] animatable values in the draw pass — snapshot reads in draw
 * invalidate the draw phase only, so animations never trigger recomposition
 * (DESIGN.md: single-Canvas-first).
 *
 * Input (MECHANICS.md): drag past 40% of a cell to commit a directional swap;
 * tap-tap select-adjacent as the fallback. Both hand the resulting
 * [SwapIntent] to the ViewModel, which buffers it while steps are resolving.
 */
@Composable
fun BoardCanvas(
    player: StepPlayer,
    config: BoardConfig = BoardConfig(),
    selected: Position?,
    modifier: Modifier = Modifier,
    onSelect: (Position?) -> Unit,
    onSwapIntent: (SwapIntent) -> Unit,
) {
    Canvas(
        modifier = modifier.boardInput(player, config, selected, onSelect, onSwapIntent),
    ) {
        // Square-board assumption: cellSize from minDimension/config.width is
        // only correct while width == height (BoardConfig is locked 9x9 today).
        // A non-square config would need letterboxed cell math —
        // min(w/width, h/height) plus a centering origin offset — or the grid
        // and gem placement silently misalign.
        val cellSize = size.minDimension / config.width

        // Board background + faint grid.
        drawRect(BOARD_BACKGROUND, size = Size(size.width, size.height))
        val gridColor = Color.White.copy(alpha = 0.08f)
        for (row in 0..config.height) {
            val y = row * cellSize
            drawLine(gridColor, Offset(0f, y), Offset(size.width, y), strokeWidth = 1f)
        }
        for (col in 0..config.width) {
            val x = col * cellSize
            drawLine(gridColor, Offset(x, 0f), Offset(x, size.height), strokeWidth = 1f)
        }

        // All gems in one pass.
        for (actor in player.actors.values) {
            if (actor.alpha <= 0f) continue
            val centerPixels = Offset(actor.x * cellSize, actor.y * cellSize)
            val sprite = GemSprites.spriteFor(actor.special, actor.type)
            if (sprite != null) {
                // Sprite spans most of the cell; the remainder is gutter between gems.
                val gemSpan = cellSize * GemSprites.GEM_WIDTH_FRACTION * actor.scale
                drawCentered(sprite, centerPixels, gemSpan, actor.alpha)
                // Special overlays: Flame sits small in the middle, Star spans
                // the gem at half opacity (see GemSprites).
                val overlay = GemSprites.overlayFor(actor.special)
                if (overlay != null) {
                    val overlaySpan = gemSpan * GemSprites.overlayFraction(actor.special)
                    drawCentered(
                        overlay,
                        centerPixels,
                        overlaySpan,
                        actor.alpha * GemSprites.overlayAlpha(actor.special),
                    )
                    // Outlines traced from the sprite's alpha channel so they hug
                    // the silhouette (flame, sparkle) instead of its bounding box.
                    val outline = GemSprites.outlineFor(actor.special)
                    if (outline != null) {
                        val scale = overlaySpan / maxOf(overlay.width, overlay.height)
                        val matrix = Matrix().apply {
                            translate(centerPixels.x - overlay.width * scale / 2f, centerPixels.y - overlay.height * scale / 2f)
                            scale(scale, scale)
                        }
                        val color = when (actor.special) {
                            Special.FLAME -> Color.Black
                            Special.STAR -> Color.White.copy(alpha = GemSprites.STAR_OUTLINE_ALPHA)
                            else -> Color.Unspecified
                        }
                        drawScaledPath(
                            outline,
                            color,
                            matrix,
                            strokePx = 2f / scale,
                            alpha = actor.alpha * GemSprites.outlineAlpha(actor.special),
                        )
                    }
                }
            } else {
                drawCenteredCircle(actor, centerPixels, cellSize)
            }
        }

        // Selection marker for the tap-tap fallback: white outline around the
        // cell, roughly tracking the square silhouette of the gem sprites.
        if (selected != null) {
            val topLeft = Offset(selected.col * cellSize, selected.row * cellSize)
            drawRect(
                color = Color.White,
                topLeft = topLeft,
                size = Size(cellSize, cellSize),
                style = Stroke(width = max(2f, cellSize * 0.035f)),
            )
        }
    }
}

/** Attaches drag-to-swap + tap-tap input to the board modifier. */
private fun Modifier.boardInput(
    player: StepPlayer,
    config: BoardConfig,
    selected: Position?,
    onSelect: (Position?) -> Unit,
    onSwapIntent: (SwapIntent) -> Unit,
): Modifier {
    val columns = config.width
    val rows = config.height
    return this
        .pointerInput(columns, rows, selected) {
            detectTapGestures { offset ->
                val cell = cellAt(offset, columns, rows, size) ?: run {
                    onSelect(null)
                    return@detectTapGestures
                }
                when {
                    !player.hasGem(cell) -> onSelect(null)
                    selected == null -> onSelect(cell)
                    cell == selected -> onSelect(null)
                    cell.isOrthogonallyAdjacentTo(selected) -> {
                        onSelect(null)
                        onSwapIntent(SwapIntent.of(selected, cell))
                    }
                    else -> onSelect(cell)
                }
            }
        }
        .pointerInput(columns, rows) {
            var dragStart: Position? = null
            var dragAccum = Offset.Zero
            detectDragGestures(
                onDragStart = { start ->
                    dragStart = cellAt(start, columns, rows, size)
                    dragAccum = Offset.Zero
                },
                onDragEnd = { dragStart = null },
                onDragCancel = { dragStart = null },
                onDrag = { change, dragAmount ->
                    change.consume()
                    val startCell = dragStart ?: return@detectDragGestures
                    if (!player.hasGem(startCell)) return@detectDragGestures
                    dragAccum += dragAmount
                    val thresholdPx = min(size.width, size.height) / columns * 0.4f
                    if (dragAccum.getDistance() >= thresholdPx) {
                        val target = neighborInDominantAxis(startCell, dragAccum, columns, rows)
                        if (target != null) {
                            dragStart = null
                            dragAccum = Offset.Zero
                            onSwapIntent(SwapIntent.of(startCell, target))
                        }
                    }
                },
            )
        }
}

private fun cellAt(offset: Offset, columns: Int, rows: Int, size: IntSize): Position? {
    val cellSize = min(size.width, size.height) / columns.toFloat()
    val col = (offset.x / cellSize).toInt()
    val row = (offset.y / cellSize).toInt()
    if (col !in 0 until columns || row !in 0 until rows) return null
    return Position(row, col)
}

private fun neighborInDominantAxis(
    start: Position,
    accum: Offset,
    columns: Int,
    rows: Int,
): Position? {
    val horizontal = abs(accum.x) >= abs(accum.y)
    val target =
        if (horizontal) {
            if (accum.x > 0) Position(start.row, start.col + 1) else Position(start.row, start.col - 1)
        } else {
            if (accum.y > 0) Position(start.row + 1, start.col) else Position(start.row - 1, start.col)
        }
    return if (target.row in 0 until rows && target.col in 0 until columns) target else null
}

private val GEM_COLORS = mapOf(
    GemType.RED to Color(0xFFE53935),
    GemType.GREEN to Color(0xFF43A047),
    GemType.BLUE to Color(0xFF1E88E5),
    GemType.YELLOW to Color(0xFFFDD835),
    GemType.PURPLE to Color(0xFF8E24AA),
    GemType.ORANGE to Color(0xFFFB8C00),
)

/** Draws [path] with [matrix] applied, using a stroke of [strokePx] in the path's own space. */
private fun DrawScope.drawScaledPath(path: Path, color: Color, matrix: Matrix, strokePx: Float, alpha: Float) {
    if (color == Color.Unspecified) return
    withTransform({ transform(matrix) }) {
        drawPath(
            path = path,
            color = color,
            alpha = alpha,
            style = Stroke(width = strokePx),
        )
    }
}

/** Draws [image] centered at [center], fit inside [span]px with aspect preserved, at [alpha]. */
private fun DrawScope.drawCentered(image: ImageBitmap, center: Offset, span: Float, alpha: Float) {
    val scale = span.coerceAtLeast(1f) / maxOf(image.width, image.height)
    val dw = (image.width * scale).roundToInt()
    val dh = (image.height * scale).roundToInt()
    drawImage(
        image = image,
        srcOffset = IntOffset.Zero,
        srcSize = IntSize(image.width, image.height),
        dstOffset = IntOffset(
            (center.x - dw / 2f).roundToInt(),
            (center.y - dh / 2f).roundToInt(),
        ),
        dstSize = IntSize(dw, dh),
        alpha = alpha,
    )
}

/** Circle fallback when sprite art is unavailable (pre-load tests, missing PNG). */
private fun DrawScope.drawCenteredCircle(actor: GemActor, center: Offset, cellSize: Float) {
    val radius = cellSize * 0.36f * actor.scale
    if (radius < 2f) return
    val color = GEM_COLORS[actor.type] ?: Color.Magenta
    drawCircle(color = color, radius = radius, center = center, alpha = actor.alpha)
    drawCircle(
        color = Color.Black.copy(alpha = 0.25f),
        radius = radius,
        center = center,
        style = Stroke(width = max(1f, radius * 0.15f)),
    )
}

private val BOARD_BACKGROUND = Color(0xFF263238)