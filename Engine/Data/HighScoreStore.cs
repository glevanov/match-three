using System.Text.Json;

namespace MatchThree.Engine.Data;

public sealed record HighScores(int Classic = 0, int Zen = 0)
{
    public int ForMode(GameMode mode) => mode == GameMode.Classic ? Classic : Zen;
}

public sealed class HighScoreStore
{
    private readonly string _filePath;

    public HighScoreStore(string saveDirectory)
    {
        _filePath = Path.Combine(saveDirectory, "highscores.json");
    }

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