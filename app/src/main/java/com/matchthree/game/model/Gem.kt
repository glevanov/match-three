package com.matchthree.game.model

/**
 * A gem on the board.
 *
 * @property id Stable identity across falls, spawns, and cascades so the UI can
 *   track gems while animating. Never reused within a game session.
 * @property type Color/basic type.
 * @property special Special kind when the gem is a transformed special
 *   (FLAME/STAR/HYPERCUBE), or null for a plain gem.
 */
data class Gem(val id: Int, val type: GemType, val special: Special? = null)