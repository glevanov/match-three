namespace MatchThree.Engine.Model;

/// <summary>A cell coordinate on the board.</summary>
/// <param name="Row">Row index, 0 = top.</param>
/// <param name="Col">Column index, 0 = left.</param>
public readonly record struct Position(int Row, int Col)
{
    /// <summary>True if <paramref name="other"/> is one step up, down, left, or right of this position.</summary>
    public bool IsOrthogonallyAdjacentTo(Position other) =>
        (Row == other.Row && Math.Abs(Col - other.Col) == 1) ||
        (Col == other.Col && Math.Abs(Row - other.Row) == 1);
}