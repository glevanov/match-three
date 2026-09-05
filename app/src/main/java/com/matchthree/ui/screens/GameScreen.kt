package com.matchthree.ui.screens

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.activity.compose.BackHandler
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.runtime.snapshotFlow
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import com.matchthree.BuildConfig
import com.matchthree.data.HighScoreStore
import com.matchthree.data.HighScores
import com.matchthree.game.engine.Step
import com.matchthree.game.model.Board
import com.matchthree.game.model.BoardConfig
import com.matchthree.game.model.Position
import com.matchthree.ui.BoardOp
import com.matchthree.ui.GameMode
import com.matchthree.ui.GameViewModel
import com.matchthree.ui.game.BoardCanvas
import com.matchthree.ui.game.FrameStats
import com.matchthree.ui.game.FrameTimeTracker
import com.matchthree.ui.game.StepPlayer
import kotlinx.coroutines.flow.first
import java.util.Locale

/**
 * M5 game screen: the board with HUD (score + Classic timer), started in the
 * mode chosen on the menu screen. Game-over saves the high score and offers
 * Play again / Back to menu. The debug frame-stress control from M2 remains.
 */
@Composable
fun GameScreen(
    mode: GameMode,
    store: HighScoreStore,
    onExitToMenu: () -> Unit,
    viewModel: GameViewModel = viewModel(factory = GameViewModel.factory(mode)),
) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    val config = remember { BoardConfig() }
    var selected by remember { mutableStateOf<Position?>(null) }
    var frameReport by remember { mutableStateOf<FrameStats?>(null) }
    var savedNewHigh by remember { mutableStateOf(false) }
    var exitConfirmVisible by remember { mutableStateOf(false) }

    val player = remember(config) {
        StepPlayer(
            config = config,
            onSettled = { settled -> viewModel.onStepsPlayed(settled) },
            onScore = { delta -> viewModel.addScore(delta) },
        )
    }

    // Drain the ViewModel's op queue from ONE coroutine — the only writer of
    // StepPlayer animation state. StepPlayer's actors are per-gem Animatables
    // (MutatorMutex): two coroutines touching the same actor cancel each
    // other's animation, and a cancelled playback never reaches its settle
    // callback, which used to leave the ViewModel stuck in Resolving/Rejecting
    // — the fast-swap deadlock. Strict sequencing rules that race out.
    //
    // An op is removed from the queue only after it fully played, so a
    // coroutine cancelled mid-op (game over, leaving the screen) re-runs that
    // op on restart instead of skipping it.
    LaunchedEffect(Unit) {
        var settledBoard: Board? = null
        while (true) {
            val op = state.pendingOps.firstOrNull()
            if (op == null) {
                // Queue drained: reconcile the actor pool with the last board
                // this consumer settled, or the published board on cold start.
                // Never snap to a board newer than the last Play we consumed —
                // its ops haven't animated yet.
                player.applyBoard(settledBoard ?: state.board)
                snapshotFlow { state.pendingOps }.first { it.isNotEmpty() }
                continue
            }
            when (op) {
                is BoardOp.Play -> {
                    frameReport = if (op.measureFrames) {
                        FrameTimeTracker.measure(op.label) { player.play(op.steps) }
                    } else {
                        player.play(op.steps)
                        null
                    }
                    settledBoard = op.steps.filterIsInstance<Step.Settled>().lastOrNull()?.board
                        ?: settledBoard
                }
                is BoardOp.Reject -> player.playRejection(op.intent.a, op.intent.b)
                is BoardOp.Resync -> {
                    player.applyBoard(op.board)
                    settledBoard = op.board
                }
            }
            viewModel.onOpConsumed(op)
        }
    }

    // Persist a new high score exactly once when a round ends.
    LaunchedEffect(state.gameOverReason) {
        val reason = state.gameOverReason ?: return@LaunchedEffect
        savedNewHigh = store.saveIfBeats(state.mode, state.score)
    }

    val scores by store.scores.collectAsStateWithLifecycle(initialValue = HighScores())

    Surface(
        modifier = Modifier.fillMaxSize(),
        color = MaterialTheme.colorScheme.background,
    ) {
        val gameOverReason = state.gameOverReason
        if (gameOverReason != null) {
            GameOverScreen(
                reason = gameOverReason,
                score = state.score,
                highScore = maxOf(scores.forMode(mode), state.score),
                isNewHighScore = savedNewHigh,
                onRestart = { viewModel.restart() },
                onExitToMenu = onExitToMenu,
            )
        } else {
            // System back exits through the same confirmation as the button.
            BackHandler { exitConfirmVisible = true }
            if (exitConfirmVisible) {
                AlertDialog(
                    onDismissRequest = { exitConfirmVisible = false },
                    title = { Text("Exit game?") },
                    text = { Text("The current round will be lost.") },
                    confirmButton = {
                        TextButton(
                            onClick = {
                                exitConfirmVisible = false
                                onExitToMenu()
                            },
                        ) { Text("Exit") }
                    },
                    dismissButton = {
                        TextButton(onClick = { exitConfirmVisible = false }) {
                            Text("Keep playing")
                        }
                    },
                )
            }
            Column(
                modifier = Modifier.fillMaxSize().padding(8.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.Center,
            ) {
                Row(
                    modifier = Modifier.padding(bottom = 4.dp),
                    horizontalArrangement = Arrangement.spacedBy(16.dp),
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    TextButton(onClick = { exitConfirmVisible = true }) {
                        Text("Menu")
                    }
                    Text(
                        text = "Score: ${state.score}",
                        style = MaterialTheme.typography.titleLarge,
                    )
                    state.secondsLeft?.let { left ->
                        Text(
                            text = "Time: $left",
                            style = MaterialTheme.typography.titleLarge,
                        )
                    }
                    Text(
                        text = if (mode == GameMode.CLASSIC) "Classic" else "Zen",
                        style = MaterialTheme.typography.titleMedium,
                        color = MaterialTheme.colorScheme.onBackground.copy(alpha = 0.6f),
                    )
                }
                Box(
                    modifier = Modifier
                        .fillMaxWidth(0.95f)
                        .aspectRatio(1f),
                ) {
                    BoardCanvas(
                        player = player,
                        config = config,
                        selected = selected,
                        modifier = Modifier.fillMaxSize(),
                        onSelect = { selected = it },
                        onSwapIntent = { viewModel.submitSwap(it) },
                    )
                }
                if (BuildConfig.DEBUG) {
                    TextButton(onClick = { viewModel.debugFullClear() }) {
                        Text("debug: worst-case clear (measures frames)")
                    }
                    frameReport?.let { report ->
                        Text(
                            text = String.format(
                                Locale.US,
                                "frames=%d avg=%.1fms p95=%.1fms p99=%.1fms budget=%.1fms withinBudget=%b",
                                report.sampleCount,
                                report.avgMillis,
                                report.p95Millis,
                                report.p99Millis,
                                FrameStats.FRAME_BUDGET_MILLIS,
                                report.withinBudget,
                            ),
                            fontSize = 10.sp,
                            fontFamily = FontFamily.Monospace,
                            color = MaterialTheme.colorScheme.primary,
                        )
                    }
                }
            }
        }
    }
}