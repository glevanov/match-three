using MatchThree.Engine.Model;
using MatchThree.Engine.Rules;
using NUnit.Framework;

namespace MatchThree.Engine.Tests;

public class ScorerTest
{
    [Test]
    public void BaseRateIs10PointsPerUniqueGemAtDepth1()
    {
        Assert.That(Scorer.RoundScore(new[] { Pos(0, 0), Pos(0, 1), Pos(0, 2) }, 1), Is.EqualTo(30));
        Assert.That(Scorer.RoundScore(
            new[] { Pos(1, 0), Pos(1, 1), Pos(1, 2), Pos(2, 2), Pos(3, 2) }, 1), Is.EqualTo(50));
    }

    [Test]
    public void CascadeMultiplierScalesLinearlyAndUncappedByDepth()
    {
        var cells = new[] { Pos(0, 0), Pos(0, 1), Pos(0, 2) };
        Assert.That(Scorer.RoundScore(cells, 1), Is.EqualTo(30));
        Assert.That(Scorer.RoundScore(cells, 2), Is.EqualTo(60));
        Assert.That(Scorer.RoundScore(cells, 3), Is.EqualTo(90));
        Assert.That(Scorer.RoundScore(cells, 10), Is.EqualTo(300));
    }

    [Test]
    public void OverlappingClearRegionsAreCountedOnce()
    {
        var rowClear = new[] { Pos(2, 0), Pos(2, 1), Pos(2, 2) };
        var columnClear = new[] { Pos(0, 2), Pos(1, 2), Pos(2, 2) };
        var combined = rowClear.Concat(columnClear).ToList();
        Assert.That(combined.Distinct().ToList(), Has.Count.EqualTo(5));
        Assert.That(Scorer.RoundScore(combined, 1), Is.EqualTo(50));
    }

    [Test]
    public void DuplicatesPassedToASingleRoundAreDeduped()
    {
        Assert.That(Scorer.RoundScore(new[] { Pos(0, 0), Pos(0, 1), Pos(0, 0) }, 1), Is.EqualTo(20));
    }

    [Test]
    public void TotalScoreSumsEveryRound()
    {
        Assert.That(Scorer.TotalScore(Array.Empty<int>()), Is.EqualTo(0));
        Assert.That(Scorer.TotalScore(new[] { 30 }), Is.EqualTo(30));
        Assert.That(Scorer.TotalScore(new[] { 30, 60, 90 }), Is.EqualTo(30 + 60 + 90));
    }

    [Test]
    public void DepthBelow1IsRejected()
    {
        Assert.Throws<ArgumentException>(() => Scorer.RoundScore(Array.Empty<Position>(), 0));
    }

    private static Position Pos(int row, int col) => new(row, col);
}