package com.matchthree.game.engine

/**
 * Hands out strictly increasing gem ids for the current session so that no two
 * gems ever share an id (AGENTS.md: stable ids). Created once per [GameEngine].
 */
class IdSource(private var next: Int = 0) {
    fun next(): Int = next++
}