using MatchThree.Engine.Model;
using MatchThree.Engine.Rules;
using NUnit.Framework;

namespace MatchThree.Engine.Tests;

public class BufferedSwapGuardTest
{
    private int nextId;

    private Gem Gem(GemType type = GemType.Red, Special? special = null) => new(nextId++, type, special);

    [Test]
    public void PlainPairWhoseGemsChangedEntirelyIsNotStale()
    {
        var submitA = Gem();
        var submitB = Gem();
        Assert.That(BufferedSwapGuard.BufferedSwapIsStale(submitA, submitB, Gem(), Gem()), Is.False);
    }

    [Test]
    public void HypercubeStillInThePairAtSettleIsNotStale()
    {
        var hyper = Gem(GemType.Blue, Special.Hypercube);
        var plain = Gem();
        Assert.That(BufferedSwapGuard.BufferedSwapIsStale(hyper, plain, hyper, plain), Is.False);
    }

    [Test]
    public void HypercubeBornOrFallenIntoThePairIsStale()
    {
        var plainA = Gem();
        var plainB = Gem();
        var settledHyper = Gem(GemType.Blue, Special.Hypercube);
        Assert.That(BufferedSwapGuard.BufferedSwapIsStale(plainA, plainB, settledHyper, plainB), Is.True);
        Assert.That(BufferedSwapGuard.BufferedSwapIsStale(plainA, plainB, plainA, settledHyper), Is.True);
    }

    [Test]
    public void HypercubeThatLeftThePairIsStale()
    {
        var hyper = Gem(GemType.Blue, Special.Hypercube);
        var plain = Gem();
        Assert.That(BufferedSwapGuard.BufferedSwapIsStale(hyper, plain, Gem(), Gem()), Is.True);
    }

    [Test]
    public void HypercubeFallingWithinThePairKeepsTheSwapFresh()
    {
        var hyper = Gem(GemType.Blue, Special.Hypercube);
        var plain = Gem();
        Assert.That(BufferedSwapGuard.BufferedSwapIsStale(hyper, plain, plain, hyper), Is.False);
    }

    [Test]
    public void ASecondHypercubeEnteringThePairIsStale()
    {
        var hyper = Gem(GemType.Blue, Special.Hypercube);
        var plain = Gem();
        var secondHyper = Gem(GemType.Green, Special.Hypercube);
        Assert.That(BufferedSwapGuard.BufferedSwapIsStale(hyper, plain, hyper, secondHyper), Is.True);
    }

    [Test]
    public void EmptyCellsCountAsNoHypercubeAndNeverCrash()
    {
        var hyper = Gem(GemType.Blue, Special.Hypercube);
        Assert.That(BufferedSwapGuard.BufferedSwapIsStale(null, null, null, null), Is.False);
        Assert.That(BufferedSwapGuard.BufferedSwapIsStale(null, null, hyper, null), Is.True);
        Assert.That(BufferedSwapGuard.BufferedSwapIsStale(hyper, null, null, null), Is.True);
    }
}