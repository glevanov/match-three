using MatchThree.Engine.Model;
using MatchThree.Engine.Rules;

namespace MatchThree.View;

/// <summary>
/// One unit of ordered UI work for the Game.cs single consumer, mirroring the
/// Kotlin BoardOp sealed interface.
/// </summary>
public abstract record BoardOp
{
    /// <summary>A batch of engine steps handed to the UI for playback.</summary>
    public sealed record Play(List<Step> Steps) : BoardOp;

    /// <summary>An invalid swap to animate there-and-back.</summary>
    public sealed record Reject(SwapIntent Intent) : BoardOp;

    /// <summary>Snap the actor pool to <paramref name="Board"/> (e.g. after a dead-board reshuffle).</summary>
    public sealed record Resync(Board Board) : BoardOp;
}

/// <summary>Input/lock state + what the UI should play next (Kotlin GamePhase).</summary>
public enum GamePhase
{
    Idle,
    Resolving,
    Rejecting,
    GameOver,
}