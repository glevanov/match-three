using Godot;

namespace MatchThree.View;

/// <summary>
/// Small debug submenu reachable from the main menu. It keeps the production
/// menu to one Debug entry, then fans out into the separate gem-effect and
/// sound-effect preview screens.
/// </summary>
public partial class DebugMenuScreen : Control
{
    private const string MenuScenePath = "res://Scenes/Menu.tscn";
    private const string DebugGemScenePath = "res://Scenes/DebugStarGlow.tscn";
    private const string DebugSoundScenePath = "res://Scenes/DebugSounds.tscn";

    public override void _Ready()
    {
        var menuButton = GetNode<Button>("MenuButton");
        menuButton.Position = new Vector2(20f, SafeArea.TopInsetPx + SafeArea.MarginPx);
        menuButton.Pressed += () => GetTree().ChangeSceneToFile(MenuScenePath);

        GetNode<Button>("VBox/DebugGlowButton").Pressed +=
            () => GetTree().ChangeSceneToFile(DebugGemScenePath);
        GetNode<Button>("VBox/DebugSoundButton").Pressed +=
            () => GetTree().ChangeSceneToFile(DebugSoundScenePath);
    }
}
