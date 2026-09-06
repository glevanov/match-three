using MatchThree.Engine.Model;

namespace MatchThree.Engine.Rules;

/// <summary>
/// After gravity, all empty cells sit in the top rows (one contiguous block per
/// column). Refill gives each a brand-new gem with a fresh <see cref="Gem.Id"/>.
///
/// Spawn colors can form new matches — the cascade loop detects and clears them,
/// which is the intended behaviour inside a resolution (MECHANICS.md: invariants
/// are checked only after a cascade settles).
/// </summary>
public static class Refill
{
    /// <summary>The refilled board plus one placement per spawned gem.</summary>
    public sealed record Result(Board Board, List<Step.Spawn.Placement> Spawned);

    /// <param name="board">Post-gravity board; empty cells are top-contiguous per column.</param>
    /// <param name="gemTypeCount">Color pool size passed to <paramref name="gemType"/>.</param>
    /// <param name="nextId">Issues fresh stable ids.</param>
    /// <param name="gemType">Picks a color for a new gem.</param>
    public static Result Fill(Board board, int gemTypeCount, Func<int> nextId, Func<int, GemType> gemType)
    {
        var cells = board.CopyCells();
        var spawned = new List<Step.Spawn.Placement>();

        for (var col = 0; col < board.Width; col++)
        {
            var row = 0;
            while (row < board.Height && cells[row, col] is null)
            {
                var gem = new Gem(nextId(), gemType(gemTypeCount));
                cells[row, col] = gem;
                spawned.Add(new Step.Spawn.Placement(gem, new Position(row, col)));
                row++;
            }
        }

        return new Result(Board.Of(board.Width, board.Height, cells), spawned);
    }
}