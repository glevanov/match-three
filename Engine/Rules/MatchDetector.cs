using MatchThree.Engine.Model;

namespace MatchThree.Engine.Rules;

/// <summary>Scans a board for maximal runs of 3+ same-type gems (horizontal and vertical).</summary>
public static class MatchDetector
{
    /// <summary>All maximal horizontal and vertical runs on <paramref name="board"/>.</summary>
    public static List<Match> FindMatches(Board board)
    {
        var matches = new List<Match>();
        FindRuns(board, horizontal: true, matches);
        FindRuns(board, horizontal: false, matches);
        return matches;
    }

    private static void FindRuns(Board board, bool horizontal, List<Match> outList)
    {
        var outer = horizontal ? board.Height : board.Width;
        var inner = horizontal ? board.Width : board.Height;
        for (var line = 0; line < outer; line++)
        {
            var start = 0;
            while (start < inner)
            {
                var first = GemAt(board, line, start, horizontal);
                if (first is null)
                {
                    start++;
                    continue;
                }

                // Hypercubes are colorless and can never be part of a run.
                if (ContainsHypercube(first))
                {
                    start++;
                    continue;
                }

                var end = start + 1;
                while (end < inner &&
                       !ContainsHypercube(GemAt(board, line, end, horizontal)) &&
                       GemAt(board, line, end, horizontal)?.Type == first.Value.Type)
                {
                    end++;
                }

                if (end - start >= 3)
                {
                    var positions = new List<Position>(end - start);
                    for (var index = start; index < end; index++)
                    {
                        positions.Add(horizontal ? new Position(line, index) : new Position(index, line));
                    }
                    outList.Add(new Match(positions));
                }

                start = end;
            }
        }
    }

    private static bool ContainsHypercube(Gem? gem) => gem?.Special == Special.Hypercube;

    private static Gem? GemAt(Board board, int line, int index, bool horizontal) =>
        horizontal ? board.GemAt(line, index) : board.GemAt(index, line);
}