package com.matchthree.ui.screens

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.matchthree.data.HighScoreStore
import com.matchthree.data.HighScores
import com.matchthree.ui.GameMode

/**
 * M5 menu: pick Classic (75 s timer) or Zen (endless), with the persisted
 * high score shown per mode. Selecting a mode starts a new game.
 */
@Composable
fun MenuScreen(
    store: HighScoreStore,
    onSelectMode: (GameMode) -> Unit,
) {
    val scores by store.scores.collectAsStateWithLifecycle(initialValue = HighScores())

    // Dark surface matching the game screen; without it the window background
    // leaks through and light-scheme text is unreadable in light mode.
    Surface(
        modifier = Modifier.fillMaxSize(),
        color = MaterialTheme.colorScheme.background,
    ) {
    Column(
        modifier = Modifier.fillMaxSize().padding(32.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Text(
            text = "Match Three",
            style = MaterialTheme.typography.displaySmall,
        )
        Spacer(modifier = Modifier.height(8.dp))
        Text(
            text = "Line up 3+ gems to clear them",
            style = MaterialTheme.typography.bodyMedium,
        )
        Spacer(modifier = Modifier.height(48.dp))

        ModeButton(
            label = "Classic",
            detail = "75 seconds",
            highScore = scores.classic,
            onClick = { onSelectMode(GameMode.CLASSIC) },
        )
        Spacer(modifier = Modifier.height(16.dp))
        ModeButton(
            label = "Zen",
            detail = "Endless",
            highScore = scores.zen,
            onClick = { onSelectMode(GameMode.ZEN) },
        )
    }
    }
}

@Composable
private fun ModeButton(
    label: String,
    detail: String,
    highScore: Int,
    onClick: () -> Unit,
) {
    Column(horizontalAlignment = Alignment.CenterHorizontally) {
        Button(onClick = onClick) {
            Text("$label — $detail")
        }
        Text(
            text = "High score: $highScore",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onBackground.copy(alpha = 0.7f),
        )
    }
}