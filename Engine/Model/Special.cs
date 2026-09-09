namespace MatchThree.Engine.Model;

/// <summary>
/// Special gem types (MECHANICS.md).
/// </summary>
public enum Special
{
    /// <summary>Born from a 4-in-row; explodes a 3x3 area around itself when cleared or activated.</summary>
    Flame,

    /// <summary>Born from a T/L shape; clears its full row and column when cleared or activated.</summary>
    Star,

    /// <summary>
    /// Born from a 5-in-row; <b>colorless</b> — it never participates in a match
    /// (see MatchDetector). Its effect depends on what triggered it: a direct
    /// swap uses the partner gem, while a Flame/Star chain-trigger uses that
    /// detonator's color.
    /// </summary>
    Hypercube,
}