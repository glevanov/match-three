using MatchThree.Engine.Model;

namespace MatchThree.Engine.Rules;

public sealed record Match
{
    public List<Position> Positions { get; }

    public int Size => Positions.Count;

    public Match(List<Position> positions)
    {
        if (positions.Count < 3)
            throw new ArgumentException("match must cover at least 3 cells", nameof(positions));
        Positions = positions;
    }
}