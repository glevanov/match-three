using MatchThree.Engine.Model;

namespace MatchThree.Engine.Rules;

public static class Scorer
{
    public const int BASE_POINTS_PER_GEM = 10;

    public static int RoundScore(IEnumerable<Position> clearedCells, int cascadeDepth)
    {
        if (cascadeDepth < 1)
            throw new ArgumentException("cascadeDepth must be >= 1", nameof(cascadeDepth));
        return clearedCells.Distinct().Count() * BASE_POINTS_PER_GEM * cascadeDepth;
    }

    public static int TotalScore(IEnumerable<int> roundScores) => roundScores.Sum();
}