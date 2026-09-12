using MatchThree.Engine.Model;

namespace MatchThree.Engine.Rules;

public abstract record Step
{
    public sealed record Swap(Position A, Position B) : Step;

    public sealed record ComboActivate(Special SpecialA, Special? SpecialB, ISet<Position> AffectedCells) : Step;

    public sealed record SpecialBirth(Position Position, int GemId, Special Special) : Step;

    public sealed record Destroy(ISet<Position> Positions) : Step;

    public sealed record Score(int Delta, int CascadeDepth) : Step;

    public sealed record Fall(List<Fall.FallMove> Moves) : Step
    {
        public sealed record FallMove(int GemId, Position From, Position To);
    }

    public sealed record Spawn(List<Spawn.Placement> Gems) : Step
    {
        public sealed record Placement(Gem Gem, Position Position);
    }

    public sealed record Settled(Board Board) : Step;
}