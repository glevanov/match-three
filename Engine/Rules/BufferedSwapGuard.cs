using MatchThree.Engine.Model;

namespace MatchThree.Engine.Rules;

public static class BufferedSwapGuard
{
    public static bool BufferedSwapIsStale(
        Gem? submitA,
        Gem? submitB,
        Gem? settledA,
        Gem? settledB) =>
        !PairHypercubeIds(submitA, submitB).SetEquals(PairHypercubeIds(settledA, settledB));

    private static HashSet<int> PairHypercubeIds(Gem? a, Gem? b)
    {
        var ids = new HashSet<int>();
        if (a?.Special == Special.Hypercube) ids.Add(a.Value.Id);
        if (b?.Special == Special.Hypercube) ids.Add(b.Value.Id);
        return ids;
    }
}