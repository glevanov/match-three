package com.matchthree.ui.screens

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
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
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import com.matchthree.data.HighScoreStore
import com.matchthree.data.HighScores
import com.matchthree.game.model.BoardConfig
import com.matchthree.game.model.Position
import com.matchthree.ui.GameMode
import com.matchthree.ui.GameViewModel
import com.matchthree.ui.game.BoardCanvas
import com.matchthree.ui.game.FrameStats
import com.matchthree.ui.game.FrameTimeTracker
import com.matchthree.ui.game.StepPlayer
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

    val player = remember(config) {
        StepPlayer(
            config = config,
            onSettled = { settled -> viewModel.onStepsPlayed(settled) },
            onScore = { delta -> viewModel.addScore(delta) },
        )
    }

    // Reconcile the actor pool whenever a settlement lands (initial + each move).
    LaunchedEffect(state.board) {
        player.applyBoard(state.board)
    }

    // Play back engine steps; the debug playbacks also measure frame timing.
    val playback = state.pendingPlayback
    LaunchedEffect(playback) {
        if (playback != null) {
            frameReport = if (playback.measureFrames) {
                FrameTimeTracker.measure(playback.label) { player.play(playback.steps) }
            } else {
                player.play(playback.steps)
                null
            }
        }
    }

    // Invalid swaps shuffle across and back, then unlock input.
    LaunchedEffect(state.rejectedSwap) {
        val rejected = state.rejectedSwap ?: return@LaunchedEffect
        player.playRejection(rejected.a, rejected.b)
        viewModel.onRejectionPlayed()
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