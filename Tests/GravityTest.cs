using MatchThree.Engine.Model;
using MatchThree.Engine.Rules;
using NUnit.Framework;

namespace MatchThree.Engine.Tests;

public class GravityTest
{
    [Test]
    public void GemsFallToFillClearedBottomRow()
    {
        var board = Boards.FromRows("RYR", "BYB", "GYG");
        var result = Gravity.Apply(
            board,
            new HashSet<Position> { new(2, 0), new(2, 1), new(2, 2) });

        // All six survivors drop by one: three row-1 gems to the bottom, three row-0 gems to the middle.
        Assert.That(result.Falls, Has.Count.EqualTo(6));
        Assert.That(result.Falls.Select(f => f.GemId).OrderBy(x => x), Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
        Assert.That(CharRow(result, 2), Is.EqualTo("BYB")); // row-1 gems now sit on the bottom
        Assert.That(CharRow(result, 1), Is.EqualTo("RYR")); // row-0 gems moved down one
        Assert.That(result.Board.GemAt(0, 0), Is.Null);     // top row is free for refill
        Assert.That(result.Board.GemAt(0, 1), Is.Null);
        Assert.That(result.Board.GemAt(0, 2), Is.Null);
    }

    [Test]
    public void GapsInsideAColumnCollapsePreservingOrder()
    {
        // One column: O(0) B(1) Y(2) G(3) R(4); destroy B(row1) and G(row3).
        var board = Boards.FromRows(
            "O",
            "B",
            "Y",
            "G",
            "R");
        var result = Gravity.Apply(board, new HashSet<Position> { new(1, 0), new(3, 0) });

        // Y(id2) moves 2->3, O(id0) moves 0->2.
        Assert.That(result.Falls.Select(f => f.GemId), Is.EquivalentTo(new[] { 0, 2 }));
        Assert.That(CharAt(result, 2), Is.EqualTo("O")); // O dropped from row0 to row2
        Assert.That(CharAt(result, 3), Is.EqualTo("Y")); // Y dropped from row2 to row3
        Assert.That(CharAt(result, 4), Is.EqualTo("R")); // R stays put
        Assert.That(result.Board.GemAt(0, 0), Is.Null);
        Assert.That(result.Board.GemAt(1, 0), Is.Null);
    }

    [Test]
    public void DestroyingAWholeColumnProducesNoFallsInIt()
    {
        var board = Boards.FromRows("O", "B", "Y");
        var result = Gravity.Apply(board, new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0) });
        Assert.That(result.Falls, Is.Empty);
        Assert.That(result.Board.GemAt(0, 0), Is.Null);
    }

    private static string CharRow(Gravity.Result result, int row) =>
        string.Concat(Enumerable.Range(0, result.Board.Width).Select(col => CharAt(result, row, col)));

    private static string CharAt(Gravity.Result result, int row, int col = 0)
    {
        var gem = result.Board.GemAt(row, col);
        return gem is { } g ? Boards.TypeToChar(g.Type).ToString() : ".";
    }
}