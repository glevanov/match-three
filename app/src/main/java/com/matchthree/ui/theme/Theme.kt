package com.matchthree.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

/** Minimal M2 theme; visual identity comes later (M5 polish). */
@Composable
fun MatchThreeTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = darkColorScheme(
            primary = Color(0xFF80CBC4),
            background = Color(0xFF1C2229),
            surface = Color(0xFF263238),
        ),
        content = content,
    )
}