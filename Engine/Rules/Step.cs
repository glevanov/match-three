using MatchThree.Engine.Model;

namespace MatchThree.Engine.Rules;

/// <summary>
/// Ordered events the engine emits while resolving a player swap. The view
/// plays these back in order to drive animation (AGENTS.md: "Steps, not state").
///
/// A sealed record hierarchy with switch pattern matching; the compiler
/// enforces exhaustiveness across the sealed subtypes.
/// </summary>
public abstract record Step
{
    /// <summary>Two adjacent gems trade places at the start of a (legal) turn.</summary>
    /// <param name="A">First swapped cell.</param>
    /// <param name="B">Second swapped cell.</param>
    public sealed record Swap(Position A, Position B) : Step;

    /// <summary>A special-special combo / the hypercube trigger fires.</summary>
    /// <param name="SpecialA">First activator special.</param>
    /// <param name="SpecialB">Second activator special, or null for a single hypercube trigger.</param>
    /// <param name="AffectedCells">Cells cleared by the activation.</param>
    public sealed record ComboActivate(Special SpecialA, Special? SpecialB, ISet<Position> AffectedCells) : Step;

    /// <summary>A matched gem transforms into a special (birth rule, MECHANICS.md).</summary>
    /// <param name="Position">Final (post-fall) birth cell.</param>
    /// <param name="GemId">Stable id of the transformed gem.</param>
    /// <param name="Special">Special kind born.</param>
    public sealed record SpecialBirth(Position Position, int GemId, Special Special) : Step;

    /// <summary>The gems at these positions are cleared (a run now; blasts/combos later).</summary>
    /// <param name="Positions">Cleared cells; unique-cell semantics.</param>
    public sealed record Destroy(ISet<Position> Positions) : Step;

    /// <summary>Points awarded for one cascade round; depth 1 is the initiating match.</summary>
    /// <param name="Delta">Points for this round.</param>
    /// <param name="CascadeDepth">Round depth, 1-based.</param>
    public sealed record Score(int Delta, int CascadeDepth) : Step;

    /// <summary>Gems dropping straight down after a clear; one entry per moved gem.</summary>
    /// <param name="Moves">Per-gem fall moves.</param>
    public sealed record Fall(List<Fall.FallMove> Moves) : Step
    {
        /// <summary>One gem's straight-down drop.</summary>
        /// <param name="GemId">Stable gem id, so the view can track the actor.</param>
        /// <param name="From">Start cell.</param>
        /// <param name="To">End cell.</param>
        public sealed record FallMove(int GemId, Position From, Position To);
    }

    /// <summary>Brand-new gems appearing in the top rows after a fall.</summary>
    /// <param name="Gems">Spawned placements.</param>
    public sealed record Spawn(List<Spawn.Placement> Gems) : Step
    {
        /// <summary>One spawned gem.</summary>
        /// <param name="Gem">The new gem (fresh stable id).</param>
        /// <param name="Position">Spawn cell.</param>
        public sealed record Placement(Gem Gem, Position Position);
    }

    /// <summary>Resolution completed; the board is stable (no matches remain).</summary>
    /// <param name="Board">The settled board snapshot.</param>
    public sealed record Settled(Board Board) : Step;
}