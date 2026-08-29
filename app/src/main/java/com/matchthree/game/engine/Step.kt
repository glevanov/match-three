package com.matchthree.game.engine

import com.matchthree.game.model.Board
import com.matchthree.game.model.Gem
import com.matchthree.game.model.Position

/**
 * Ordered events the engine emits while resolving a player swap. The ViewModel
 * plays these back in order to drive animation. AGENTS.md: "Steps, not state."
 *
 * Milestone 1 emits Swap / Destroy / Fall / Spawn / Settled. Later milestones
 * extend this sealed type with more variants (Match, ComboActivate, Score).
 */
sealed interface Step {

    /** Two adjacent gems trade places at the start of a (legal) turn. */
    data class Swap(val a: Position, val b: Position) : Step

    /** The gems at these positions are cleared (a run now; blasts/combos later). */
    data class Destroy(val positions: Set<Position>) : Step

    /** Points awarded for one cascade round; depth 1 is the initiating match. */
    data class Score(val delta: Int, val cascadeDepth: Int) : Step

    /** Gems dropping straight down after a clear; one entry per moved gem. */
    data class Fall(val moves: List<FallMove>) : Step {
        data class FallMove(val gemId: Int, val from: Position, val to: Position)
    }

    /** Brand-new gems appearing in the top rows after a fall. */
    data class Spawn(val gems: List<Placement>) : Step {
        data class Placement(val gem: Gem, val position: Position)
    }

    /** Resolution completed; the board is stable (no matches remain). */
    data class Settled(val board: Board) : Step
}