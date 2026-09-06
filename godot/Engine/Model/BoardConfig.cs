namespace MatchThree.Engine.Model;

/// <summary>
/// Board size and gem-color count. Tuned constants per MECHANICS.md;
/// the generator and engine derive everything from these values.
/// </summary>
/// <param name="Width">Board width in cells.</param>
/// <param name="Height">Board height in cells.</param>
/// <param name="GemTypeCount">Number of gem colors in the pool.</param>
public sealed record BoardConfig(
    int Width = 9,
    int Height = 9,
    int GemTypeCount = 6)
{
    // Defaults above are literals because a primary-constructor parameter list
    // is evaluated in the enclosing scope and cannot reference these consts.
    public const int DEFAULT_WIDTH = 9;
    public const int DEFAULT_HEIGHT = 9;
    public const int DEFAULT_GEM_TYPES = 6;
}