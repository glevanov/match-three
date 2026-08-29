package com.matchthree.game.model

/**
 * A gem on the board.
 *
 * @property id Stable identity across falls, spawns, and cascades so the UI can
 *   track gems while animating. Never reused within a game session.
 * @property type Color/basic type. Special types arrive in Milestone 4.
 */
data class Gem(val id: Int, val type: GemType)