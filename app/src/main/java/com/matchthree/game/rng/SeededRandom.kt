package com.matchthree.game.rng

import kotlin.random.Random

/**
 * Deterministic pseudo-randomness for the whole engine (AGENTS.md: seeded RNG).
 * Same seed reproduces the exact same game, which keeps tests and replays stable.
 */
class SeededRandom(seed: Long) {
    private val random = Random(seed)

    /** Random integer in `[0, bound)`. */
    fun nextInt(bound: Int): Int = random.nextInt(bound)

    /** Random integer in `[min, max]` inclusive. */
    fun nextInt(min: Int, max: Int): Int = random.nextInt(min, max + 1)

    fun nextLong(): Long = random.nextLong()
}