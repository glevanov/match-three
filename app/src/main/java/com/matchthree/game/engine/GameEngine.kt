package com.matchthree.game.engine

import com.matchthree.game.model.Board
import com.matchthree.game.model.BoardConfig
import com.matchthree.game.model.GemType
import com.matchthree.game.model.Position
import com.matchthree.game.rng.SeededRandom

/**
 * The pure-Kotlin game engine (AGENTS.md: engine is pure Kotlin, no Android
 * imports under game/). Consumes a board + swap, and emits an ordered list of
 * [Step]s which the UI plays back for animation.
 */
class GameEngine(
    private val config: BoardConfig = BoardConfig(),
    private val rng: SeededRandom,
) {
    private val idSource = IdSource()

    /** A fresh board satisfying generation invariants (no match, >=1 legal move). */
    fun newGame(): Board = BoardGenerator(
        width = config.width,
        height = config.height,
        gemTypeCount = config.gemTypeCount,
        rng = rng,
        idSource = idSource,
    ).newBoard()

    /** True if swapping these adjacent cells would create at least one match. */
    fun isLegalSwap(board: Board, a: Position, b: Position): Boolean {
        if (!board.isInside(a) || !board.isInside(b)) return false
        if (!a.isOrthogonallyAdjacentTo(b)) return false
        if (board.gemAt(a) == null || board.gemAt(b) == null) return false
        return MatchDetector.findMatches(board.withSwapped(a, b)).isNotEmpty()
    }

    /**
     * Resolves a legal swap into the full cascade of steps, ending with a stable
     * board that contains no matches. Returns null when the swap is illegal —
     * the caller decides how to animate the rejection.
     */
    fun resolveSwap(board: Board, a: Position, b: Position): Resolution? {
        if (!isLegalSwap(board, a, b)) return null

        val steps = mutableListOf<Step>()
        steps += Step.Swap(a, b)

        var current = board.withSwapped(a, b)
        var rounds = 0
        while (true) {
            val matches = MatchDetector.findMatches(current)
            if (matches.isEmpty()) break
            val destroyed = matches.flatMap { it.positions }.toSet()
            steps += Step.Destroy(destroyed)

            val gravity = Gravity.apply(current, destroyed)
            if (gravity.falls.isNotEmpty()) steps += Step.Fall(gravity.falls)

            val refill = Refill.fill(
                board = gravity.board,
                gemTypeCount = config.gemTypeCount,
                nextId = idSource::next,
                gemType = { count -> GemType.fromIndex(rng.nextInt(count)) },
            )
            if (refill.spawned.isNotEmpty()) steps += Step.Spawn(refill.spawned)

            current = refill.board
            rounds++
            if (rounds > MAX_CASCADE_ROUNDS) {
                error("cascade did not settle after $MAX_CASCADE_ROUNDS rounds")
            }
        }

        steps += Step.Settled(current)
        return Resolution(board = current, steps = steps.toList())
    }

    private companion object {
        /** Safety valve against an unlucky refill streak looping forever. */
        const val MAX_CASCADE_ROUNDS = 1_000
    }
}

/** Outcome of [GameEngine.resolveSwap]: the settled board plus its playback steps. */
data class Resolution(val board: Board, val steps: List<Step>)