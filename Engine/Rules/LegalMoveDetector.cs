using MatchThree.Engine.Model;

namespace MatchThree.Engine.Rules;

public static class LegalMoveDetector
{
    public static int LegalMoveCount(Board board)
    {
        var count = 0;
        for (var row = 0; row < board.Height; row++)
        {
            for (var col = 0; col < board.Width; col++)
            {
                var a = new Position(row, col);
                if (board.GemAt(a) is null) continue;

                var neighbors = new[] { new Position(row, col + 1), new Position(row + 1, col) };
                foreach (var b in neighbors)
                {
                    if (board.IsInside(b) && board.GemAt(b) is not null)
                    {
                        var swapped = board.WithSwapped(a, b);
                        var matchLegal = MatchDetector.FindMatches(swapped).Count > 0;
                        var specialLegal = SpecialRules.SwapContactLegal(swapped.GemAt(a), swapped.GemAt(b));
                        if (matchLegal || specialLegal) count++;
                    }
                }
            }
        }
        return count;
    }

    public static bool HasLegalMove(Board board) => LegalMoveCount(board) > 0;
}