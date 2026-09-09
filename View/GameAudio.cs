using Godot;

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
/// </summary>
public partial class GameAudio : Node
{
    private AudioStreamPlayer _music = null!;
    private AudioStreamPlayer _swipe = null!;

    public override void _Ready()
    {
        _music = GetNode<AudioStreamPlayer>("Music");
        _swipe = GetNode<AudioStreamPlayer>("Swipe");

        var game = Game.Instance;
        game.RoundStarted += StartMusic;
        game.RoundEnded += (_, _) => StopMusic();

        // The menu's StartRound emitted RoundStarted before this scene existed,
        // so bind the initial state here: a round is live when the scene
        // appears with no game-over recorded yet.
        var selftest = OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--selftest"));
        if (!selftest && game.GameOverReason is null) StartMusic();
    }

    /// <summary>Whoosh for a committed swap gesture (BoardView calls this at the
    /// drag/tap commit points — one sound per gesture, valid or not).</summary>
    public void PlaySwapSfx() => _swipe.Play();

    private void StartMusic() => _music.Play();

    private void StopMusic() => _music.Stop();
}
