using Godot;

namespace MatchThree.View;

/// <summary>
/// Debug-only sound preview screen, reachable from the Debug submenu. It
/// mirrors the board scene's one-shot SFX mix (but not the music) so the
/// sounds can be auditioned quickly without starting a round.
/// </summary>
public partial class DebugSounds : Control
{
    private const string DebugMenuScenePath = "res://Scenes/DebugMenu.tscn";

    private AudioStreamPlayer _swipe = null!;
    private AudioStreamPlayer _pop = null!;
    private AudioStreamPlayer _flame = null!;
    private AudioStreamPlayer _star = null!;
    private AudioStreamPlayer _hypercube = null!;
    private AudioStreamPlayer _birth = null!;

    public override void _Ready()
    {
        var backButton = GetNode<Button>("BackButton");
        backButton.Position = new Vector2(20f, SafeArea.TopInsetPx + SafeArea.MarginPx);
        backButton.Pressed += () => GetTree().ChangeSceneToFile(DebugMenuScenePath);

        _swipe = GetNode<AudioStreamPlayer>("Audio/Swipe");
        _pop = GetNode<AudioStreamPlayer>("Audio/Pop");
        _flame = GetNode<AudioStreamPlayer>("Audio/Flame");
        _star = GetNode<AudioStreamPlayer>("Audio/Star");
        _hypercube = GetNode<AudioStreamPlayer>("Audio/Hypercube");
        _birth = GetNode<AudioStreamPlayer>("Audio/Birth");

        GetNode<Button>("VBox/Buttons/SwipeButton").Pressed += () => _swipe.Play();
        GetNode<Button>("VBox/Buttons/PopButton").Pressed += PlayPopPreview;
        GetNode<Button>("VBox/Buttons/FlameButton").Pressed += () => _flame.Play();
        GetNode<Button>("VBox/Buttons/StarButton").Pressed += () => _star.Play();
        GetNode<Button>("VBox/Buttons/HypercubeButton").Pressed += () => _hypercube.Play();
        GetNode<Button>("VBox/Buttons/BirthButton").Pressed += () => _birth.Play();
    }

    private void PlayPopPreview()
    {
        // Preview the base (first-cascade) pitch; in-game later cascades are
        // pitched up by GameAudio.PlayPop.
        _pop.PitchScale = 1f;
        _pop.Play();
    }
}
