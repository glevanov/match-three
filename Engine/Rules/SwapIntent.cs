using MatchThree.Engine.Model;

namespace MatchThree.Engine.Rules;

/// <summary>
/// A player-intended swap of two orthogonally adjacent cells, normalized so the
/// lower/left cell is always <see cref="A"/>. Pure C# on purpose: unit-testable,
/// and the engine/UI both consume the same canonical form (so input direction
/// never affects match resolution).
/// </summary>
public sealed record SwapIntent
{
    /// <summary>The normalized first cell (lower row; left-most on ties).</summary>
    public Position A { get; }

    /// <summary>The normalized second cell.</summary>
    public Position B { get; }

    private SwapIntent(Position a, Position b)
    {
        A = a;
        B = b;
    }

    /// <summary>Builds a normalized <see cref="SwapIntent"/>; throws on non-adjacent cells.</summary>
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