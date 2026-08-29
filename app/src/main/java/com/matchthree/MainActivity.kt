package com.matchthree

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import com.matchthree.ui.screens.GameScreen
import com.matchthree.ui.theme.MatchThreeTheme

/** M2: Compose shell hosting the game screen. */
class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            MatchThreeTheme {
                GameScreen()
            }
        }
    }
}