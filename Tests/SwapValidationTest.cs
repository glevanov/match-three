using MatchThree.Engine.Model;
using MatchThree.Engine.Rules;
using NUnit.Framework;

namespace MatchThree.Engine.Tests;

public class SwapValidationTest
{
    private readonly GameEngine engine = new(new SeededRandom(1L));

    [Test]
    public void SwapCompletingAHorizontalRunIsLegal()
    {
        var board = Boards.FromRows(
            "RGR",
            "BRB",
            "RYR");
        Assert.That(engine.IsLegalSwap(board, new Position(0, 1), new Position(1, 1)), Is.True);
    }

    [Test]
    public void SwapThatCreatesNothingIsIllegal()
    {
        var board = Boards.FromRows(
            "RGR",
            "BPP",
            "RYR");
        Assert.That(engine.IsLegalSwap(board, new Position(0, 1), new Position(1, 1)), Is.False);
    }

    [Test]
    public void NonAdjacentSwapIsIllegal()
    {
        var board = Boards.FromRows("RYR", "BGP", "RYR");
        Assert.That(engine.IsLegalSwap(board, new Position(0, 0), new Position(2, 2)), Is.False);
    }

    [Test]
    public void PositionOutsideBoardIsIllegal()
    {
        var board = Boards.FromRows("RYR", "BGP", "RYR");
        Assert.That(engine.IsLegalSwap(board, new Position(3, 0), new Position(2, 0)), Is.False);
        Assert.That(engine.IsLegalSwap(board, new Position(-1, 0), new Position(0, 0)), Is.False);
    }

    [Test]
    public void SwappingAnEmptyCellIsIllegal()
    {
        var board = Boards.FromRows(
            ".YR",
            "BGP",
            "RYR");
        Assert.That(engine.IsLegalSwap(board, new Position(0, 0), new Position(1, 0)), Is.False);
    }

    [Test]
    public void SwapCompletingAVerticalRunIsLegal()
    {
        var board = Boards.FromRows(
            "RPG",
            "BGP",
            "RGP");
        Assert.That(engine.IsLegalSwap(board, new Position(0, 1), new Position(0, 2)), Is.True);
    }

    [Test]
    public void FixtureBoardLegalMoveCountMatchesBruteForceScan()
    {
        var board = Boards.FromRows(
            "RGR",
            "BRB",
            "RYR");
        Assert.That(LegalMoveDetector.LegalMoveCount(board), Is.EqualTo(4));
        var bruteForce = CountLegalSwapsByBruteForce(board);
        Assert.That(LegalMoveDetector.LegalMoveCount(board), Is.EqualTo(bruteForce));
    }

    private static int CountLegalSwapsByBruteForce(Board board)
    {
        var count = 0;
        for (var row = 0; row < board.Height; row++)
        {
            for (var col = 0; col < board.Width; col++)
            {
                var a = new Position(row, col);
                if (board.GemAt(a) is null) continue;
                foreach (var b in new[] { new Position(row, col + 1), new Position(row + 1, col) })
                {
                    if (board.IsInside(b) && board.GemAt(b) is not null)
                    {
                        if (MatchDetector.FindMatches(board.WithSwapped(a, b)).Count > 0) count++;
                    }
                }
            }
        }
        return count;
    }
}