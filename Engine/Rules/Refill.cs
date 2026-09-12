using MatchThree.Engine.Model;

namespace MatchThree.Engine.Rules;

public static class Refill
{
    public sealed record Result(Board Board, List<Step.Spawn.Placement> Spawned);

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