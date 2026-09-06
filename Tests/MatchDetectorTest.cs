using MatchThree.Engine.Model;
using MatchThree.Engine.Rules;
using NUnit.Framework;

namespace MatchThree.Engine.Tests;

public class MatchDetectorTest
{
    [Test]
    public void HorizontalRunOf3IsFound()
    {
        var board = Boards.FromRows("RRR");
        var matches = MatchDetector.FindMatches(board);
        Assert.That(matches, Has.Count.EqualTo(1));
        Assert.That(matches[0].Positions, Is.EqualTo(new[]
        {
            new Position(0, 0), new Position(0, 1), new Position(0, 2),
        }));
    }

    [Test]
    public void RunOf4IsASingleMaximalMatch()
    {
        var board = Boards.FromRows("RRRR");
        var matches = MatchDetector.FindMatches(board);
        Assert.That(matches, Has.Count.EqualTo(1));
        Assert.That(matches[0].Size, Is.EqualTo(4));
        Assert.That(matches[0].Positions, Is.EqualTo(new[]
        {
            new Position(0, 0), new Position(0, 1), new Position(0, 2), new Position(0, 3),
        }));
    }

    [Test]
    public void VerticalRunOf3IsFound()
    {
        var board = Boards.FromRows("R..", "R..", "R..");
        var matches = MatchDetector.FindMatches(board);
        Assert.That(matches, Has.Count.EqualTo(1));
        Assert.That(matches[0].Positions, Is.EqualTo(new[]
        {
            new Position(0, 0), new Position(1, 0), new Position(2, 0),
        }));
    }

    [Test]
    public void LShapeYieldsHorizontalAndVerticalMatches()
    {
        var board = Boards.FromRows(
            "RRR",
            "..R",
            "..R");
        var matches = MatchDetector.FindMatches(board);
        Assert.That(matches, Has.Count.EqualTo(2));
        Assert.That(matches.Any(m => m.Positions.All(p => p.Row == 0)), Is.True);
        Assert.That(matches.Any(m => m.Positions.All(p => p.Col == 2)), Is.True);
    }

    [Test]
    public void TwoSeparateRunsInOneRowAreBothFound()
    {
        var board = Boards.FromRows("RRRBBRRR");
        var matches = MatchDetector.FindMatches(board);
        Assert.That(matches, Has.Count.EqualTo(2));
        Assert.That(matches[0].Size, Is.EqualTo(3));
        Assert.That(matches[1].Size, Is.EqualTo(3));
    }

    [Test]
    public void AlternatingBoardHasNoMatches()
    {
        var board = Boards.FromRows(
            "RYRYRYRYR",
            "YRYRYRYRY",
            "RYRYRYRYR");
        Assert.That(MatchDetector.FindMatches(board), Is.Empty);
    }

    [Test]
    public void EmptyCellsBreakRuns()
    {
        var board = Boards.FromRows("RR.RR");
        Assert.That(MatchDetector.FindMatches(board), Is.Empty);
    }
}