using System.Text.Json;

namespace MatchThree.Engine.Data;

public sealed record GameSettings(bool MusicEnabled = true);

public sealed class SettingsStore
{
    private readonly string _filePath;

    public SettingsStore(string saveDirectory)
    {
        _filePath = Path.Combine(saveDirectory, "settings.json");
    }

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

    public void Save(GameSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(settings));
    }
}