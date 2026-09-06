namespace MatchThree.Engine.Rules;

/// <summary>
/// Hands out strictly increasing gem ids for the current session so that no two
/// gems ever share an id (AGENTS.md: stable ids). Created once per game engine.
/// </summary>
public sealed class IdSource
{
    private int next;

    public IdSource(int next = 0)
    {
        this.next = next;
    }

    /// <summary>The next id; each call returns a fresh, strictly larger value.</summary>
    public int Next() => next++;
}