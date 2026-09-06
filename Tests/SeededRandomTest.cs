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

    private static List<int> Seqs(SeededRandom r, int count)
    {
        var result = new List<int>(count);
        for (var i = 0; i < count; i++) result.Add(r.NextInt(6));
        return result;
    }
}