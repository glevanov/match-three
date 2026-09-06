using MatchThree.Engine.Data;
using NUnit.Framework;

namespace MatchThree.Engine.Tests;

/// <summary>JVM tests for the M5 high-score persistence logic (mirrors app/src/test HighScoreStoreTest).</summary>
public class HighScoreStoreTest
{
    private string _tempDir = null!;
    private HighScoreStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"matchthree-tests-{Guid.NewGuid():N}");
        _store = new HighScoreStore(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void SaveIfBeatsPersistsNewHighScore()
    {
        Assert.That(_store.SaveIfBeats(GameMode.Classic, 500), Is.True);
        Assert.That(_store.Load().Classic, Is.EqualTo(500));
    }

    [Test]
    public void SaveIfBeatsKeepsHighestScoreOnly()
    {
        _store.SaveIfBeats(GameMode.Classic, 500);
        Assert.That(_store.SaveIfBeats(GameMode.Classic, 300), Is.False);
        Assert.That(_store.Load().Classic, Is.EqualTo(500));
        Assert.That(_store.SaveIfBeats(GameMode.Classic, 900), Is.True);
        Assert.That(_store.Load().Classic, Is.EqualTo(900));
    }

    [Test]
    public void ClassicAndZenScoresAreIndependent()
    {
        _store.SaveIfBeats(GameMode.Classic, 500);
        _store.SaveIfBeats(GameMode.Zen, 900);
        var scores = _store.Load();
        Assert.That(scores.Classic, Is.EqualTo(500));
        Assert.That(scores.Zen, Is.EqualTo(900));
    }

    [Test]
    public void NonPositiveScoresAreNeverSaved()
    {
        Assert.That(_store.SaveIfBeats(GameMode.Zen, 0), Is.False);
        Assert.That(_store.SaveIfBeats(GameMode.Zen, -10), Is.False);
        Assert.That(_store.Load().Zen, Is.EqualTo(0));
    }

    [Test]
    public void CorruptFileReadsAsZeros()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "highscores.json"), "not json {{{");
        Assert.That(_store.Load(), Is.EqualTo(new HighScores()));
    }

    [Test]
    public void ForModeMapsToTheRightPerModeField()
    {
        var scores = new HighScores(Classic: 100, Zen: 200);
        Assert.That(scores.ForMode(GameMode.Classic), Is.EqualTo(100));
        Assert.That(scores.ForMode(GameMode.Zen), Is.EqualTo(200));
    }
}