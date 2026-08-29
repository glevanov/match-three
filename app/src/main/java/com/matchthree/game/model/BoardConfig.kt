package com.matchthree.game.model

/**
 * Board size and gem-color count. Tuned constants per MECHANICS.md;
 * the generator and engine derive everything from these values.
 */
data class BoardConfig(
    val width: Int = DEFAULT_WIDTH,
    val height: Int = DEFAULT_HEIGHT,
    val gemTypeCount: Int = DEFAULT_GEM_TYPES,
) {
    companion object {
        const val DEFAULT_WIDTH = 9
        const val DEFAULT_HEIGHT = 9
        const val DEFAULT_GEM_TYPES = 6
    }
}