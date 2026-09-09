using System.Text.Json;

namespace MatchThree.Engine.Data;

/// <summary>User settings as persisted by <see cref="SettingsStore"/>.</summary>
/// <param name="MusicEnabled">Background-music toggle (HUD button); true by default.</param>
public sealed record GameSettings(bool MusicEnabled = true);

/// <summary>
/// Persistent user settings as a JSON file (user://settings.json): the
/// background-music toggle lives here so it survives scene changes and app
/// restarts.
///
/// Pure C# on purpose (precedent: HighScoreStore): the save directory is
/// injected, so the logic runs under `dotnet test` — the Godot side
/// constructs it with `ProjectSettings.GlobalizePath("user://")`.
/// </summary>
public sealed class SettingsStore
{
    private readonly string _filePath;

    /// <param name="saveDirectory">Directory holding settings.json (e.g. user:// globalized).</param>
    public SettingsStore(string saveDirectory)
    {
        _filePath = Path.Combine(saveDirectory, "settings.json");
    }

    /// <summary>Current settings; missing/corrupt files read as defaults.</summary>
    public GameSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return new GameSettings();
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<GameSettings>(json) ?? new GameSettings();
        }
        catch (Exception)
        {
            return new GameSettings();
        }
    }

    /// <summary>Persists the full settings snapshot (write-through on toggle).</summary>
    public void Save(GameSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(settings));
    }
}