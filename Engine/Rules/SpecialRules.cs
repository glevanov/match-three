using MatchThree.Engine.Model;

namespace MatchThree.Engine.Rules;

/// <summary>
/// Special-gem rules (MECHANICS.md). All functions are deterministic and
/// unit-testable — the engine calls these, the tests call these directly.
///
/// Birth is per shape (MECHANICS.md): runs sharing cells form one shape, and
/// each shape births exactly one special by precedence <b>5-in-row &gt; T/L &gt;
/// 4-in-row &gt; plain 3</b> — one matched gem transforms, the rest clear normally.
/// Non-overlapping shapes resolve independently, so one cascade round can birth
/// several specials.
/// </summary>
public static class SpecialRules
{
    /// <summary>A gem chosen to transform into a special during a cascade round.</summary>
    /// <param name="Special">Special kind born.</param>
    /// <param name="GemId">Stable id of the transformed gem.</param>
    /// <param name="Cell">Birth cell (pre-fall).</param>
    public sealed record Birth(Special Special, int GemId, Position Cell);

    /// <summary>
    /// True when the swap of <paramref name="gemA"/> and <paramref name="gemB"/>
    /// is legal even without a match.
    /// </summary>
    public static bool SwapContactLegal(Gem? gemA, Gem? gemB)
    {
        var specA = gemA?.Special;
        var specB = gemB?.Special;
        return (specA is not null && specB is not null) ||
               specA == Special.Hypercube ||
               specB == Special.Hypercube;
    }

    /// <summary>
    /// Decides the specials born from one cascade round: runs are clustered into
    /// shapes by shared cells, and each shape births at most one special by
    /// precedence (5-run &gt; T/L &gt; 4-run; plain 3-runs birth nothing). Groups are
    /// ordered by their first run's index in <paramref name="matches"/> — deterministic.
    /// </summary>
    public static List<Birth> ResolveBirths(Board board, List<Match> matches) =>
        ShapeGroups(matches)
            .Select(group => BirthForShape(board, group))
            .Where(birth => birth is not null)
            .Select(birth => birth!)
            .ToList();

    /// <summary>
    /// Clusters runs into shapes: runs sharing any cell belong to the same shape
    /// (a T/L is one shape of two intersecting runs; a shared cell implies the
    /// same gem and color, since a cell holds one gem).
    /// </summary>
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

        // Deterministic group order: first run's index in `matches`.
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

    /// <summary>The one special born from a single shape (or null for plain 3-runs).</summary>
    private static Birth? BirthForShape(Board board, List<Match> runs)
    {
        // 1. 5-in-row wins over everything (including a crossing T/L).
        var five = runs.FirstOrDefault(m => m.Positions.Count >= 5);
        if (five is not null)
        {
            // Pick the first group cell with no special, falling back to the
            // third cell (index 2). FirstOrDefault can't express "not found"
            // for a value-type Position (default == (0,0) is a real cell), so
            // search explicitly.
            var cell = FindFirstPlainCell(five.Positions, board) ?? five.Positions[2];
            var gem = board.GemAt(cell)
                ?? throw new InvalidOperationException($"matched run cell {cell} is empty");
            return new Birth(Special.Hypercube, gem.Id, cell);
        }

        // 2. T/L: a position shared by a horizontal and a vertical run.
        var cross = IntersectionCell(board, runs);
        if (cross is not null)
        {
            var gem = board.GemAt(cross.Value)
                ?? throw new InvalidOperationException($"intersection cell {cross} is empty");
            return new Birth(Special.Star, gem.Id, cross.Value);
        }

        // 3. 4-in-row becomes a Flame.
        var four = runs.FirstOrDefault(m => m.Positions.Count == 4);
        if (four is not null)
        {
            var cell = FindFirstPlainCell(four.Positions, board) ?? four.Positions[1];
            var gem = board.GemAt(cell)
                ?? throw new InvalidOperationException($"matched run cell {cell} is empty");
            return new Birth(Special.Flame, gem.Id, cell);
        }

        // 4. Plain 3-runs birth nothing.
        return null;
    }

