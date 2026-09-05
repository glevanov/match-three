package com.matchthree.ui

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import androidx.lifecycle.viewmodel.initializer
import androidx.lifecycle.viewmodel.viewModelFactory
import com.matchthree.game.engine.GameEngine
import com.matchthree.game.engine.LegalMoveDetector
import com.matchthree.game.engine.Step
import com.matchthree.game.model.Board
import com.matchthree.game.model.BoardConfig
import com.matchthree.game.rng.SeededRandom
import com.matchthree.game.model.Gem
import com.matchthree.ui.game.SwapIntent
import com.matchthree.ui.game.bufferedSwapIsStale
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

/**
 * UI-side game state. Pure logic lives in the engine; this ViewModel owns the
 * run loop: submit -> engine resolves -> steps handed to the UI to play back.
 *
 * Input lock (MECHANICS.md/decisions log): while steps are resolving OR a
 * rejection animation is playing, new swap intents are buffered (most recent
 * wins) and executed when the current animation settles. Nothing is dropped —
 * except a stale intent whose pair gained or lost a Hypercube during the
 * resolution (bufferedSwapIsStale): a Hypercube must only be consumed by a
 * gesture that targeted it.
 *
 * M3: score accumulates as Score steps are played; Classic mode runs a
 * countdown timer; Zen mode ends when the board is dead AND reshuffle fails
 * (MECHANICS.md). M5: the mode is chosen on the menu screen and passed in at
 * construction via the companion [factory].
 */
class GameViewModel(initialMode: GameMode = GameMode.CLASSIC) : ViewModel() {

    companion object {
        /** Creates a ViewModel for the given mode (used by the M5 menu). */
        fun factory(mode: GameMode): ViewModelProvider.Factory = viewModelFactory {
            initializer { GameViewModel(mode) }
        }

        /** Placeholder Classic round length; tunable when the M5 menu lands. */
        const val CLASSIC_TIMER_SECONDS = 75
    }

    private val config = BoardConfig()
    private val engine = GameEngine(config, rng = SeededRandom(System.nanoTime()))

    private var board: Board = engine.newGame()
    private var phase = GamePhase.Idle
    /** A swap buffered during input lock, with the pair's gems as the drag saw them. */
    private data class BufferedSwap(val intent: SwapIntent, val a: Gem?, val b: Gem?)

    private var bufferedSwap: BufferedSwap? = null
    private var mode = initialMode
    private var timerJob: Job? = null

    private val _uiState = MutableStateFlow(GameUiState(board = board, phase = phase, mode = mode))
    val uiState: StateFlow<GameUiState> = _uiState.asStateFlow()

    init {
        startTimerIfClassic()
    }

    /** Called by the UI when it starts a swap/drag intent. */
    fun submitSwap(intent: SwapIntent) {
        if (phase == GamePhase.GameOver) return
        if (phase != GamePhase.Idle) {
            // board is the last settled board — exactly what the drag was made against.
            bufferedSwap = BufferedSwap(intent, board.gemAt(intent.a), board.gemAt(intent.b))
            return
        }
        startResolution(intent)
    }

    /** Called by the StepPlayer after it has played a full resolution. */
    fun onStepsPlayed(settledBoard: Board) {
        if (phase == GamePhase.GameOver) return
        attach(settledBoard)
    }

    /** Called by the StepPlayer after an invalid-swap there-and-back. */
    fun onRejectionPlayed() {
        if (phase == GamePhase.GameOver) return
        attach(board)
    }

    /** Called by the StepPlayer as each Score step is played back. */
    fun addScore(delta: Int) {
        if (phase == GamePhase.GameOver) return
        _uiState.update { it.copy(score = it.score + delta) }
    }

    /** New game with the current mode (used by the GameOver screen). */
    fun restart() {
        timerJob?.cancel()
        board = engine.newGame()
        phase = GamePhase.Idle
        bufferedSwap = null
        _uiState.update {
            it.copy(
                board = board,
                phase = GamePhase.Idle,
                pendingPlayback = null,
                rejectedSwap = null,
                score = 0,
                gameOverReason = null,
            )
        }
        startTimerIfClassic()
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

        // Board invariants (MECHANICS.md): a dead board is reshuffled; if even
        // the reshuffle fails there is no way to keep playing.
        if (!LegalMoveDetector.hasLegalMove(board)) {
            val reshuffled = engine.reshuffle(board)
            if (reshuffled == null) {
                endGame("No moves left")
                return
            }
            bufferedSwap = null // layout changed; stale intents are dropped
            board = reshuffled
            _uiState.update { it.copy(board = board) }
        }

        bufferedSwap?.let { swap ->
            bufferedSwap = null
            val stale = bufferedSwapIsStale(
                submitA = swap.a,
                submitB = swap.b,
                settledA = board.gemAt(swap.intent.a),
                settledB = board.gemAt(swap.intent.b),
            )
            if (!stale) startResolution(swap.intent)
            // Stale per MECHANICS.md: a Hypercube entered or left the pair during
            // resolution, so this gesture must not consume a Hypercube it never
            // targeted. Same precedent as the reshuffle branch above.
        }
    }

    private fun endGame(reason: String) {
        timerJob?.cancel()
        phase = GamePhase.GameOver
        bufferedSwap = null
        _uiState.update {
            it.copy(phase = GamePhase.GameOver, gameOverReason = reason, pendingPlayback = null, rejectedSwap = null)
        }
    }

    /** Classic mode: 75s countdown placeholder (timer spec: M3 detail). */
    private fun startTimerIfClassic() {
        timerJob?.cancel()
        if (mode != GameMode.CLASSIC) {
            _uiState.update { it.copy(secondsLeft = null) }
            return
        }
        timerJob = viewModelScope.launch {
            var remaining = CLASSIC_TIMER_SECONDS
            while (remaining > 0) {
                _uiState.update { it.copy(secondsLeft = remaining) }
                delay(1_000)
                remaining--
            }
            _uiState.update { it.copy(secondsLeft = 0) }
            endGame("Time's up!")
        }
    }
}

/** Input/lock state + what the UI should play next. */
enum class GamePhase { Idle, Resolving, Rejecting, GameOver }

/** Game mode chosen on the M5 menu screen. */
enum class GameMode { CLASSIC, ZEN }

data class GameUiState(
    val board: Board,
    val phase: GamePhase,
    val pendingPlayback: Playback? = null,
    val rejectedSwap: SwapIntent? = null,
    val score: Int = 0,
    val mode: GameMode = GameMode.CLASSIC,
    val secondsLeft: Int? = null,
    val gameOverReason: String? = null,
)

/** A batch of engine steps handed to the UI for playback. */
data class Playback(
    val label: String,
    val steps: List<Step>,
    val measureFrames: Boolean = false,
)