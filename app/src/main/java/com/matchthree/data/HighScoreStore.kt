package com.matchthree.data

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.intPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import com.matchthree.ui.GameMode
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

private val Context.highScoreDataStore by preferencesDataStore(name = "match_three_scores")

/** App wiring: a process-wide DataStore-backed store. */
fun highScoreStore(context: Context): HighScoreStore = HighScoreStore(context.highScoreDataStore)

/** High scores per mode as persisted by [HighScoreStore]. */
data class HighScores(
    val classic: Int = 0,
    val zen: Int = 0,
) {
    fun forMode(mode: GameMode): Int = when (mode) {
        GameMode.CLASSIC -> classic
        GameMode.ZEN -> zen
    }
}

/**
 * Persistent high scores via DataStore Preferences (M5). Only the max score
 * per mode is saved (MECHANICS.md non-goal: no mid-session persistence).
 *
 * Takes a [DataStore] directly so the save logic is JVM-testable (the
 * Context-backed singleton lives in [highScoreStore]).
 */
class HighScoreStore(private val dataStore: DataStore<Preferences>) {

    val scores: Flow<HighScores> = dataStore.data.map { prefs ->
        HighScores(
            classic = prefs[Keys.CLASSIC] ?: 0,
            zen = prefs[Keys.ZEN] ?: 0,
        )
    }

    /**
     * Persists [score] for [mode] when it beats the stored high score.
     * Returns true when a new high score was recorded.
     */
    suspend fun saveIfBeats(mode: GameMode, score: Int): Boolean {
        if (score <= 0) return false
        var saved = false
        dataStore.edit { prefs ->
            val key = when (mode) {
                GameMode.CLASSIC -> Keys.CLASSIC
                GameMode.ZEN -> Keys.ZEN
            }
            val current = prefs[key] ?: 0
            if (score > current) {
                prefs[key] = score
                saved = true
            }
        }
        return saved
    }

    private object Keys {
        val CLASSIC = intPreferencesKey("high_score_classic")
        val ZEN = intPreferencesKey("high_score_zen")
    }
}