    /// <summary>
    /// Cells cleared when <paramref name="special"/> detonates around
    /// <paramref name="center"/>: Flame = 3x3, Star = its row + column.
    /// Hypercubes never reach this path (colorless: they cannot be part of a
    /// match), so they add nothing here.
    /// </summary>
    public static ISet<Position> DetonationCells(Board board, Position center, Special special) =>
        special switch
        {
            Special.Flame => Blast(center, radius: 1, board),
            Special.Star => StarLines(center, board),
            Special.Hypercube => new HashSet<Position>(),
            _ => throw new ArgumentOutOfRangeException(nameof(special)),
        };

    /// <summary>
    /// Extra detonations from specials swept into a cascade round's match cells.
    /// The matched cells themselves are cleared anyway; this adds their blasts.
    /// </summary>
    public static ISet<Position> SweptBlastCells(Board board, ISet<Position> matched)
    {
        var extra = new HashSet<Position>();
        foreach (var pos in matched)
        {
            var gem = board.GemAt(pos);
            // gem.special is nullable (Special?); use an explicit property
            // pattern to unwrap it.
            if (gem is { Special: { } special } && special != Special.Hypercube)
            {
                extra.UnionWith(DetonationCells(board, pos, special));
            }
        }
        return extra;
    }

    /// <summary>
    /// Cells cleared when <paramref name="hyperPos"/>'s hypercube triggers
    /// against the gem at <paramref name="partnerPos"/> (MECHANICS.md trigger
    /// rule and combo table). A plain partner clears every gem of its color; a
    /// special partner powers every gem of its color up into that special,
    /// which then detonates. The hypercube itself is always consumed and included.
    /// </summary>
    public static ISet<Position> HypercubeTriggerCells(
        Board board,
        Position hyperPos,
        Position partnerPos,
        Special? partnerSpecial)
    {
        var partnerGem = board.GemAt(partnerPos);
        if (partnerGem is null) return new HashSet<Position> { hyperPos };

        var color = partnerGem.Value.Type;
        var affected = new HashSet<Position> { hyperPos };
        foreach (var pos in board.Positions())
        {
            if (board.GemAt(pos)?.Type != color) continue;
            if (partnerSpecial is null) affected.Add(pos);
            else affected.UnionWith(DetonationCells(board, pos, partnerSpecial.Value));
        }
        return affected;
    }

    /// <summary>
    /// The cells cleared by a player-swap combo (MECHANICS.md combo table).
    /// Both swapped positions count as cleared (the specials are consumed).
    /// </summary>
    public static ISet<Position> ComboAffectedCells(
        Board board,
        Position swapA,
        Position swapB,
        Special specA,
        Special specB)
    {
        // Hypercube combos: every gem of the swapped partner's color powers up
        // into the partner's special, then detonates simultaneously; the
        // hypercube itself is consumed too.
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

        return (specA, specB) switch
        {
            (Special.Flame, Special.Flame) => Blast(swapA, radius: 2, board),
            (Special.Flame, Special.Star) or (Special.Star, Special.Flame) => ThickCross(swapA, board),
            (Special.Star, Special.Star) => StarLines(swapA, board).Union(StarLines(swapB, board)).ToHashSet(),
            // Elided left-to-right for the compiler: the enum has exactly three
            // members, so (Flame,Hypercube)/(Star,Hypercube)/... are covered by
            // the Hypercube branch above; this arm is unreachable.
            _ => new HashSet<Position>(),
        };
    }

    // --- shape analysis -----------------------------------------------------

    /// <summary>First cell in <paramref name="cells"/> holding a non-special gem, or null.</summary>
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

    // --- area helpers -------------------------------------------------------

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

    /// <summary>The 3-wide row + 3-wide column "thick cross" through <paramref name="center"/>.</summary>
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