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

/// <summary>Companion helpers, mirroring Kotlin's <c>GemType.Companion</c>.</summary>
public static class GemTypes
{
    /// <summary>Index-based lookup matching Kotlin's <c>entries[index]</c> (declaration order).</summary>
    public static GemType FromIndex(int index) => (GemType)index;
}