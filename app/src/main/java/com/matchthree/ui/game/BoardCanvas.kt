package com.matchthree.ui.game

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.gestures.detectDragGestures
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.unit.IntSize
import com.matchthree.game.model.BoardConfig
import com.matchthree.game.model.GemType
import com.matchthree.game.model.Position
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
            if (actor.alpha.value <= 0f) continue
            val center = Offset(actor.x.value * cellSize, actor.y.value * cellSize)
            val radius = cellSize * 0.36f * actor.scale.value
            drawCircle(
                color = actor.type.color(),
                radius = radius,
                center = center,
                alpha = actor.alpha.value,
            )
            if (radius >= 2f) {
                drawCircle(
                    color = Color.Black.copy(alpha = 0.25f),
                    radius = radius,
                    center = center,
                    style = Stroke(width = max(1f, radius * 0.15f), cap = StrokeCap.Round),
                )
            }
        }

        // Selection ring for the tap-tap fallback.
        if (selected != null) {
            val c = Offset((selected.col + 0.5f) * cellSize, (selected.row + 0.5f) * cellSize)
            drawCircle(
                color = Color.White,
                radius = cellSize * 0.42f,
                center = c,
                style = Stroke(width = 2f),
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

private fun GemType.color(): Color = when (this) {
    GemType.RED -> Color(0xFFE53935)
    GemType.GREEN -> Color(0xFF43A047)
    GemType.BLUE -> Color(0xFF1E88E5)
    GemType.YELLOW -> Color(0xFFFDD835)
    GemType.PURPLE -> Color(0xFF8E24AA)
    GemType.ORANGE -> Color(0xFFFB8C00)
}

private val BOARD_BACKGROUND = Color(0xFF263238)