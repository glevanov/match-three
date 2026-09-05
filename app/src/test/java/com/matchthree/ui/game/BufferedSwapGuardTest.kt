package com.matchthree.ui.game

import com.matchthree.game.model.Gem
import com.matchthree.game.model.GemType
import com.matchthree.game.model.Special
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Tests the buffered-swap staleness rule (MECHANICS.md, Input lock): a swap
 * buffered during input lock goes stale exactly when a Hypercube entered or
 * left its pair between the gesture and the flush, compared by gem id.
 */
class BufferedSwapGuardTest {

    private var nextId = 0

    private fun gem(type: GemType = GemType.RED, special: Special? = null): Gem =
        Gem(nextId++, type, special)

    @Test
    fun `plain pair whose gems changed entirely is not stale`() {
        // Fast-player path: a plain buffered swap still executes after a cascade
        // even when both cells hold different gems by then.
        val submitA = gem()
        val submitB = gem()
        assertFalse(
            bufferedSwapIsStale(submitA, submitB, settledA = gem(), settledB = gem()),
        )
    }

    @Test
    fun `hypercube still in the pair at settle is not stale`() {
        val hyper = gem(GemType.BLUE, Special.HYPERCUBE)
        val plain = gem()
        assertFalse(bufferedSwapIsStale(hyper, plain, hyper, plain))
    }

    @Test
    fun `hypercube born or fallen into the pair is stale`() {
        // The bug case: the buffered gesture never aimed at this Hypercube.
        val plainA = gem()
        val plainB = gem()
        val settledHyper = gem(GemType.BLUE, Special.HYPERCUBE)
        assertTrue(bufferedSwapIsStale(plainA, plainB, settledHyper, plainB))
        assertTrue(bufferedSwapIsStale(plainA, plainB, plainA, settledHyper))
    }

    @Test
    fun `hypercube that left the pair is stale`() {
        val hyper = gem(GemType.BLUE, Special.HYPERCUBE)
        val plain = gem()
        assertTrue(
            bufferedSwapIsStale(hyper, plain, settledA = gem(), settledB = gem()),
        )
    }

    @Test
    fun `hypercube falling within the pair keeps the swap fresh`() {
        // Same gem id, other cell of the pair: still the Hypercube the player targeted.
        val hyper = gem(GemType.BLUE, Special.HYPERCUBE)
        val plain = gem()
        assertFalse(bufferedSwapIsStale(hyper, plain, plain, hyper))
    }

    @Test
    fun `a second hypercube entering the pair is stale`() {
        // The flush would fire a Hypercube+Hypercube combo the player never gestured at.
        val hyper = gem(GemType.BLUE, Special.HYPERCUBE)
        val plain = gem()
        val secondHyper = gem(GemType.GREEN, Special.HYPERCUBE)
        assertTrue(bufferedSwapIsStale(hyper, plain, hyper, secondHyper))
    }

    @Test
    fun `empty cells count as no hypercube and never crash`() {
        val hyper = gem(GemType.BLUE, Special.HYPERCUBE)
        assertFalse(bufferedSwapIsStale(null, null, null, null))
        assertTrue(bufferedSwapIsStale(null, null, hyper, null))
        assertTrue(bufferedSwapIsStale(hyper, null, null, null))
    }
}
