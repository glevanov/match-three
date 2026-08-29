package com.matchthree.game.engine

import com.matchthree.game.model.Board
import com.matchthree.game.model.Position

/**
 * Applies gravity after a clear: every surviving gem in a column drops to the
 * lowest free row, preserving its column order and its stable [id][com.matchthree.game.model.Gem.id].
 */
object Gravity {

    data class Result(val board: Board, val falls: List<Step.Fall.FallMove>)

    fun apply(board: Board, destroyed: Set<Position>): Result {
        val cells = Array(board.height) { row -> board.cells[row].copyOf() }
        val falls = mutableListOf<Step.Fall.FallMove>()

        for (col in 0 until board.width) {
            var targetRow = board.height - 1
            for (row in board.height - 1 downTo 0) {
                if (Position(row, col) in destroyed) {
                    cells[row][col] = null
                    continue
                }
                val gem = cells[row][col] ?: continue
                if (row != targetRow) {
                    cells[targetRow][col] = gem
                    cells[row][col] = null
                    falls += Step.Fall.FallMove(gem.id, Position(row, col), Position(targetRow, col))
                }
                targetRow--
            }
        }
        return Result(Board.of(board.width, board.height, cells), falls)
    }
}