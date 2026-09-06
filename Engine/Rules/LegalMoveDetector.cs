using MatchThree.Engine.Model;

namespace MatchThree.Engine.Rules;

/// <summary>
/// Counts swaps that would create a match. A swap is legal iff it is orthogonal
/// adjacency between two present gems and the swapped board contains a match —
/// or the swap puts two specials into contact / a Hypercube touches any gem
/// (MECHANICS.md combo table, handled via <see cref="SpecialRules.SwapContactLegal"/>).
/// </summary>
public static class LegalMoveDetector
{
    /// <summary>Number of distinct adjacent pairs whose swap is legal.</summary>
    public static int LegalMoveCount(Board board)
    {
        var count = 0;
        for (var row = 0; row < board.Height; row++)
        {
            for (var col = 0; col < board.Width; col++)
            {
                var a = new Position(row, col);
                if (board.GemAt(a) is null) continue;

                // Test each orthogonal neighbor once (right and down).
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

    /// <summary>True if at least one legal swap exists on <paramref name="board"/>.</summary>
    public static bool HasLegalMove(Board board) => LegalMoveCount(board) > 0;
}