using MatchThree.Engine.Data;
using NUnit.Framework;

namespace MatchThree.Engine.Tests;

public class SettingsStoreTest
{
    private string _tempDir = null!;
    private SettingsStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"matchthree-tests-{Guid.NewGuid():N}");
        _store = new SettingsStore(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void MissingFileReadsAsDefaults()
    {
        Assert.That(_store.Load(), Is.EqualTo(new GameSettings()));
        Assert.That(_store.Load().MusicEnabled, Is.True);
    }

    [Test]
    public void SaveRoundTrips()
    {
        _store.Save(new GameSettings(MusicEnabled: false));
        Assert.That(_store.Load(), Is.EqualTo(new GameSettings(MusicEnabled: false)));
    }

    [Test]
    public void SaveOverwritesPreviousSettings()
    {
        _store.Save(new GameSettings(MusicEnabled: false));
        _store.Save(new GameSettings());
        Assert.That(_store.Load(), Is.EqualTo(new GameSettings()));
    }

    [Test]
    public void CorruptFileReadsAsDefaults()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "settings.json"), "not json {{{");
        Assert.That(_store.Load(), Is.EqualTo(new GameSettings()));
    }
}