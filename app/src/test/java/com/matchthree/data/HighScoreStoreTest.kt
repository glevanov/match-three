package com.matchthree.data

import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import com.matchthree.ui.GameMode
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.File

/** JVM tests for the M5 high-score persistence logic. */
class HighScoreStoreTest {

    private fun tempStore(): Pair<HighScoreStore, CoroutineScope> {
        val file = File.createTempFile("high-scores-test", ".preferences_pb")
        file.deleteOnExit()
        val scope = CoroutineScope(Dispatchers.IO + Job())
        val store = HighScoreStore(
            PreferenceDataStoreFactory.create(scope = scope) { file },
        )
        return store to scope
    }

    @Test
    fun saveIfBeatsPersistsNewHighScore() = runBlocking {
        val (store, scope) = tempStore()
        try {
            assertTrue(store.saveIfBeats(GameMode.CLASSIC, 500))
            assertEquals(500, store.scores.first().classic)
        } finally {
            scope.cancel()
        }
    }

    @Test
    fun saveIfBeatsKeepsHighestScoreOnly() = runBlocking {
        val (store, scope) = tempStore()
        try {
            store.saveIfBeats(GameMode.CLASSIC, 500)
            assertFalse(store.saveIfBeats(GameMode.CLASSIC, 300))
            assertEquals(500, store.scores.first().classic)
            assertTrue(store.saveIfBeats(GameMode.CLASSIC, 900))
            assertEquals(900, store.scores.first().classic)
        } finally {
            scope.cancel()
        }
    }

    @Test
    fun classicAndZenScoresAreIndependent() = runBlocking {
        val (store, scope) = tempStore()
        try {
            store.saveIfBeats(GameMode.CLASSIC, 500)
            store.saveIfBeats(GameMode.ZEN, 900)
            val scores = store.scores.first()
            assertEquals(500, scores.classic)
            assertEquals(900, scores.zen)
        } finally {
            scope.cancel()
        }
    }

    @Test
    fun nonPositiveScoresAreNeverSaved() = runBlocking {
        val (store, scope) = tempStore()
        try {
            assertFalse(store.saveIfBeats(GameMode.ZEN, 0))
            assertFalse(store.saveIfBeats(GameMode.ZEN, -10))
            assertEquals(0, store.scores.first().zen)
        } finally {
            scope.cancel()
        }
    }

    @Test
    fun forModeMapsToTheRightPerModeField() {
        val scores = HighScores(classic = 100, zen = 200)
        assertEquals(100, scores.forMode(GameMode.CLASSIC))
        assertEquals(200, scores.forMode(GameMode.ZEN))
    }
}