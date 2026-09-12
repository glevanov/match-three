namespace MatchThree.Engine.Model;

public enum GemType
{
    Red,
    Green,
    Blue,
    Yellow,
    Purple,
    Orange,
}

public static class GemTypes
{
    public static GemType FromIndex(int index) => (GemType)index;
}