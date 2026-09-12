namespace MatchThree.Engine.Rules;

public sealed class SeededRandom
{
    private readonly Random random;

    public SeededRandom(long seed)
    {
        random = new Random((int)(seed ^ (seed >> 32)));
    }

    public int NextInt(int bound) => random.Next(bound);

    public int NextInt(int min, int max) => random.Next(min, max + 1);

    public long NextLong() => random.NextInt64();
}