namespace MatchThree.Engine.Data;

/// <summary>Game mode chosen on the menu screen (Kotlin ui.GameMode).</summary>
public enum GameMode
{
    /// <summary>75s countdown round (MECHANICS.md).</summary>
    Classic,

    /// <summary>Endless; ends only on a dead board whose reshuffle failed.</summary>
    Zen,
}