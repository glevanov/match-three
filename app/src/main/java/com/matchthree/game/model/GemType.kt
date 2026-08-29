package com.matchthree.game.model

/**
 * The six gem colors. Count must match [BoardConfig.gemTypeCount].
 * Specials (Flame/Star/Hypercube) are introduced in Milestone 4.
 */
enum class GemType {
    RED,
    GREEN,
    BLUE,
    YELLOW,
    PURPLE,
    ORANGE;

    companion object {
        fun fromIndex(index: Int): GemType = entries[index]
    }
}