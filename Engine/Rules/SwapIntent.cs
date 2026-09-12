using MatchThree.Engine.Model;

namespace MatchThree.Engine.Rules;

public sealed record SwapIntent
{
    public Position A { get; }

    public Position B { get; }

    private SwapIntent(Position a, Position b)
    {
        A = a;
        B = b;
    }

    public static SwapIntent Of(Position first, Position second)
    {
        if (!first.IsOrthogonallyAdjacentTo(second))
            throw new ArgumentException($"swap requires orthogonal neighbors: {first}, {second}");

        return Compare(first, second) <= 0
            ? new SwapIntent(first, second)
            : new SwapIntent(second, first);
    }

    private static int Compare(Position x, Position y) =>
        x.Row != y.Row ? x.Row.CompareTo(y.Row) : x.Col.CompareTo(y.Col);
}