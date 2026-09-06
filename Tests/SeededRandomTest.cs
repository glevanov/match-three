using MatchThree.Engine.Model;
using MatchThree.Engine.Rules;
using NUnit.Framework;

namespace MatchThree.Engine.Tests;

public class SeededRandomTest
{
    [Test]
    public void SameSeedReproducesSameSequence()
    {
        var a = Seqs(new SeededRandom(42L), 100);
        var b = Seqs(new SeededRandom(42L), 100);
        Assert.That(a, Is.EqualTo(b));
    }

    [Test]
    public void DifferentSeedProducesDifferentSequence()
    {
        var a = Seqs(new SeededRandom(1L), 50);
        var b = Seqs(new SeededRandom(2L), 50);
        Assert.That(a, Is.Not.EqualTo(b));
    }

    [Test]
    public void NextIntRespectsBounds()
    {
        var rng = new SeededRandom(7L);
        for (var i = 0; i < 1_000; i++)
        {
            var value = rng.NextInt(6);
            Assert.That(value, Is.InRange(0, 5));
        }
    }

    [Test]
    public void DifferentSeedsWithSameLower32BitsProduceDifferentSequences()
    {
        // Two ticks values 2^32 apart previously collapsed to the same int seed.
        long seedA = 1_000_000L;
        long seedB = seedA + (1L << 32);

        var rngA = new SeededRandom(seedA);
        var rngB = new SeededRandom(seedB);

        var sequenceA = Enumerable.Range(0, 20).Select(_ => rngA.NextInt(1000)).ToList();
        var sequenceB = Enumerable.Range(0, 20).Select(_ => rngB.NextInt(1000)).ToList();

        Assert.That(sequenceA, Is.Not.EqualTo(sequenceB));
    }

    private static List<int> Seqs(SeededRandom r, int count)
    {
        var result = new List<int>(count);
        for (var i = 0; i < count; i++) result.Add(r.NextInt(6));
        return result;
    }
}