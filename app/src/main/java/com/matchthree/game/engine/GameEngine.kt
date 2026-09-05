package com.matchthree.game.engine

import com.matchthree.game.model.Board
import com.matchthree.game.model.BoardConfig
import com.matchthree.game.model.Gem
import com.matchthree.game.model.GemType
import com.matchthree.game.model.Position
import com.matchthree.game.model.Special
import com.matchthree.game.rng.SeededRandom

/**
 * The pure-Kotlin game engine (AGENTS.md: engine is pure Kotlin, no Android
 * imports under game/). Consumes a board + swap, and emits an ordered list of
 * [Step]s which the UI plays back for animation.
 *
 * M4: the resolution loop also handles special gems — births (one gem per
 * shape group transforms, precedence 5 > T/L > 4 > 3), cascade-swept
 * detonation, player swap combos, and Hypercube+Hypercube board regeneration.
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

    /**
     * True if swapping these adjacent cells would create a match, OR the swap
     * puts two specials into contact, OR a Hypercube contacts any gem
     * (MECHANICS.md: hypercube trigger and the combo table).
     */
    fun isLegalSwap(board: Board, a: Position, b: Position): Boolean {
        if (!board.isInside(a) || !board.isInside(b)) return false
        if (!a.isOrthogonallyAdjacentTo(b)) return false
        if (board.gemAt(a) == null || board.gemAt(b) == null) return false
        val swapped = board.withSwapped(a, b)
        if (MatchDetector.findMatches(swapped).isNotEmpty()) return true
        return SpecialRules.swapContactLegal(swapped.gemAt(a), swapped.gemAt(b))
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
        var cascadeDepth = 0
        var rounds = 0

        // 1) Player-swap special activation: combos and hypercube triggers fire
        //    before any match detection (depth-1 round).
        val swapActivation = swapActivation(current, a, b)
        if (swapActivation != null) {
            cascadeDepth = 1
            steps += Step.ComboActivate(
                specialA = swapActivation.specialA,
                specialB = swapActivation.specialB,
                affectedCells = swapActivation.affected,
            )
            steps += Step.Destroy(swapActivation.affected)
            steps += Step.Score(
                Scorer.roundScore(swapActivation.affected, cascadeDepth),
                cascadeDepth,
            )

            // Hypercube+Hypercube: full-board clear is followed by an immediate
            // regeneration (invariant-checked by the generator), not a refill.
            if (swapActivation.regenerate) {
                val fresh = newGame()
                steps += Step.Settled(fresh)
                return Resolution(board = fresh, steps = steps.toList())
            }

            val gravity = Gravity.apply(current, swapActivation.affected)
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
        }

        // 2) Standard cascade loop: match -> (births, swept detonations) ->
        //    gravity -> refill -> re-check, until the board is stable.
        while (true) {
            val matches = MatchDetector.findMatches(current)
            if (matches.isEmpty()) break
            cascadeDepth++

            val matched = matches.flatMap { it.positions }.toSet()
            val extra = SpecialRules.sweptBlastCells(current, matched)
            val destroyed = matched + extra

            val births = SpecialRules.resolveBirths(current, matches)
            val destroyedEx = destroyed - births.map { it.cell }.toSet()

            steps += Step.Destroy(destroyedEx)
            steps += Step.Score(Scorer.roundScore(destroyedEx, cascadeDepth), cascadeDepth)

            val gravity = Gravity.apply(current, destroyedEx)
            if (gravity.falls.isNotEmpty()) steps += Step.Fall(gravity.falls)

            // Birth gems are surviving gems: apply their transformations on the
            // fallen board (same ids, same colors, new special kinds). These
            // states are impossible by construction (birth cells are excluded
            // from destruction), so invariant violations abort loudly.
            var afterBirth = gravity.board
            for (birth in births) {
                val finalPos = checkNotNull(afterBirth.positionOf(birth.gemId)) {
                    "birth gem ${birth.gemId} vanished after gravity"
                }
                val gem = checkNotNull(afterBirth.gemAt(finalPos)) {
                    "birth cell $finalPos is empty on the fallen board"
                }
                afterBirth = afterBirth.withGem(
                    finalPos,
                    Gem(gem.id, gem.type, birth.special),
                )
                steps += Step.SpecialBirth(finalPos, gem.id, birth.special)
            }

            val refill = Refill.fill(
                board = afterBirth,
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

    /**
     * Fisher-Yates reshuffle of the existing gem multiset (MECHANICS.md):
     * re-validates that the result has no pre-existing match and at least one
     * legal move. Retries up to [MAX_RESHUFFLE_ATTEMPTS]; returns null when the
     * board stays dead, which triggers game over in Zen mode.
     */
    fun reshuffle(board: Board): Board? {
        repeat(MAX_RESHUFFLE_ATTEMPTS) {
            val candidate = shuffleMultiset(board)
            if (MatchDetector.findMatches(candidate).isEmpty() && LegalMoveDetector.hasLegalMove(candidate)) {
                return candidate
            }
        }
        return null
    }

    /** Shuffles the gems into a fresh layout, preserving ids and types. */
    private fun shuffleMultiset(board: Board): Board {
        val gems = board.positions().mapNotNull { board.gemAt(it) }.toMutableList()
        for (i in gems.size - 1 downTo 1) {
            val j = rng.nextInt(i + 1)
            val tmp = gems[i]
            gems[i] = gems[j]
            gems[j] = tmp
        }
        var next = 0
        return Board.create(board.width, board.height) { position ->
            if (next < gems.size) gems[next++] else null
        }
    }

    /**
     * Computes what (if anything) a player swap of two gems activates: a combo
     * between two specials, or a single hypercube trigger against a normal gem.
     * Returns null when neither gem is a special (plain match path).
     */
    private fun swapActivation(
        board: Board,
        a: Position,
        b: Position,
    ): SwapActivation? {
        val gemA = board.gemAt(a)
        val gemB = board.gemAt(b)
        val specA = gemA?.special
        val specB = gemB?.special
        if (specA == null && specB == null) return null

        // Hypercube against a plain gem: clears every gem of the swapped color
        // plus the hypercube itself (single-activator, no combo partner).
        if (specA == Special.HYPERCUBE && specB == null || specB == Special.HYPERCUBE && specA == null) {
            val hyperPos = if (specA == Special.HYPERCUBE) a else b
            val otherPos = if (hyperPos == a) b else a
            val partner = board.gemAt(otherPos)
            if (partner == null || partner.special != null) return null
            return SwapActivation(
                specialA = Special.HYPERCUBE,
                specialB = null,
                affected = SpecialRules.hypercubeTriggerCells(board, hyperPos, otherPos, null),
                regenerate = false,
            )
        }

        // Combo table: the two swapped gems are both specials.
        if (specA != null && specB != null) {
            val affected = SpecialRules.comboAffectedCells(board, a, b, specA, specB)
            val regenerate = specA == Special.HYPERCUBE && specB == Special.HYPERCUBE
            return SwapActivation(specA, specB, affected, regenerate)
        }
        return null
    }

    /** Result of a player-swap special activation. */
    private data class SwapActivation(
        val specialA: Special,
        val specialB: Special?,
        val affected: Set<Position>,
        val regenerate: Boolean,
    )

    private companion object {
        /** Safety valve against an unlucky refill streak looping forever. */
        const val MAX_CASCADE_ROUNDS = 1_000

        /** MECHANICS.md: reshuffle retries before handing the board to game over. */
        const val MAX_RESHUFFLE_ATTEMPTS = 20
    }
}

/** Outcome of [GameEngine.resolveSwap]: the settled board plus its playback steps. */
data class Resolution(val board: Board, val steps: List<Step>)