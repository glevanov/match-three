using MatchThree.Engine.Model;

namespace MatchThree.Engine.Rules;

/// <summary>
/// Scoring per MECHANICS.md:
///
/// - Base: <b>10 points</b> per cleared gem.
/// - Cascade multiplier: linear by depth, uncapped (<c>multiplier = cascadeDepth</c>).
/// - Unique-cell scoring: <c>gemsClearedThisStep</c> is the count of <b>unique</b>
///   cleared positions. Overlapping regions (row+column clears, intersecting
///   matches) never double-count — <see cref="RoundScore"/> dedupes via a set.
/// - <c>stepScore = uniqueCells * 10 * cascadeDepth</c>; total = sum over the chain.
///
/// No special-creation bonus in v1 (MECHANICS.md non-goal).
/// </summary>
public static class Scorer
{
    /// <summary>Base points per unique cleared gem at depth 1.</summary>
    public const int BASE_POINTS_PER_GEM = 10;

    /// <summary>
    /// Points for one cascade round clearing <paramref name="clearedCells"/> at
    /// <paramref name="cascadeDepth"/>. Duplicate positions are counted once
    /// (unique-cell scoring).
    /// </summary>
    public static int RoundScore(IEnumerable<Position> clearedCells, int cascadeDepth)
    {
        if (cascadeDepth < 1)
            throw new ArgumentException("cascadeDepth must be >= 1", nameof(cascadeDepth));
        return clearedCells.Distinct().Count() * BASE_POINTS_PER_GEM * cascadeDepth;
    }

    /// <summary>Total score for a full resolution, summing each round's score.</summary>
    public static int TotalScore(IEnumerable<int> roundScores) => roundScores.Sum();
}