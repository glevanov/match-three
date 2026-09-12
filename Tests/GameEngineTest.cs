using MatchThree.Engine.Model;
using MatchThree.Engine.Rules;
using NUnit.Framework;

namespace MatchThree.Engine.Tests;

public class GameEngineTest
{
    private static GameEngine Engine() => new(new SeededRandom(1L));

    private static Board CascadeFixture() => Boards.FromRows(
        "RYRYRYRYR",
        "YRYRYRYRY",
        "RYRYRYRYR",
        "YRYRYRRYR",
        "RYBARYYRY",
        "YRABARYRY",
        "RYBRYYRYR",
        "YRBGYRYRY",
        "RYRYRYYRY");

    [Test]
    public void IllegalSwapReturnsNullAndNoSteps()
    {
        var engine = Engine();
        var board = CascadeFixture();
        var swapped = engine.ResolveSwap(board, new Position(0, 0), new Position(2, 2));
        Assert.That(swapped, Is.Null);
    }

    [Test]
    public void SwapResolutionEmitsOrderedStepsAndSettles()
    {
        var engine = Engine();
        var board = CascadeFixture();

        var resolution = engine.ResolveSwap(board, new Position(5, 3), new Position(4, 3));
        Assert.That(resolution, Is.Not.Null);

        var steps = resolution!.Steps;
        Assert.That(steps[0], Is.EqualTo(new Step.Swap(new Position(5, 3), new Position(4, 3))));

        var firstDestroy = (Step.Destroy)steps[1];
        Assert.That(firstDestroy.Positions, Is.EquivalentTo(new[]
        {
            new Position(5, 2), new Position(5, 3), new Position(5, 4),
        }));

        var destroyCount = steps.OfType<Step.Destroy>().Count();
        Assert.That(destroyCount, Is.GreaterThanOrEqualTo(2), $"expected cascade, got {destroyCount} destroys");

        Assert.That(steps.OfType<Step.Fall>(), Is.Not.Empty);

        var settled = (Step.Settled)steps[^1];
        Assert.That(MatchDetector.FindMatches(settled.Board), Is.Empty);

        var kinds = steps.Select(s => s.GetType().Name).ToList();
        Assert.That(kinds[^1], Is.EqualTo("Settled"));
    }

    [Test]
    public void NewGameBoardIsFullyPopulated()
    {
        var engine = Engine();
        var board = engine.NewGame();
        Assert.That(board.Positions().Count(p => board.GemAt(p) is not null), Is.EqualTo(81));
        Assert.That(MatchDetector.FindMatches(board), Is.Empty);
        Assert.That(LegalMoveDetector.HasLegalMove(board), Is.True);
    }

    [Test]
    public void SpawnedGemsGetFreshUniqueIds()
    {
        var engine = Engine();
        var sessionBoard = engine.NewGame();
        var sessionIds = sessionBoard.Positions().Select(p => sessionBoard.GemAt(p)).Where(g => g is not null)
            .Select(g => g!.Value.Id).ToHashSet();
        var fixture = CascadeFixture();
        var fixtureIds = fixture.Positions().Select(p => fixture.GemAt(p)).Where(g => g is not null)
            .Select(g => g!.Value.Id).ToHashSet();

        var resolution = engine.ResolveSwap(fixture, new Position(5, 3), new Position(4, 3))!;
        var spawnedIds = resolution.Steps
            .OfType<Step.Spawn>()
            .SelectMany(s => s.Gems.Select(g => g.Gem.Id))
            .ToList();

        Assert.That(spawnedIds, Is.Not.Empty);
        Assert.That(spawnedIds.All(id => !fixtureIds.Contains(id)), Is.True);
        Assert.That(spawnedIds.All(id => !sessionIds.Contains(id)), Is.True);
        Assert.That(spawnedIds.Distinct().ToList(), Has.Count.EqualTo(spawnedIds.Count));
    }

    [Test]
    public void EachCascadeRoundEmitsAScoreStepWithUniqueCellDeltaAndDepth()
    {
        var engine = Engine();
        var resolution = engine.ResolveSwap(CascadeFixture(), new Position(5, 3), new Position(4, 3))!;

        var destroys = resolution.Steps.OfType<Step.Destroy>().ToList();
        var scores = resolution.Steps.OfType<Step.Score>().ToList();
        Assert.That(destroys, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(scores, Has.Count.EqualTo(destroys.Count));

        for (var index = 0; index < scores.Count; index++)
        {
            var destroyed = destroys[index].Positions;
            Assert.That(
                scores[index].Delta,
                Is.EqualTo(destroyed.Count * Scorer.BASE_POINTS_PER_GEM * (index + 1)),
                $"round {index + 1}: delta = uniqueCells * 10 * depth");
            Assert.That(scores[index].CascadeDepth, Is.EqualTo(index + 1));
        }

        Assert.That(scores.Sum(s => s.Delta), Is.EqualTo(Scorer.TotalScore(scores.Select(s => s.Delta))));
    }

    [Test]
    public void ScoreStepsAlternateWithDestroyStepsInCascadeOrder()
    {
        var engine = Engine();
        var resolution = engine.ResolveSwap(CascadeFixture(), new Position(5, 3), new Position(4, 3))!;

        var paired = resolution.Steps.Where(s => s is Step.Destroy or Step.Score).ToList();
        Assert.That(paired, Has.Count.GreaterThanOrEqualTo(4));
        for (var index = 0; index < paired.Count; index++)
        {
            var expected = index % 2 == 0 ? "Destroy" : "Score";
            Assert.That(paired[index].GetType().Name, Is.EqualTo(expected));
        }
    }

    [Test]
    public void ReshufflePreservesTheGemMultisetAndRestoresLegalMoves()
    {
        var engine = Engine();
        var board = Boards.FromRows(
            "RYRYRY",
            "GBGBGB",
            "YOYOYP");

        var reshuffled = engine.Reshuffle(board);
        Assert.That(reshuffled, Is.Not.Null, "reshuffle should fix a small board");

        var originalIds = board.Positions().Select(p => board.GemAt(p)).Where(g => g is not null)
            .Select(g => g!.Value.Id).OrderBy(x => x).ToList();
        var shuffledIds = reshuffled!.Positions().Select(p => reshuffled.GemAt(p)).Where(g => g is not null)
            .Select(g => g!.Value.Id).OrderBy(x => x).ToList();
        Assert.That(shuffledIds, Is.EqualTo(originalIds), "same gem multiset (ids preserved)");

        Assert.That(MatchDetector.FindMatches(reshuffled), Is.Empty);
        Assert.That(LegalMoveDetector.HasLegalMove(reshuffled), Is.True);
    }

    [Test]
    public void ReshuffleReturnsNullWhenTheBoardCannotBeFixed()
    {
        var engine = Engine();
        var dead = Board.Create(2, 2, pos => new Gem(pos.Row * 2 + pos.Col, GemTypes.FromIndex(0)));
        Assert.That(engine.Reshuffle(dead), Is.Null);
    }
}