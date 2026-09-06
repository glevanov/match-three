namespace MatchThree.Engine.Model;

/// <summary>
/// Special gem types (MECHANICS.md, M4).
/// </summary>
public enum Special
{
    /// <summary>Born from a 4-in-row; explodes a 3x3 area around itself when cleared or activated.</summary>
    Flame,

    /// <summary>Born from a T/L shape; clears its full row and column.</summary>
    Star,

    /// <summary>
    /// Born from a 5-in-row; <b>colorless</b> — it never participates in a match
    /// (see MatchDetector) and its effect depends on the gem it is swapped with
    /// (clears every gem of that swapped color).
    /// </summary>
    Hypercube,
}