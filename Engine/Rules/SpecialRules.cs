using MatchThree.Engine.Model;

namespace MatchThree.Engine.Rules;

public static class SpecialRules
{
    public sealed record Birth(Special Special, int GemId, Position Cell);

    public static bool SwapContactLegal(Gem? gemA, Gem? gemB)
    {
        var specA = gemA?.Special;
        var specB = gemB?.Special;
        return (specA is not null && specB is not null) ||
               specA == Special.Hypercube ||
               specB == Special.Hypercube;
    }

    public static List<Birth> ResolveBirths(Board board, List<Match> matches) =>
        ShapeGroups(matches)
            .Select(group => BirthForShape(board, group))
            .Where(birth => birth is not null)
            .Select(birth => birth!)
            .ToList();

    private static List<List<Match>> ShapeGroups(List<Match> matches)
    {
        var parent = new int[matches.Count];
        for (var i = 0; i < parent.Length; i++) parent[i] = i;

        int Find(int i)
        {
            var root = i;
            while (parent[root] != root) root = parent[root];
            var cur = i;
            while (parent[cur] != root)
            {
                var next = parent[cur];
                parent[cur] = root;
                cur = next;
            }
            return root;
        }

        var cellOwner = new Dictionary<Position, int>();
        for (var index = 0; index < matches.Count; index++)
        {
            foreach (var pos in matches[index].Positions)
            {
                if (cellOwner.TryGetValue(pos, out var owner))
                {
                    var a = Find(owner);
                    var b = Find(index);
                    if (a != b) parent[Math.Max(a, b)] = Math.Min(a, b);
                }
                else
                {
                    cellOwner[pos] = index;
                }
            }
        }

        var groups = new List<List<Match>>();
        var groupByRoot = new Dictionary<int, int>();
        for (var index = 0; index < matches.Count; index++)
        {
            var root = Find(index);
            if (!groupByRoot.TryGetValue(root, out var groupIndex))
            {
                groupIndex = groups.Count;
                groupByRoot[root] = groupIndex;
                groups.Add(new List<Match>());
            }
            groups[groupIndex].Add(matches[index]);
        }
        return groups;
    }

    private static Birth? BirthForShape(Board board, List<Match> runs)
    {
        var five = runs.FirstOrDefault(m => m.Positions.Count >= 5);
        if (five is not null)
        {
            var cell = FindFirstPlainCell(five.Positions, board) ?? five.Positions[2];
            var gem = board.GemAt(cell)
                ?? throw new InvalidOperationException($"matched run cell {cell} is empty");
            return new Birth(Special.Hypercube, gem.Id, cell);
        }

        var cross = IntersectionCell(board, runs);
        if (cross is not null)
        {
            var gem = board.GemAt(cross.Value)
                ?? throw new InvalidOperationException($"intersection cell {cross} is empty");
            return new Birth(Special.Star, gem.Id, cross.Value);
        }

        var four = runs.FirstOrDefault(m => m.Positions.Count == 4);
        if (four is not null)
        {
            var cell = FindFirstPlainCell(four.Positions, board) ?? four.Positions[1];
            var gem = board.GemAt(cell)
                ?? throw new InvalidOperationException($"matched run cell {cell} is empty");
            return new Birth(Special.Flame, gem.Id, cell);
        }

        return null;
    }

    public static ISet<Position> DetonationCells(Board board, Position center, Special special) =>
        special switch
        {
            Special.Flame => Blast(center, radius: 1, board),
            Special.Star => StarLines(center, board),
            Special.Hypercube => new HashSet<Position>(),
            _ => throw new ArgumentOutOfRangeException(nameof(special)),
        };

    public static ISet<Position> SweptBlastCells(Board board, ISet<Position> matched) =>
        ChainReactionCells(
            board,
            initiallyCleared: matched,
            initialDetonations: Array.Empty<DetonationSeed>())
        .Except(matched)
        .ToHashSet();

    public static ISet<Position> HypercubeTriggerCells(
        Board board,
        Position hyperPos,
        Position partnerPos,
        Special? partnerSpecial)
    {
        var partnerGem = board.GemAt(partnerPos);
        if (partnerGem is null) return new HashSet<Position> { hyperPos };

        var color = partnerGem.Value.Type;
        if (partnerSpecial is null) return HypercubeTriggerCellsByColor(board, hyperPos, color, null);

        var transformed = board.Positions()
            .Where(pos =>
                pos != hyperPos &&
                board.GemAt(pos) is { Type: var type, Special: not Special.Hypercube } &&
                type == color)
            .ToHashSet();

        return ChainReactionCells(
            board,
            initiallyCleared: new[] { hyperPos },
            initialDetonations: transformed.Select(pos => new DetonationSeed(pos, partnerSpecial.Value, color)),
            ignoredDetonators: transformed);
    }

    public static ISet<Position> ComboAffectedCells(
        Board board,
        Position swapA,
        Position swapB,
        Special specA,
        Special specB)
    {
        if (specA == Special.Hypercube || specB == Special.Hypercube)
        {
            if (specA == Special.Hypercube && specB == Special.Hypercube)
            {
                return board.Positions().ToHashSet();
            }
            var hyperPos = specA == Special.Hypercube ? swapA : swapB;
            var partnerPos = hyperPos == swapA ? swapB : swapA;
            var partnerSpec = specA == Special.Hypercube ? specB : specA;
            return HypercubeTriggerCells(board, hyperPos, partnerPos, partnerSpec);
        }

        var affected = (specA, specB) switch
        {
            (Special.Flame, Special.Flame) => Blast(swapA, radius: 2, board),
            (Special.Flame, Special.Star) or (Special.Star, Special.Flame) => ThickCross(swapA, board),
            (Special.Star, Special.Star) => StarLines(swapA, board).Union(StarLines(swapB, board)).ToHashSet(),
            _ => new HashSet<Position>(),
        };

        return ChainReactionCells(
            board,
            initiallyCleared: affected,
            initialDetonations: Array.Empty<DetonationSeed>(),
            ignoredDetonators: new HashSet<Position> { swapA, swapB });
    }

