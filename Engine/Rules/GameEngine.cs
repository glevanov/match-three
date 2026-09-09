using MatchThree.Engine.Model;

namespace MatchThree.Engine.Rules;

/// <summary>
/// The pure-C# game engine (AGENTS.md: engine is pure, no Godot dependency under
/// Engine/). Consumes a board + swap, and emits an ordered list of
/// <see cref="Step"/>s which the UI plays back for animation.
///
/// The resolution loop also handles special gems — births (one gem per
/// shape group transforms, precedence 5 &gt; T/L &gt; 4 &gt; 3), cascade/chain
/// detonation, player swap combos, and Hypercube+Hypercube board regeneration.
/// </summary>
public sealed class GameEngine
{
    private const int MAX_CASCADE_ROUNDS = 1_000;
    private const int MAX_RESHUFFLE_ATTEMPTS = 20;

    private readonly BoardConfig config;
    private readonly SeededRandom rng;
    private readonly IdSource idSource = new();

    /// <param name="config">Board size / color pool; defaults to 9x9/6.</param>
    /// <param name="rng">Seeded randomness (AGENTS.md: seeded RNG).</param>
    public GameEngine(BoardConfig config, SeededRandom rng)
    {
        this.config = config;
        this.rng = rng;
    }

    /// <summary>Convenience overload using the default <see cref="BoardConfig"/>.</summary>
    public GameEngine(SeededRandom rng) : this(new BoardConfig(), rng)
    {
    }

    /// <summary>A fresh board satisfying generation invariants (no match, &gt;=1 legal move).</summary>
    public Board NewGame() =>
        new BoardGenerator(
            width: config.Width,
            height: config.Height,
            gemTypeCount: config.GemTypeCount,
            rng: rng,
            idSource: idSource
        ).NewBoard();

    /// <summary>
    /// True if swapping these adjacent cells would create a match, OR the swap
    /// puts two specials into contact, OR a Hypercube contacts any gem
    /// (MECHANICS.md: hypercube trigger and the combo table).
    /// </summary>
    public bool IsLegalSwap(Board board, Position a, Position b)
    {
        if (!board.IsInside(a) || !board.IsInside(b)) return false;
        if (!a.IsOrthogonallyAdjacentTo(b)) return false;
        if (board.GemAt(a) is null || board.GemAt(b) is null) return false;

        var swapped = board.WithSwapped(a, b);
        if (MatchDetector.FindMatches(swapped).Count > 0) return true;
        return SpecialRules.SwapContactLegal(swapped.GemAt(a), swapped.GemAt(b));
    }

    /// <summary>
    /// Resolves a legal swap into the full cascade of steps, ending with a stable
    /// board that contains no matches. Returns null when the swap is illegal —
    /// the caller decides how to animate the rejection.
    /// </summary>
    public Resolution? ResolveSwap(Board board, Position a, Position b)
    {
        if (!IsLegalSwap(board, a, b)) return null;

        var steps = new List<Step>();
        steps.Add(new Step.Swap(a, b));

        var current = board.WithSwapped(a, b);
        var cascadeDepth = 0;
        var rounds = 0;

        // 1) Player-swap special activation: combos and hypercube triggers fire
        //    before any match detection (depth-1 round).
        var swapActivation = ComputeSwapActivation(current, a, b);
        if (swapActivation is not null)
        {
            cascadeDepth = 1;
            steps.Add(new Step.ComboActivate(
                SpecialA: swapActivation.SpecialA,
                SpecialB: swapActivation.SpecialB,
                AffectedCells: swapActivation.Affected));
            steps.Add(new Step.Destroy(swapActivation.Affected));
            steps.Add(new Step.Score(
                Scorer.RoundScore(swapActivation.Affected, cascadeDepth),
                cascadeDepth));

            // Hypercube+Hypercube: full-board clear is followed by an immediate
            // regeneration (invariant-checked by the generator), not a refill.
            if (swapActivation.Regenerate)
            {
                var fresh = NewGame();
                steps.Add(new Step.Settled(fresh));
                return new Resolution(Board: fresh, Steps: steps);
            }

            var gravity = Gravity.Apply(current, swapActivation.Affected);
            if (gravity.Falls.Count > 0) steps.Add(new Step.Fall(gravity.Falls));

            var refill = Refill.Fill(
                board: gravity.Board,
                gemTypeCount: config.GemTypeCount,
                nextId: idSource.Next,
                gemType: count => GemTypes.FromIndex(rng.NextInt(count)));
            if (refill.Spawned.Count > 0) steps.Add(new Step.Spawn(refill.Spawned));
            current = refill.Board;
            rounds++;
        }

        // 2) Standard cascade loop: match -> (births, swept detonations) ->
        //    gravity -> refill -> re-check, until the board is stable.
        while (true)
        {
            var matches = MatchDetector.FindMatches(current);
            if (matches.Count == 0) break;
            cascadeDepth++;

            var matched = matches.SelectMany(m => m.Positions).ToHashSet();
            var extra = SpecialRules.SweptBlastCells(current, matched);
            var destroyed = new HashSet<Position>(matched.Concat(extra));

            var births = SpecialRules.ResolveBirths(current, matches);
            var birthCells = births.Select(b => b.Cell).ToHashSet();
            var destroyedEx = new HashSet<Position>(destroyed.Except(birthCells));

            steps.Add(new Step.Destroy(destroyedEx));
            steps.Add(new Step.Score(Scorer.RoundScore(destroyedEx, cascadeDepth), cascadeDepth));

            var gravity = Gravity.Apply(current, destroyedEx);
            if (gravity.Falls.Count > 0) steps.Add(new Step.Fall(gravity.Falls));

            // Birth gems are surviving gems: apply their transformations on the
            // fallen board (same ids, same colors, new special kinds). These
            // states are impossible by construction (birth cells are excluded
            // from destruction), so invariant violations abort loudly.
            var afterBirth = gravity.Board;
            foreach (var birth in births)
            {
                var finalPos = afterBirth.PositionOf(birth.GemId)
                    ?? throw new InvalidOperationException($"birth gem {birth.GemId} vanished after gravity");
                var gem = afterBirth.GemAt(finalPos)
                    ?? throw new InvalidOperationException($"birth cell {finalPos} is empty on the fallen board");
                afterBirth = afterBirth.WithGem(finalPos, new Gem(gem.Id, gem.Type, birth.Special));
                steps.Add(new Step.SpecialBirth(finalPos, gem.Id, birth.Special));
            }

            var refill = Refill.Fill(
                board: afterBirth,
                gemTypeCount: config.GemTypeCount,
                nextId: idSource.Next,
                gemType: count => GemTypes.FromIndex(rng.NextInt(count)));
            if (refill.Spawned.Count > 0) steps.Add(new Step.Spawn(refill.Spawned));

            current = refill.Board;
            rounds++;
            if (rounds > MAX_CASCADE_ROUNDS)
            {
                throw new InvalidOperationException($"cascade did not settle after {MAX_CASCADE_ROUNDS} rounds");
            }
        }

        steps.Add(new Step.Settled(current));
        return new Resolution(Board: current, Steps: steps);
    }

