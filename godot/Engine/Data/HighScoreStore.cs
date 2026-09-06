using System.Text.Json;

namespace MatchThree.Engine.Data;

/// <summary>High scores per mode as persisted by <see cref="HighScoreStore"/>.</summary>
public sealed record HighScores(int Classic = 0, int Zen = 0)
{
    public int ForMode(GameMode mode) => mode == GameMode.Classic ? Classic : Zen;
}

/// <summary>
/// Persistent high scores as a JSON file (M5; Kotlin used DataStore
/// Preferences). Only the max score per mode is saved (MECHANICS.md non-goal:
/// no mid-session persistence).
///
/// Pure C# on purpose (precedent: SwapIntent): the save directory is injected,
/// so the logic runs under `dotnet test` — the Godot side constructs it with
/// `ProjectSettings.GlobalizePath("user://")`.
/// </summary>
public sealed class HighScoreStore
{
    private readonly string _filePath;

    /// <param name="saveDirectory">Directory holding highscores.json (e.g. user:// globalized).</param>
    public HighScoreStore(string saveDirectory)
    {
        _filePath = Path.Combine(saveDirectory, "highscores.json");
    }

    /// <summary>Current high scores; missing/corrupt files read as zeros.</summary>
    public HighScores Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return new HighScores();
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<HighScores>(json) ?? new HighScores();
        }
        catch (Exception)
        {
            return new HighScores();
        }
    }

    /// <summary>
    /// Persists <paramref name="score"/> for <paramref name="mode"/> when it
    /// beats the stored high score. Returns true when a new high score was
    /// recorded.
    /// </summary>
    public bool SaveIfBeats(GameMode mode, int score)
    {
        if (score <= 0) return false;

        var current = Load();
        if (score <= current.ForMode(mode)) return false;

        var updated = mode == GameMode.Classic
            ? current with { Classic = score }
            : current with { Zen = score };

        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(updated));
        return true;
    }
}