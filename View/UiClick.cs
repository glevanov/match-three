using Godot;

namespace MatchThree.View;

public partial class UiClick : Node
{
    public const string NoClickGroup = "no_ui_click";

    private static readonly StringName SubscribedMeta = "ui_click_subscribed";

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
