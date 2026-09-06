using MatchThree.Engine.Model;

namespace MatchThree.Engine.Rules;

/// <summary>
/// Applies gravity after a clear: every surviving gem in a column drops to the
/// lowest free row, preserving its column order and its stable
/// <see cref="Gem.Id"/>.
/// </summary>
public static class Gravity
{
    /// <summary>The fallen board plus one fall move per moved gem.</summary>
    public sealed record Result(Board Board, List<Step.Fall.FallMove> Falls);

    public static Result Apply(Board board, ISet<Position> destroyed)
    {
        var cells = board.CopyCells();
        var falls = new List<Step.Fall.FallMove>();

        for (var col = 0; col < board.Width; col++)
        {
            var targetRow = board.Height - 1;
            for (var row = board.Height - 1; row >= 0; row--)
            {
                if (destroyed.Contains(new Position(row, col)))
                {
                    cells[row, col] = null;
                    continue;
                }

                var gem = cells[row, col];
                if (gem is null) continue;

                if (row != targetRow)
                {
                    cells[targetRow, col] = gem;
                    cells[row, col] = null;
                    falls.Add(new Step.Fall.FallMove(gem.Value.Id, new Position(row, col), new Position(targetRow, col)));
                }
                targetRow--;
            }
        }

        return new Result(Board.Of(board.Width, board.Height, cells), falls);
    }
}