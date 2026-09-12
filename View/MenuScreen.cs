using Godot;
using MatchThree.Engine.Data;

namespace MatchThree.View;

/// <summary>
/// Main menu: pick Classic (75 s timer) or Zen (endless), inspect the
/// persisted high score per mode, or open the debug submenu.
/// </summary>
public partial class MenuScreen : Control
{
    private const string DebugMenuScenePath = "res://Scenes/DebugMenu.tscn";

    private Label _classicHighLabel = null!;
    private Label _zenHighLabel = null!;

    public override void _Ready()
    {
        _classicHighLabel = GetNode<Label>("VBox/ClassicHighLabel");
        _zenHighLabel = GetNode<Label>("VBox/ZenHighLabel");
        var classicButton = GetNode<Button>("VBox/ClassicButton");
        var zenButton = GetNode<Button>("VBox/ZenButton");
        var debugButton = GetNode<Button>("VBox/DebugButton");

        var scores = Game.Instance.HighScores.Load();
        _classicHighLabel.Text = $"High score: {scores.Classic}";
        _zenHighLabel.Text = $"High score: {scores.Zen}";

        classicButton.Pressed += () => Game.Instance.StartRound(GameMode.Classic);
        zenButton.Pressed += () => Game.Instance.StartRound(GameMode.Zen);
        debugButton.Pressed += () => GetTree().ChangeSceneToFile(DebugMenuScenePath);
    }
}