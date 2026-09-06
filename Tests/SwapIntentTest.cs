using MatchThree.Engine.Model;
using MatchThree.Engine.Rules;
using NUnit.Framework;

namespace MatchThree.Engine.Tests;

public class SwapIntentTest
{
    [Test]
    public void HorizontalSwapNormalizesToLeftCellFirst()
    {
        var intent = SwapIntent.Of(new Position(3, 5), new Position(3, 6));
        Assert.That(intent.A, Is.EqualTo(new Position(3, 5)));
        Assert.That(intent.B, Is.EqualTo(new Position(3, 6)));
    }

    [Test]
    public void ReversedHorizontalSwapYieldsIdenticalIntent()
    {
        Assert.That(
            SwapIntent.Of(new Position(3, 5), new Position(3, 6)),
            Is.EqualTo(SwapIntent.Of(new Position(3, 6), new Position(3, 5))));
    }

    [Test]
    public void VerticalSwapNormalizesToUpperCellFirst()
    {
        var intent = SwapIntent.Of(new Position(8, 2), new Position(7, 2));
        Assert.That(intent.A, Is.EqualTo(new Position(7, 2)));
        Assert.That(intent.B, Is.EqualTo(new Position(8, 2)));
    }

    [Test]
    public void NonAdjacentCellsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => SwapIntent.Of(new Position(0, 0), new Position(2, 2)));
    }
}