using MatchThree.Engine.Model;
using MatchThree.Engine.Rules;
using NUnit.Framework;

namespace MatchThree.Engine.Tests;

public class RefillTest
{
    private readonly IdSource idSource = new(100);

    [Test]
    public void RefillSpawnsGemsOnlyInTopGaps()
    {
        var board = Boards.FromRows(
            "..G",
            "..B",
            ".RY");
        var result = Refill.Fill(board, 6, idSource.Next, _ => GemType.Red);

        Assert.That(result.Spawned, Has.Count.EqualTo(5));
        Assert.That(result.Spawned.Select(s => s.Position), Is.EquivalentTo(new[]
        {
            new Position(0, 0), new Position(1, 0), new Position(2, 0),
            new Position(0, 1), new Position(1, 1),
        }));

        Assert.That(result.Spawned.Select(s => s.Gem.Id).Distinct().ToList(), Has.Count.EqualTo(5));
        Assert.That(idSource.Next(), Is.EqualTo(105));

        Assert.That(result.Board.GemAt(0, 2)?.Type, Is.EqualTo(GemType.Green));
        Assert.That(result.Board.GemAt(1, 2)?.Type, Is.EqualTo(GemType.Blue));
        Assert.That(result.Board.GemAt(2, 2)?.Type, Is.EqualTo(GemType.Yellow));
    }

    [Test]
    public void FullBoardNeedsNoSpawns()
    {
        var board = Boards.FromRows("RYR", "BYB");
        var result = Refill.Fill(board, 6, idSource.Next, _ => GemType.Red);
        Assert.That(result.Spawned, Is.Empty);
    }
}