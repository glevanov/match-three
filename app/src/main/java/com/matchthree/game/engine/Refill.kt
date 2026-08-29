package com.matchthree.game.engine

import com.matchthree.game.model.Board
import com.matchthree.game.model.Gem
import com.matchthree.game.model.GemType
import com.matchthree.game.model.Position


/**
 * After gravity, all empty cells sit in the top rows (one contiguous block per
 * column). Refill gives each a brand-new gem with a fresh [id][Gem.id].
 *
 * Spawn colors can form new matches — the cascade loop detects and clears them,
 * which is the intended behaviour inside a resolution (MECHANICS.md: invariants
 * are checked only after a cascade settles).
 */
object Refill {

    data class Result(val board: Board, val spawned: List<Step.Spawn.Placement>)

    fun fill(
        board: Board,
        gemTypeCount: Int,
        nextId: () -> Int,
        gemType: (Int) -> GemType,
    ): Result {
        val cells = Array(board.height) { row -> board.cells[row].copyOf() }
        val spawned = mutableListOf<Step.Spawn.Placement>()

        for (col in 0 until board.width) {
            var row = 0
            while (row < board.height && cells[row][col] == null) {
                val gem = Gem(nextId(), gemType(gemTypeCount))
                cells[row][col] = gem
                spawned += Step.Spawn.Placement(gem, Position(row, col))
                row++
            }
        }
        return Result(Board.of(board.width, board.height, cells), spawned)
    }
}