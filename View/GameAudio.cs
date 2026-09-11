using Godot;
using MatchThree.Engine.Data;

namespace MatchThree.View;

/// <summary>
/// Round audio for the board scene (Game.tscn): the looping background-music
/// bed plus one-shot sound effects. Owns no gameplay state — it just mirrors
/// the round lifecycle onto audio players.
///
/// Music policy (docs/DECISIONS.md "Audio (v1)"): the track plays only while
/// a round is actually being played on the board. It starts when the Game
/// scene appears with a round already running (menu start emits RoundStarted
/// before the scene change, so _Ready decides the initial state) or on
/// RoundStarted (Play again), and it stops when the round ends (RoundEnded)
/// or the node is freed with the scene (back to menu). Self-test runs
/// (--selftest*) skip the music entirely — they are automation, not play.
///
/// The HUD music button toggles <see cref="MusicEnabled"/> (persisted via
/// SettingsStore, user://settings.json); toggling off stops the current
/// track, toggling on resumes only while a round is active.
/// </summary>
public partial class GameAudio : Node
{
    private AudioStreamPlayer _music = null!;
    private AudioStreamPlayer _swipe = null!;
    private AudioStreamPlayer _pop = null!;
    private AudioStreamPlayer _flame = null!;
    private AudioStreamPlayer _star = null!;
    private AudioStreamPlayer _hypercube = null!;
    private SettingsStore _settings = null!;
    private Game _game = null!;
    private bool _roundActive;

    /// <summary>Background-music toggle state (persisted; default on).</summary>
    public bool MusicEnabled { get; private set; } = true;

    /// <summary>Pitch climb per cascade generation (2 semitones) — see DECISIONS.md "Audio (v1)".</summary>
    private const float PopSemitonesPerCascade = 2f;

    /// <summary>Pitch-rise ceiling so deep cascades stay musical, not cartoonish.</summary>
    private const float PopMaxSemitones = 6f;

    public override void _EnterTree()
    {
        // Loaded here (before any sibling _Ready) so HUD can read the
        // initial toggle state when it binds the button.
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

        var game = Game.Instance;
        _game = game;
        game.RoundStarted += OnRoundStarted;
        game.RoundEnded += OnRoundEnded;

        // The menu's StartRound emitted RoundStarted before this scene existed,
        // so bind the initial state here: a round is live when the scene
        // appears with no game-over recorded yet.
        _roundActive = game.GameOverReason is null;
        if (!Game.IsSelfTestRun && _roundActive) StartMusic();
    }

    /// <summary>
    /// The autoload outlives this scene: unsubscribe so the freed audio node
    /// never receives round-lifecycle signals (a throwing stale handler also
    /// aborts delivery to the live scene's subscribers).
    /// </summary>
    public override void _ExitTree()
    {
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

    /// <summary>HUD button handler: flips the persisted music toggle and
    /// applies it immediately (off stops the track; on resumes only while
    /// a round is active, so the game-over overlay stays silent).</summary>
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

    /// <summary>Whoosh for an accepted swap: StepPlayer invokes this when a
    /// genuine engine <see cref="Step.Swap"/> animates (drag or tap-tap,
    /// buffered swaps included) — invalid-swap rejections stay silent.
    /// Policy: docs/DECISIONS.md "Audio (v1)".</summary>
    public void PlaySwapSfx() => _swipe.Play();

    /// <summary>Gem-clear pop for a <see cref="Step.Destroy"/>: one pop per
    /// cascade generation, pitched up 2 semitones per generation (capped at
    /// 6) so deep cascades audibly escalate. Policy: DECISIONS.md "Audio (v1)".</summary>
    public void PlayPop(int cascade)
    {
        var semitones = Mathf.Min((cascade - 1) * PopSemitonesPerCascade, PopMaxSemitones);
        _pop.PitchScale = Mathf.Pow(2f, semitones / 12f);
        _pop.Play();
    }

    /// <summary>Fire whoosh when a Destroy step clears at least one Flame gem
    /// (swap-combo consumption or swept chain-detonation; StepPlayer detects
    /// the doomed actors). Policy: DECISIONS.md "Audio (v1)".</summary>
    public void PlayFlameSfx() => _flame.Play();

    /// <summary>Glockenspiel sweep when a Destroy step clears at least one
    /// Star gem. Policy: DECISIONS.md "Audio (v1)".</summary>
    public void PlayStarSfx() => _star.Play();

    /// <summary>Cinematic impact when a Destroy step clears at least one
    /// Hypercube gem (swap triggers, combo consumption, blast-hit activations).
    /// Policy: DECISIONS.md "Audio (v1)".</summary>
    public void PlayHypercubeSfx() => _hypercube.Play();

    private void StartMusic()
    {
        if (!MusicEnabled) return;
        _music.Play();
    }

    private void StopMusic() => _music.Stop();
}
