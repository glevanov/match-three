using MatchThree.Engine.Model;

namespace MatchThree.Engine.Rules;

public sealed class GameEngine
{
    private const int MAX_CASCADE_ROUNDS = 1_000;
    private const int MAX_RESHUFFLE_ATTEMPTS = 20;

    private readonly BoardConfig config;
    private readonly SeededRandom rng;
    private readonly IdSource idSource = new();

    public GameEngine(BoardConfig config, SeededRandom rng)
    {
        this.config = config;
        this.rng = rng;
    }

    public GameEngine(SeededRandom rng) : this(new BoardConfig(), rng)
    {
    }

    public Board NewGame() =>
        new BoardGenerator(
            width: config.Width,
            height: config.Height,
            gemTypeCount: config.GemTypeCount,
            rng: rng,
            idSource: idSource
        ).NewBoard();

    public bool IsLegalSwap(Board board, Position a, Position b)
    {
        if (!board.IsInside(a) || !board.IsInside(b)) return false;
        if (!a.IsOrthogonallyAdjacentTo(b)) return false;
        if (board.GemAt(a) is null || board.GemAt(b) is null) return false;

        var swapped = board.WithSwapped(a, b);
        if (MatchDetector.FindMatches(swapped).Count > 0) return true;
        return SpecialRules.SwapContactLegal(swapped.GemAt(a), swapped.GemAt(b));
    }

    public Resolution? ResolveSwap(Board board, Position a, Position b)
    {
        if (!IsLegalSwap(board, a, b)) return null;

        var steps = new List<Step>();
        steps.Add(new Step.Swap(a, b));

        var current = board.WithSwapped(a, b);
        var cascadeDepth = 0;
        var rounds = 0;

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

    private SwapActivation? ComputeSwapActivation(Board board, Position a, Position b)
    {
        var gemA = board.GemAt(a);
        var gemB = board.GemAt(b);
        var specA = gemA?.Special;
        var specB = gemB?.Special;
        if (specA is null && specB is null) return null;

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

        if (specA is not null && specB is not null)
        {
            var affected = SpecialRules.ComboAffectedCells(board, a, b, specA.Value, specB.Value);
            var regenerate = specA == Special.Hypercube && specB == Special.Hypercube;
            return new SwapActivation(specA.Value, specB.Value, affected, regenerate);
        }
        return null;
    }

    private sealed record SwapActivation(
        Special SpecialA,
        Special? SpecialB,
        ISet<Position> Affected,
        bool Regenerate);
}

public sealed record Resolution(Board Board, List<Step> Steps);