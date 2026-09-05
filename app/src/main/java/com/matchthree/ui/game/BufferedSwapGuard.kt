package com.matchthree.ui.game

import com.matchthree.game.model.Gem
import com.matchthree.game.model.Special

/**
 * Staleness gate for swaps that were buffered during input lock (MECHANICS.md,
 * Input lock): a Hypercube is only ever consumed by a gesture that targeted it,
 * so a buffered swap may execute against the settled board only when no
 * Hypercube entered or left the swapped pair between the gesture and the flush,
 * compared by stable [Gem.id].
 *
 * Consequences (all tested in BufferedSwapGuardTest):
 *  - a Hypercube born into or fallen into the pair during resolution makes the
 *    buffered swap stale — the player never aimed at it;
 *  - a Hypercube that fell from one cell of the pair to the other keeps its id,
 *    so a deliberate Hypercube drag still fires;
 *  - with no Hypercube involved at either time the swap is never stale — the
 *    decision-log promise ("buffer most-recent drag, not drop") is preserved
 *    for plain gems.
 *
 * Pure Kotlin on purpose (precedent: SwapIntent.kt): the [GameViewModel] stays
 * thin wiring and this rule runs on the JVM in tests.
 */
fun bufferedSwapIsStale(
    submitA: Gem?,
    submitB: Gem?,
    settledA: Gem?,
    settledB: Gem?,
): Boolean = pairHypercubeIds(listOf(submitA, submitB)) != pairHypercubeIds(listOf(settledA, settledB))

/** Ids of the pair's Hypercube gems; absent gems and plain specials contribute nothing. */
private fun pairHypercubeIds(gems: List<Gem?>): Set<Int> =
    gems.mapNotNull { gem ->
        if (gem?.special == Special.HYPERCUBE) gem.id else null
    }.toSet()
