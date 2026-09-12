using MatchThree.Engine.Model;
using MatchThree.Engine.Rules;
using NUnit.Framework;

namespace MatchThree.Engine.Tests;

public class BoardSimulationTest
{
    private readonly BoardConfig config = new();

    [Test]
    public void ReportSimulationMetrics()
    {
        const int boardCount = 150;
        long totalLegalMoves = 0;
        long totalAdjacentPairs = 0;
        long totalLegalSwaps = 0;

        for (var i = 0; i < boardCount; i++)
        {
            var engine = new GameEngine(config, new SeededRandom(1000L + i));
            var board = engine.NewGame();
            var legalCount = LegalMoveDetector.LegalMoveCount(board);
            totalLegalMoves += legalCount;

            for (var row = 0; row < config.Height; row++)
            {
                for (var col = 0; col < config.Width; col++)
                {
                    var a = new Position(row, col);
                    if (board.GemAt(a) is null) continue;
                    foreach (var b in Neighbours(row, col))
                    {
                        if (board.IsInside(b) && board.GemAt(b) is not null)
                        {
                            totalAdjacentPairs++;
                            if (engine.IsLegalSwap(board, a, b)) totalLegalSwaps++;
                        }
                    }
                }
            }
        }

        var avgLegalMoves = (double)totalLegalMoves / boardCount;
        var swapMatchProbability = (double)totalLegalSwaps / totalAdjacentPairs;

        TestContext.Progress.WriteLine(
            $"SIMULATION 9x9/6 over {boardCount} boards -> " +
            $"avg legal moves per board: {avgLegalMoves:F4}, " +
            $"random-swap match probability: {swapMatchProbability:F4}");
        TestContext.WriteLine(
            $"SIMULATION 9x9/6 over {boardCount} boards -> " +
            $"avg legal moves per board: {avgLegalMoves:F4}, " +
            $"random-swap match probability: {swapMatchProbability:F4}");

        Assert.That(avgLegalMoves, Is.GreaterThan(1.0));
        Assert.That(avgLegalMoves, Is.LessThan(80.0));
        Assert.That(swapMatchProbability, Is.GreaterThan(0.0));
        Assert.That(swapMatchProbability, Is.LessThan(0.5));
    }

    private static List<Position> Neighbours(int row, int col) => new()
    {
        new Position(row, col + 1),
        new Position(row + 1, col),
    };
}