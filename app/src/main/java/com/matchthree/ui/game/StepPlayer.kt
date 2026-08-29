package com.matchthree.ui.game

import androidx.compose.animation.core.AnimationSpec
import androidx.compose.animation.core.FastOutSlowInEasing
import androidx.compose.animation.core.tween
import androidx.compose.runtime.mutableStateMapOf
import androidx.compose.runtime.snapshots.SnapshotStateMap
import androidx.compose.ui.geometry.Offset
import com.matchthree.game.engine.Step
import com.matchthree.game.model.Board
import com.matchthree.game.model.BoardConfig
import com.matchthree.game.model.Position
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.launch
import kotlin.math.abs

/**
 * Plays back engine [Step] events as animations over the shared actor pool.
 *
 * The player owns the only mutable render state: a gem-id -> [GemActor] map plus
 * a logical id grid (which gem sits in which cell). The Canvas composable just
 * reads the actors; the ViewModel just resolves steps. This is the M2
 * incarnation of "UI plays back Steps" (AGENTS.md).
 *
 * Timing constants (from MECHANICS.md where they exist):
 *  - swap: ~150ms
 *  - invalid swap there-and-back: ~150ms
 *  - destroy/shrink: 200ms
 *  - falls/spawns: ~70ms per row, min 90ms (constant speed, simultaneous landing)
 */
class StepPlayer(
    private val config: BoardConfig = BoardConfig(),
    private val onSettled: (Board) -> Unit,
    private val onScore: (Int) -> Unit = {},
) {
    private val _actors = mutableStateMapOf<Int, GemActor>()

    /** Read-only view for the Canvas draw pass (redraws on change, no recomposition). */
    val actors: SnapshotStateMap<Int, GemActor> = _actors

    /** Logical gem-id grid in board-cell space; `null` = empty cell. */
    private var ids: Array<Array<Int?>> =
        Array(config.height) { arrayOfNulls(config.width) }

    fun hasGem(position: Position): Boolean =
        position.row in 0 until config.height &&
            position.col in 0 until config.width &&
            ids[position.row][position.col] != null

    /** Snaps the actor pool to a settled [board] (initial load and post-playback). */
    suspend fun applyBoard(board: Board) {
        val liveIds = board.positions().mapNotNull { board.gemAt(it)?.id }.toSet()
        _actors.keys.filterTo(mutableListOf()) { it !in liveIds }.forEach { id ->
            _actors.remove(id)
        }
        for (row in 0 until board.height) {
            for (col in 0 until board.width) {
                val gem = board.gemAt(row, col) ?: continue
                ids[row][col] = gem.id
                _actors[gem.id]?.snapTo(cellCenter(row, col))
                    ?: run { _actors[gem.id] = GemActor(gem.id, gem.type, cellCenter(row, col)) }
            }
        }
    }

    /** Plays a full engine resolution; invokes [onSettled] after the last step. */
    suspend fun play(steps: List<Step>) {
        steps.forEach { step -> playStep(step) }
        val settled = steps.filterIsInstance<Step.Settled>().lastOrNull()
        if (settled != null) onSettled(settled.board)
    }

    /** Animates an invalid swap: swap across and right back (no onSettled). */
    suspend fun playRejection(a: Position, b: Position) {
        swapActors(a, b)
        swapActors(a, b)
    }

    private suspend fun playStep(step: Step) {
        when (step) {
            is Step.Swap -> swapActors(step.a, step.b)
            is Step.Destroy -> destroyActors(step.positions)
            is Step.Score -> onScore(step.delta) // points accumulate live
            is Step.Fall -> fallActors(step.moves)
            is Step.Spawn -> spawnActors(step.gems)
            is Step.Settled -> Unit // handled in play()
        }
    }

    private suspend fun swapActors(a: Position, b: Position) {
        val idA = ids[a.row][a.col] ?: return
        val idB = ids[b.row][b.col] ?: return
        val actorA = _actors[idA] ?: return
        val actorB = _actors[idB] ?: return
        val targetA = cellCenter(b.row, b.col)
        val targetB = cellCenter(a.row, a.col)
        val spec = SWAP_SPEC
        coroutineScope {
            launch { actorA.moveTo(targetA, spec) }
            launch { actorB.moveTo(targetB, spec) }
        }
        ids[a.row][a.col] = idB
        ids[b.row][b.col] = idA
    }

    private suspend fun destroyActors(positions: Set<Position>) {
        val doomed = mutableListOf<GemActor>()
        for (p in positions) {
            val id = ids[p.row][p.col]
            if (id != null) {
                _actors[id]?.let { doomed += it }
                ids[p.row][p.col] = null
            }
        }
        if (doomed.isEmpty()) return
        coroutineScope {
            doomed.forEach { actor ->
                launch { actor.vanish(DESTROY_SPEC) }
            }
        }
        doomed.forEach { _actors.remove(it.id) }
    }

    private suspend fun fallActors(falls: List<Step.Fall.FallMove>) {
        if (falls.isEmpty()) return
        coroutineScope {
            falls.forEach { fall ->
                launch {
                    val actor = _actors[fall.gemId] ?: return@launch
                    val target = cellCenter(fall.to.row, fall.to.col)
                    val rows = abs(fall.to.row - fall.from.row)
                    actor.moveTo(target, fallSpec(rows))
                    ids[fall.to.row][fall.to.col] = fall.gemId
                    ids[fall.from.row][fall.from.col] = null
                }
            }
        }
    }

    private suspend fun spawnActors(spawned: List<Step.Spawn.Placement>) {
        if (spawned.isEmpty()) return
        // All gems start the same distance above their target so every column's
        // refill lands simultaneously at constant speed.
        val maxTargetRow = spawned.maxOf { it.position.row }
        val distance = maxTargetRow + 1
        coroutineScope {
            spawned.forEach { placement ->
                launch {
                    val gem = placement.gem
                    val target = cellCenter(placement.position.row, placement.position.col)
                    val start = Offset(target.x, target.y - distance)
                    val actor = GemActor(gem.id, gem.type, start)
                    _actors[gem.id] = actor
                    ids[placement.position.row][placement.position.col] = gem.id
                    actor.moveTo(target, fallSpec(distance))
                }
            }
        }
    }

    private fun cellCenter(row: Int, col: Int): Offset =
        Offset(col + 0.5f, row + 0.5f)

    private fun fallSpec(rows: Int): AnimationSpec<Float> =
        tween(durationMillis = FALL_BASE_MILLIS + rows * FALL_PER_ROW_MILLIS, easing = FastOutSlowInEasing)

    private companion object {
        const val SWAP_MILLIS = 150
        const val DESTROY_MILLIS = 200
        const val FALL_BASE_MILLIS = 90
        const val FALL_PER_ROW_MILLIS = 70

        val SWAP_SPEC = tween<Float>(SWAP_MILLIS, easing = FastOutSlowInEasing)
        val DESTROY_SPEC = tween<Float>(DESTROY_MILLIS, easing = FastOutSlowInEasing)
    }
}