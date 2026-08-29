package com.matchthree.ui

import androidx.lifecycle.ViewModel
import com.matchthree.game.engine.GameEngine
import com.matchthree.game.engine.Step
import com.matchthree.game.model.Board
import com.matchthree.game.model.BoardConfig
import com.matchthree.game.rng.SeededRandom
import com.matchthree.ui.game.SwapIntent
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update

/**
 * UI-side game state. Pure logic lives in the engine; this ViewModel owns the
 * run loop: submit -> engine resolves -> steps handed to the UI to play back.
 *
 * Input lock (MECHANICS.md/decisions log): while steps are resolving OR a
 * rejection animation is playing, new swap intents are buffered (most recent
 * wins) and executed when the current animation settles. Nothing is dropped.
 */
class GameViewModel : ViewModel() {

    private val config = BoardConfig()
    private val engine = GameEngine(config, rng = SeededRandom(System.nanoTime()))

    private var board: Board = engine.newGame()
    private var phase = GamePhase.Idle
    private var bufferedSwap: SwapIntent? = null

    private val _uiState = MutableStateFlow(GameUiState(board = board, phase = phase))
    val uiState: StateFlow<GameUiState> = _uiState.asStateFlow()

    /** Called by the UI when it starts a swap/drag intent. */
    fun submitSwap(intent: SwapIntent) {
        if (phase != GamePhase.Idle) {
            bufferedSwap = intent
            return
        }
        startResolution(intent)
    }

    /** Called by the StepPlayer after it has played a full resolution. */
    fun onStepsPlayed(settledBoard: Board) {
        attach(settledBoard)
    }

    /** Called by the StepPlayer after an invalid-swap there-and-back. */
    fun onRejectionPlayed() {
        attach(board)
    }

    /**
     * Debug-only worst-case render stress: simulates a Hypercube+Hypercube
     * full-board clear (planned for M4) so M2 can measure frame timing under
     * max load (81 simultaneous shrinks, then 81 spawns falling in together).
     */
    fun debugFullClear() {
        if (phase != GamePhase.Idle) return
        val target = engine.newGame()
        val allCells = board.positions()
        val destroyedStep = Step.Destroy(allCells.toSet())
        val placements = allCells.mapNotNull { pos ->
            target.gemAt(pos)?.let { Step.Spawn.Placement(it, pos) }
        }
        val steps = listOf(destroyedStep, Step.Spawn(placements), Step.Settled(target))
        phase = GamePhase.Resolving
        _uiState.update {
            it.copy(
                phase = GamePhase.Resolving,
                pendingPlayback = Playback(
                    label = "debug full-board-clear (81 gems)",
                    steps = steps,
                    measureFrames = true,
                ),
            )
        }
    }

    private fun startResolution(intent: SwapIntent) {
        val resolved = engine.resolveSwap(board, intent.a, intent.b)
        if (resolved == null) {
            phase = GamePhase.Rejecting
            _uiState.update {
                it.copy(phase = GamePhase.Rejecting, rejectedSwap = intent)
            }
            return
        }
        phase = GamePhase.Resolving
        _uiState.update {
            it.copy(
                phase = GamePhase.Resolving,
                pendingPlayback = Playback(
                    label = "swap @(${intent.a.row},${intent.a.col})<->(${intent.b.row},${intent.b.col})",
                    steps = resolved.steps,
                    measureFrames = false,
                ),
            )
        }
    }

    private fun attach(settledBoard: Board) {
        board = settledBoard
        phase = GamePhase.Idle
        _uiState.update {
            it.copy(board = board, phase = GamePhase.Idle, pendingPlayback = null, rejectedSwap = null)
        }
        bufferedSwap?.let { swap ->
            bufferedSwap = null
            startResolution(swap)
        }
    }
}

/** Input/lock state + what the UI should play next. */
enum class GamePhase { Idle, Resolving, Rejecting }

data class GameUiState(
    val board: Board,
    val phase: GamePhase,
    val pendingPlayback: Playback? = null,
    val rejectedSwap: SwapIntent? = null,
    val score: Int = 0,
)

/** A batch of engine steps handed to the UI for playback. */
data class Playback(
    val label: String,
    val steps: List<Step>,
    val measureFrames: Boolean = false,
)