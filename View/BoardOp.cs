using MatchThree.Engine.Model;
using MatchThree.Engine.Rules;

namespace MatchThree.View;

public abstract record BoardOp
{
    public sealed record Play(List<Step> Steps) : BoardOp;

    public sealed record Reject(SwapIntent Intent) : BoardOp;

    public sealed record Resync(Board Board) : BoardOp;
}

public enum GamePhase
{
    Idle,
    Resolving,
    Rejecting,
    GameOver,
}