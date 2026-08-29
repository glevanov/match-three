package com.matchthree.ui.screens

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp

/**
 * M5 game-over screen: final score, mode high score (with a "new high score"
 * note when the round beat it), Play again, and a Back-to-menu button.
 */
@Composable
fun GameOverScreen(
    reason: String,
    score: Int,
    highScore: Int,
    isNewHighScore: Boolean,
    onRestart: () -> Unit,
    onExitToMenu: () -> Unit,
) {
    Column(
        modifier = Modifier.fillMaxSize().padding(32.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Text(
            text = "Game Over",
            style = MaterialTheme.typography.headlineLarge,
        )
        Spacer(modifier = Modifier.height(12.dp))
        Text(
            text = reason,
            style = MaterialTheme.typography.titleMedium,
        )
        Spacer(modifier = Modifier.height(12.dp))
        Text(
            text = "Score: $score",
            style = MaterialTheme.typography.titleLarge,
        )
        if (isNewHighScore) {
            Spacer(modifier = Modifier.height(8.dp))
            Text(
                text = "New high score!",
                style = MaterialTheme.typography.titleMedium,
                color = MaterialTheme.colorScheme.primary,
            )
        }
        Spacer(modifier = Modifier.height(8.dp))
        Text(
            text = "Best: $highScore",
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onBackground.copy(alpha = 0.7f),
        )
        Spacer(modifier = Modifier.height(32.dp))
        Button(onClick = onRestart) {
            Text("Play again")
        }
        Spacer(modifier = Modifier.height(8.dp))
        TextButton(onClick = onExitToMenu) {
            Text("Menu")
        }
    }
}