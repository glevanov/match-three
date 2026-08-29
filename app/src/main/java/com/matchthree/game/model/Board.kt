package com.matchthree.game.model

/**
 * An immutable 9x9 (or config-sized) grid of gems. `null` marks an empty cell
 * (e.g. a cell awaiting a spawned gem). Engine passes return snapshots; the
 * grid itself is never mutated after construction.
 */
class Board private constructor(
    val width: Int,
    val height: Int,
    internal val cells: Array<Array<Gem?>>,
) {
    init {
        require(cells.size == height) { "cells rows != height" }
        require(cells.all { it.size == width }) { "cells cols != width" }
    }

    fun gemAt(row: Int, col: Int): Gem? =
        if (row in 0 until height && col in 0 until width) cells[row][col] else null

    fun gemAt(position: Position): Gem? = gemAt(position.row, position.col)

    fun isInside(position: Position): Boolean =
        position.row in 0 until height && position.col in 0 until width

    /** All board positions, row-major. */
    fun positions(): List<Position> =
        (0 until height).flatMap { row -> (0 until width).map { col -> Position(row, col) } }

    /** A deep-copied board with the two positions swapped. */
    fun withSwapped(a: Position, b: Position): Board {
        require(isInside(a) && isInside(b))
        val copy = copyCells()
        val gemA = copy[a.row][a.col]
        copy[a.row][a.col] = copy[b.row][b.col]
        copy[b.row][b.col] = gemA
        return Board(width, height, copy)
    }

    /** A deep-copied board with [gem] placed at [position]. */
    fun withGem(position: Position, gem: Gem?): Board {
        require(isInside(position))
        val copy = copyCells()
        copy[position.row][position.col] = gem
        return Board(width, height, copy)
    }

    private fun copyCells(): Array<Array<Gem?>> =
        Array(height) { row -> cells[row].copyOf() }

    companion object {
        /** Wraps a cell grid. The grid must match [width] x [height]. */
        fun of(width: Int, height: Int, cells: Array<Array<Gem?>>): Board =
            Board(width, height, cells)

        /** A board builder; [factory] returns the gem for each position (row-major). */
        fun create(width: Int, height: Int, factory: (Position) -> Gem?): Board {
            val cells = Array(height) { row ->
                Array(width) { col -> factory(Position(row, col)) }
            }
            return Board(width, height, cells)
        }
    }
}