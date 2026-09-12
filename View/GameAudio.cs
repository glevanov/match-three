using Godot;
using MatchThree.Engine.Data;

namespace MatchThree.View;

public partial class GameAudio : Node
{
    private AudioStreamPlayer _music = null!;
    private AudioStreamPlayer _swipe = null!;
    private AudioStreamPlayer _pop = null!;
    private AudioStreamPlayer _flame = null!;
    private AudioStreamPlayer _star = null!;
    private AudioStreamPlayer _hypercube = null!;
    private AudioStreamPlayer _birth = null!;
    private SettingsStore _settings = null!;
    private Game _game = null!;
    private bool _roundActive;

    public bool MusicEnabled { get; private set; } = true;

    private const float PopSemitonesPerCascade = 2f;

    private const float PopMaxSemitones = 6f;

    public override void _EnterTree()
    {
        _settings = new SettingsStore(ProjectSettings.GlobalizePath("user://"));
        MusicEnabled = _settings.Load().MusicEnabled;
    }

    public override void _Ready()
    {
        _music = GetNode<AudioStreamPlayer>("Music");
        _swipe = GetNode<AudioStreamPlayer>("Swipe");
        _pop = GetNode<AudioStreamPlayer>("Pop");
        _flame = GetNode<AudioStreamPlayer>("Flame");
        _star = GetNode<AudioStreamPlayer>("Star");
        _hypercube = GetNode<AudioStreamPlayer>("Hypercube");
        _birth = GetNode<AudioStreamPlayer>("Birth");

        var game = Game.Instance;
        _game = game;
        game.RoundStarted += OnRoundStarted;
        game.RoundEnded += OnRoundEnded;

        _roundActive = game.GameOverReason is null;
        if (!Game.IsSelfTestRun && _roundActive) StartMusic();
    }

    public override void _ExitTree()
    {
        if (_game is null) return;
        _game.RoundStarted -= OnRoundStarted;
        _game.RoundEnded -= OnRoundEnded;
    }

    private void OnRoundStarted()
    {
        _roundActive = true;
        StartMusic();
    }

    private void OnRoundEnded(string reason, int score)
    {
        _roundActive = false;
        StopMusic();
    }

    public void ToggleMusic()
    {
        MusicEnabled = !MusicEnabled;
        _settings.Save(new GameSettings(MusicEnabled));
        if (MusicEnabled)
        {
            if (_roundActive) StartMusic();
        }
        else
        {
            StopMusic();
        }
    }

    public void PlaySwapSfx() => _swipe.Play();

    public void PlayPop(int cascade)
    {
        var semitones = Mathf.Min((cascade - 1) * PopSemitonesPerCascade, PopMaxSemitones);
        _pop.PitchScale = Mathf.Pow(2f, semitones / 12f);
        _pop.Play();
    }

    public void PlayFlameSfx() => _flame.Play();

    public void PlayStarSfx() => _star.Play();

    public void PlayHypercubeSfx() => _hypercube.Play();

    public void PlaySpecialBirthSfx() => _birth.Play();

    private void StartMusic()
    {
        if (!MusicEnabled) return;
        _music.Play();
    }

    private void StopMusic() => _music.Stop();
}
