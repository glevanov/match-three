namespace MatchThree.Engine.Model;

public sealed record BoardConfig(
    int Width = 9,
    int Height = 9,
    int GemTypeCount = 6)
{
    public const int DEFAULT_WIDTH = 9;
    public const int DEFAULT_HEIGHT = 9;
    public const int DEFAULT_GEM_TYPES = 6;
}