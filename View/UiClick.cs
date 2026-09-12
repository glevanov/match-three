using Godot;

namespace MatchThree.View;

/// <summary>
/// Autoload that gives every UI button a short click without per-scene wiring:
/// it listens for <see cref="SceneTree.NodeAdded"/> and hooks
/// <see cref="BaseButton.Pressed"/> on each button as it enters the tree, so
/// new screens (and new buttons on existing screens) get it automatically.
///
/// Buttons in the <see cref="NoClickGroup"/> group are skipped — the
/// DebugSounds preview buttons, which exist to audition the board SFX and
/// would otherwise double up with their own sound.
///
/// Policy: docs/DECISIONS.md "Audio (v1)"; provenance: ASSET_SOURCES.md.
/// </summary>
public partial class UiClick : Node
{
    /// <summary>Group that opts a button out of the UI click.</summary>
    public const string NoClickGroup = "no_ui_click";

    /// <summary>Marks a button as already subscribed. A node can re-enter the
    /// tree (RemoveChild/AddChild), and its C# event subscription survives
    /// that, so without the guard the click would be wired twice.</summary>
    private static readonly StringName SubscribedMeta = "ui_click_subscribed";

    /// <summary>Click level relative to the file (peak -2.9 dBFS): -6 dB keeps
    /// it comfortably under the board SFX.</summary>
    private const float ClickVolumeDb = -6f;

    private AudioStreamPlayer _player = null!;

    public override void _Ready()
    {
        _player = new AudioStreamPlayer
        {
            Stream = GD.Load<AudioStream>("res://Assets/Audio/click.mp3"),
            VolumeDb = ClickVolumeDb,
        };
        AddChild(_player);
        GetTree().NodeAdded += OnNodeAdded;
    }

    /// <summary>The autoload outlives every scene: unsubscribe on teardown.</summary>
    public override void _ExitTree() => GetTree().NodeAdded -= OnNodeAdded;

    private void OnNodeAdded(Node node)
    {
        if (node is not BaseButton button) return;
        if (button.IsInGroup(NoClickGroup)) return;
        if (button.HasMeta(SubscribedMeta)) return;

        button.SetMeta(SubscribedMeta, true);
        button.Pressed += () => _player.Play();
    }
}
