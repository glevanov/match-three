namespace MatchThree.Engine.Model;

/// <summary>
/// The six gem colors. Count must match <see cref="BoardConfig.GemTypeCount"/>.
/// </summary>
public enum GemType
{
    Red,
    Green,
    Blue,
    Yellow,
    Purple,
    Orange,
}

/// <summary>Static helpers for <see cref="GemType"/>.</summary>
public static class GemTypes
{
    /// <summary>Index-based lookup in declaration order.</summary>
    public static GemType FromIndex(int index) => (GemType)index;
}