namespace MatchThree.Engine.Model;

public readonly record struct Position(int Row, int Col)
{
    public bool IsOrthogonallyAdjacentTo(Position other) =>
        (Row == other.Row && Math.Abs(Col - other.Col) == 1) ||
        (Col == other.Col && Math.Abs(Row - other.Row) == 1);
}