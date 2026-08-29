package com.matchthree.game.engine

import com.matchthree.game.model.Board
import com.matchthree.game.model.Gem
import com.matchthree.game.model.Position
import com.matchthree.game.model.Special

/** Scans a board for maximal runs of 3+ same-type gems (horizontal and vertical). */
object MatchDetector {

    fun findMatches(board: Board): List<Match> {
        val matches = mutableListOf<Match>()
        findRuns(board, horizontal = true, matches)
        findRuns(board, horizontal = false, matches)
        return matches
    }

    private fun findRuns(board: Board, horizontal: Boolean, out: MutableList<Match>) {
        val outer = if (horizontal) board.height else board.width
        val inner = if (horizontal) board.width else board.height
        for (line in 0 until outer) {
            var start = 0
            while (start < inner) {
                val first = gemAt(board, line, start, horizontal) ?: run {
                    start++
                    continue
                }
                // M4: Hypercubes are colorless and can never be part of a run.
                if (containsHypercube(first)) {
                    start++
                    continue
                }
                var end = start + 1
                while (end < inner && !containsHypercube(gemAt(board, line, end, horizontal)) &&
                    gemAt(board, line, end, horizontal)?.type == first.type
                ) {
                    end++
                }
                if (end - start >= 3) {
                    out += Match((start until end).map { index ->
                        if (horizontal) Position(line, index) else Position(index, line)
                    })
                }
                start = end
            }
        }
    }

    private fun containsHypercube(gem: Gem?): Boolean =
        gem?.special == Special.HYPERCUBE


    private fun gemAt(board: Board, line: Int, index: Int, horizontal: Boolean) =
        if (horizontal) board.gemAt(line, index) else board.gemAt(index, line)
}