using MatchThree.Engine.Model;

namespace MatchThree.Engine.Rules;

/// <summary>
/// Staleness gate for swaps that were buffered during input lock (MECHANICS.md,
/// Input lock): a Hypercube is only ever consumed by a gesture that targeted it,
/// so a buffered swap may execute against the settled board only when no
/// Hypercube entered or left the swapped pair between the gesture and the flush,
/// compared by stable <see cref="Gem.Id"/>.
///
/// Consequences (all tested in BufferedSwapGuardTest):
///  - a Hypercube born into or fallen into the pair during resolution makes the
///    buffered swap stale — the player never aimed at it;
///  - a Hypercube that fell from one cell of the pair to the other keeps its id,
///    so a deliberate Hypercube drag still fires;
///  - with no Hypercube involved at either time the swap is never stale — the
///    decision-log promise ("buffer most-recent drag, not drop") is preserved
///    for plain gems.
///
/// Pure C# on purpose (precedent: SwapIntent in Kotlin): the game state machine
/// stays thin wiring and this rule runs under `dotnet test`.
/// </summary>
public static class BufferedSwapGuard
{
    /// <summary>
    /// True when a buffered swap must NOT execute against the settled board.
    /// </summary>
    /// <param name="submitA">Gem at cell A as the gesture saw it at submit time.</param>
    /// <param name="submitB">Gem at cell B as the gesture saw it at submit time.</param>
    /// <param name="settledA">Gem at cell A on the settled board.</param>
    /// <param name="settledB">Gem at cell B on the settled board.</param>
    public static bool BufferedSwapIsStale(
        Gem? submitA,
        Gem? submitB,
        Gem? settledA,
        Gem? settledB) =>
        !PairHypercubeIds(submitA, submitB).SetEquals(PairHypercubeIds(settledA, settledB));

    /// <summary>Ids of the pair's Hypercube gems; absent gems and plain specials contribute nothing.</summary>
    private static HashSet<int> PairHypercubeIds(Gem? a, Gem? b)
    {
        var ids = new HashSet<int>();
        if (a?.Special == Special.Hypercube) ids.Add(a.Value.Id);
        if (b?.Special == Special.Hypercube) ids.Add(b.Value.Id);
        return ids;
    }
}