using MatchThree.Engine.Model;
using MatchThree.Engine.Rules;
using NUnit.Framework;

namespace MatchThree.Engine.Tests;

public class BoardGeneratorTest
{
    [Test]
    public void GeneratedBoardsHaveNoPreExistingMatchesAndAtLeastOneLegalMove()
    {
        var config = new BoardConfig();
        for (var i = 0; i < 50; i++)
        {
            var rng = new SeededRandom(10_000L + i);
            var board = new GameEngine(config, rng).NewGame();
            var matches = MatchDetector.FindMatches(board);
            Assert.That(matches, Is.Empty, $"board {i} has unexpected matches: {string.Join(", ", matches)}");
            Assert.That(LegalMoveDetector.HasLegalMove(board), Is.True, $"board {i} has no legal move");
        }
    }

    [Test]
    public void GeneratedBoardsUseUniqueGemIds()
    {
        var config = new BoardConfig();
        var engine = new GameEngine(config, new SeededRandom(77L));
        for (var i = 0; i < 10; i++)
        {
            var board = engine.NewGame();
            var ids = board.Positions().Select(p => board.GemAt(p)).Where(g => g is not null)
                .Select(g => g!.Value.Id).ToList();
            Assert.That(ids, Has.Count.EqualTo(config.Width * config.Height));
            Assert.That(ids.Distinct().ToList(), Has.Count.EqualTo(ids.Count));
        }
    }

    [Test]
    public void OverallBoardSizeRespectsConfig()
    {
        var engine = new GameEngine(new BoardConfig(), new SeededRandom(5L));
        var board = engine.NewGame();
        Assert.That(board.Width, Is.EqualTo(9));
        Assert.That(board.Height, Is.EqualTo(9));
    }
}