    /// <summary>
    /// Fisher-Yates reshuffle of the existing gem multiset (MECHANICS.md):
    /// re-validates that the result has no pre-existing match and at least one
    /// legal move. Retries up to <see cref="MAX_RESHUFFLE_ATTEMPTS"/>; returns
    /// null when the board stays dead, which triggers game over in Zen mode.
    /// </summary>
    public Board? Reshuffle(Board board)
    {
        for (var attempt = 0; attempt < MAX_RESHUFFLE_ATTEMPTS; attempt++)
        {
            var candidate = ShuffleMultiset(board);
            if (MatchDetector.FindMatches(candidate).Count == 0 && LegalMoveDetector.HasLegalMove(candidate))
            {
                return candidate;
            }
        }
        return null;
    }

    /// <summary>Shuffles the gems into a fresh layout, preserving ids and types.</summary>
    private Board ShuffleMultiset(Board board)
    {
        var gems = board.Positions().Select(p => board.GemAt(p)).Where(g => g is not null).Select(g => g!.Value).ToList();
        for (var i = gems.Count - 1; i >= 1; i--)
        {
            var j = rng.NextInt(i + 1);
            (gems[i], gems[j]) = (gems[j], gems[i]);
        }
        var next = 0;
        return Board.Create(board.Width, board.Height, _ =>
            next < gems.Count ? gems[next++] : null);
    }

    /// <summary>
    /// Computes what (if anything) a player swap of two gems activates: a combo
    /// between two specials, or a single hypercube trigger against a normal gem.
    /// Returns null when neither gem is a special (plain match path).
    /// </summary>
    private SwapActivation? ComputeSwapActivation(Board board, Position a, Position b)
    {
        var gemA = board.GemAt(a);
        var gemB = board.GemAt(b);
        var specA = gemA?.Special;
        var specB = gemB?.Special;
        if (specA is null && specB is null) return null;

        // Hypercube against a plain gem: clears every gem of the swapped color
        // plus the hypercube itself (single-activator, no combo partner).
        if ((specA == Special.Hypercube && specB is null) ||
            (specB == Special.Hypercube && specA is null))
        {
            var hyperPos = specA == Special.Hypercube ? a : b;
            var otherPos = hyperPos == a ? b : a;
            var partner = board.GemAt(otherPos);
            if (partner is null || partner.Value.Special is not null) return null;
            return new SwapActivation(
                SpecialA: Special.Hypercube,
                SpecialB: null,
                Affected: SpecialRules.HypercubeTriggerCells(board, hyperPos, otherPos, null),
                Regenerate: false);
        }

        // Combo table: the two swapped gems are both specials.
        if (specA is not null && specB is not null)
        {
            var affected = SpecialRules.ComboAffectedCells(board, a, b, specA.Value, specB.Value);
            var regenerate = specA == Special.Hypercube && specB == Special.Hypercube;
            return new SwapActivation(specA.Value, specB.Value, affected, regenerate);
        }
        return null;
    }

    /// <summary>Result of a player-swap special activation.</summary>
    private sealed record SwapActivation(
        Special SpecialA,
        Special? SpecialB,
        ISet<Position> Affected,
        bool Regenerate);
}

/// <summary>Outcome of <see cref="GameEngine.ResolveSwap"/>: the settled board plus its playback steps.</summary>
/// <param name="Board">Settled board snapshot.</param>
/// <param name="Steps">Ordered playback steps.</param>
public sealed record Resolution(Board Board, List<Step> Steps);