    private sealed record DetonationSeed(Position Center, Special Effect, GemType Color);

    private static ISet<Position> ChainReactionCells(
        Board board,
        IEnumerable<Position> initiallyCleared,
        IEnumerable<DetonationSeed> initialDetonations,
        ISet<Position>? ignoredDetonators = null)
    {
        var ignored = ignoredDetonators ?? new HashSet<Position>();
        var affected = initiallyCleared.ToHashSet();
        var pending = new Queue<DetonationSeed>();
        var queuedDetonators = new HashSet<Position>();
        var triggeredHypercubes = new HashSet<Position>();

        void EnqueueSeed(DetonationSeed seed)
        {
            if (queuedDetonators.Add(seed.Center)) pending.Enqueue(seed);
        }

        void EnqueueActualDetonator(Position pos)
        {
            if (ignored.Contains(pos)) return;
            var gem = board.GemAt(pos);
            if (gem is { Special: { } special, Type: var color } && special != Special.Hypercube)
            {
                EnqueueSeed(new DetonationSeed(pos, special, color));
            }
        }

        foreach (var seed in initialDetonations.OrderBy(seed => seed.Center.Row).ThenBy(seed => seed.Center.Col))
        {
            EnqueueSeed(seed);
        }
        foreach (var pos in OrderByBoard(initiallyCleared))
        {
            EnqueueActualDetonator(pos);
        }

        while (pending.Count > 0)
        {
            var seed = pending.Dequeue();
            foreach (var cell in OrderByBoard(DetonationCells(board, seed.Center, seed.Effect)))
            {
                affected.Add(cell);

                var gem = board.GemAt(cell);
                if (gem is not { Special: { } special }) continue;

                if (special == Special.Hypercube)
                {
                    if (triggeredHypercubes.Add(cell))
                    {
                        affected.UnionWith(HypercubeTriggerCellsByColor(board, cell, seed.Color, null));
                    }
                }
                else
                {
                    EnqueueActualDetonator(cell);
                }
            }
        }

        return affected;
    }

    private static ISet<Position> HypercubeTriggerCellsByColor(
        Board board,
        Position hyperPos,
        GemType color,
        Special? partnerSpecial)
    {
        var affected = new HashSet<Position> { hyperPos };
        foreach (var pos in board.Positions())
        {
            var gem = board.GemAt(pos);
            if (gem is not { Type: var type, Special: not Special.Hypercube } || type != color) continue;
            if (partnerSpecial is null) affected.Add(pos);
            else affected.UnionWith(DetonationCells(board, pos, partnerSpecial.Value));
        }
        return affected;
    }

    private static IEnumerable<Position> OrderByBoard(IEnumerable<Position> cells) =>
        cells.OrderBy(pos => pos.Row).ThenBy(pos => pos.Col);

    private static Position? FindFirstPlainCell(List<Position> cells, Board board)
    {
        foreach (var pos in cells)
        {
            if (board.GemAt(pos)?.Special is null) return pos;
        }
        return null;
    }

    private static Position? IntersectionCell(Board board, List<Match> matches)
    {
        var horizontal = matches
            .Where(m => m.Positions.All(p => p.Row == m.Positions[0].Row))
            .ToList();
        var vertical = matches
            .Where(m => m.Positions.All(p => p.Col == m.Positions[0].Col))
            .ToList();

        foreach (var h in horizontal)
        {
            foreach (var v in vertical)
            {
                foreach (var pos in h.Positions)
                {
                    if (v.Positions.Contains(pos) && board.GemAt(pos)?.Special is null) return pos;
                }
            }
        }
        return null;
    }

    private static ISet<Position> Blast(Position center, int radius, Board board)
    {
        var cells = new HashSet<Position>();
        for (var dr = -radius; dr <= radius; dr++)
        {
            for (var dc = -radius; dc <= radius; dc++)
            {
                var pos = new Position(center.Row + dr, center.Col + dc);
                if (board.IsInside(pos)) cells.Add(pos);
            }
        }
        return cells;
    }

    private static ISet<Position> StarLines(Position center, Board board)
    {
        var cells = new HashSet<Position>();
        for (var col = 0; col < board.Width; col++) cells.Add(new Position(center.Row, col));
        for (var row = 0; row < board.Height; row++) cells.Add(new Position(row, center.Col));
        return cells;
    }

    private static ISet<Position> ThickCross(Position center, Board board)
    {
        var cells = new HashSet<Position>();
        for (var dc = -1; dc <= 1; dc++)
        {
            for (var col = 0; col < board.Width; col++)
            {
                var pos = new Position(center.Row + dc, col);
                if (board.IsInside(pos)) cells.Add(pos);
            }
        }
        for (var dr = -1; dr <= 1; dr++)
        {
            for (var row = 0; row < board.Height; row++)
            {
                var pos = new Position(row, center.Col + dr);
                if (board.IsInside(pos)) cells.Add(pos);
            }
        }
        return cells;
    }
}