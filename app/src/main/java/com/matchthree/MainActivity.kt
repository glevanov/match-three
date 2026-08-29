package com.matchthree

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.platform.LocalContext
import androidx.lifecycle.ViewModelStore
import androidx.lifecycle.ViewModelStoreOwner
import androidx.lifecycle.viewmodel.compose.LocalViewModelStoreOwner
import com.matchthree.data.HighScoreStore
import com.matchthree.data.highScoreStore
import com.matchthree.ui.GameMode
import com.matchthree.ui.screens.GameScreen
import com.matchthree.ui.screens.MenuScreen
import com.matchthree.ui.theme.MatchThreeTheme

/**
 * M5 app shell: menu -> game routing. Each game gets its own scoped
 * [ViewModelStore] so the game ViewModel (and its classic timer) is torn down
 * when the player returns to the menu; a new game always starts fresh.
 */
class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            MatchThreeTheme {
                val context = LocalContext.current
                val store = remember(context) { highScoreStore(context) }
                var route by remember { mutableStateOf<Route>(Route.Menu) }

                when (val r = route) {
                    Route.Menu -> MenuScreen(
                        store = store,
                        onSelectMode = { mode -> route = Route.Game(mode) },
                    )
                    is Route.Game -> GameHost(
                        mode = r.mode,
                        store = store,
                        onExitToMenu = { route = Route.Menu },
                    )
                }
            }
        }
    }
}

/** Simple state-based navigation between the menu and one game. */
private sealed interface Route {
    data object Menu : Route
    data class Game(val mode: GameMode) : Route
}

/**
 * Wraps a game in an ephemeral [ViewModelStoreOwner], so `viewModel()` inside
 * [GameScreen] is scoped to this composition instead of the Activity.
 * [GameHost] leaving the composition clears the store (timer cancelled; no
 * stale games in the Activity's store).
 */
@Composable
private fun GameHost(
    mode: GameMode,
    store: HighScoreStore,
    onExitToMenu: () -> Unit,
) {
    val owner = remember {
        object : ViewModelStoreOwner {
            override val viewModelStore = ViewModelStore()
        }
    }
    DisposableEffect(owner) {
        onDispose { owner.viewModelStore.clear() }
    }
    CompositionLocalProvider(LocalViewModelStoreOwner provides owner) {
        GameScreen(
            mode = mode,
            store = store,
            onExitToMenu = onExitToMenu,
        )
    }
}