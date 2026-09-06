using MatchThree.Engine.Model;

namespace MatchThree.Engine.Rules;

/// <summary>
/// A maximal horizontal or vertical run of 3+ same-type gems.
/// Shape precedence (5-run &gt; T/L &gt; 4-run &gt; 3-run) is decided in
/// <see cref="SpecialRules"/>.
/// </summary>
public sealed record Match
{
    /// <summary>Cells covered by the run, in scan order.</summary>
    public List<Position> Positions { get; }

    /// <summary>Number of cells in the run.</summary>
    public int Size => Positions.Count;

    public Match(List<Position> positions)
    {
        if (positions.Count < 3)
            throw new ArgumentException("match must cover at least 3 cells", nameof(positions));
        Positions = positions;
    }
}