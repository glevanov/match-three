package com.matchthree.game

import com.matchthree.game.model.Board
import com.matchthree.game.model.Gem
import com.matchthree.game.model.GemType
import com.matchthree.game.model.Position

/**
 * Builds a [Board] from rows of single-character codes for readable test fixtures:
 * R=RED, G=GREEN, B=BLUE, Y=YELLOW, A/other=PURPLE or ORANGE via [charToType],
 * '.' = empty cell. Gem ids are assigned row-major starting at [idOffset].
 */
object Boards {

    fun fromRows(vararg rows: String): Board = fromRows(rows.toList())

    fun fromRows(rows: List<String>, idOffset: Int = 0): Board {
        require(rows.isNotEmpty()) { "at least one row" }
        val width = rows[0].length
        require(rows.all { it.length == width }) { "all rows must have equal width" }
        var nextId = idOffset
        return Board.create(width, rows.size) { pos: Position ->
            charToType(rows[pos.row][pos.col])?.let { Gem(nextId++, it) }
        }
    }

    fun charToType(char: Char): GemType? = when (char) {
        'R' -> GemType.RED
        'G' -> GemType.GREEN
        'B' -> GemType.BLUE
        'Y' -> GemType.YELLOW
        'P', 'A' -> GemType.PURPLE
        'O' -> GemType.ORANGE
        '.' -> null
        else -> error("unknown gem char: $char")
    }

    /** Reverse of [charToType]; PURPLE renders as 'P'. */
    fun typeToChar(type: GemType): Char = when (type) {
        GemType.RED -> 'R'
        GemType.GREEN -> 'G'
        GemType.BLUE -> 'B'
        GemType.YELLOW -> 'Y'
        GemType.PURPLE -> 'P'
        GemType.ORANGE -> 'O'
    }
}