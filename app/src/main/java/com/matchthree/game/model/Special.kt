package com.matchthree.game.model

/**
 * Special gem types (MECHANICS.md, M4).
 *
 * - [FLAME]: born from a 4-in-row; explodes a 3x3 area around itself when
 *   cleared or activated.
 * - [STAR]: born from a T/L shape; clears its full row and column.
 * - [HYPERCUBE]: born from a 5-in-row; **colorless** — it never participates in
 *   a match (see MatchDetector) and its effect depends on the gem it is swapped
 *   with (clears every gem of that swapped color).
 */
enum class Special {
    FLAME,
    STAR,
    HYPERCUBE,
}