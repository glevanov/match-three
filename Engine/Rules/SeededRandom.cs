namespace MatchThree.Engine.Rules;

/// <summary>
/// Deterministic pseudo-randomness for the whole engine (AGENTS.md: seeded RNG).
/// Same seed reproduces the exact same game, which keeps tests and replays stable.
/// </summary>
/// <remarks>
/// Wraps <see cref="System.Random"/> with an explicit seed, per GODOT_PORT_PLAN §4.
/// Sequences are deterministic within this port (not bit-identical to Kotlin's
/// kotlin.random.Random — no cross-language replay is required).
/// </remarks>
public sealed class SeededRandom
{
    private readonly Random random;

    public SeededRandom(long seed)
    {
        // Fold the full 64-bit seed into 32 bits instead of truncating the
        // high bits away, so seeds don't repeat on a ~7.2-minute cycle when
        // driven by DateTime.Ticks.
        random = new Random((int)(seed ^ (seed >> 32)));
    }

    /// <summary>Random integer in <c>[0, bound)</c>.</summary>
    public int NextInt(int bound) => random.Next(bound);

    /// <summary>Random integer in <c>[min, max]</c> inclusive.</summary>
    public int NextInt(int min, int max) => random.Next(min, max + 1);

    public long NextLong() => random.NextInt64();
}