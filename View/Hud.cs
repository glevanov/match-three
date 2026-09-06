using Godot;
using MatchThree.Engine.Data;

namespace MatchThree.View;

/// <summary>
/// Top HUD strip: Menu, score, Classic timer, mode label. Subscribes to Game.cs
/// signals. Exit-to-menu goes through a confirmation dialog.
/// </summary>
public partial class Hud : CanvasLayer
{
    private Label _scoreLabel = null!;
    private Label _timeLabel = null!;
    private Label _modeLabel = null!;
    private Button _menuButton = null!;
    private AcceptDialog _exitDialog = null!;

    public override void _Ready()
    {
        _scoreLabel = GetNode<Label>("HudBox/ScoreLabel");
        _timeLabel = GetNode<Label>("HudBox/TimeLabel");
        _modeLabel = GetNode<Label>("HudBox/ModeLabel");
        _menuButton = GetNode<Button>("HudBox/MenuButton");
        _exitDialog = GetNode<AcceptDialog>("ExitDialog");
        _exitDialog.Title = "Exit game?";
        _exitDialog.DialogText = "The current round will be lost.";
        _exitDialog.OkButtonText = "Exit";
        _exitDialog.AddCancelButton("Keep playing");

        var game = Game.Instance;
        game.ScoreChanged += score => _scoreLabel.Text = $"Score: {score}";
        game.TimerChanged += seconds =>
        {
            _timeLabel.Visible = seconds >= 0;
            if (seconds >= 0) _timeLabel.Text = $"Time: {seconds}";
        };
        _menuButton.Pressed += () => _exitDialog.PopupCentered();
        _exitDialog.Confirmed += () => GetTree().ChangeSceneToFile("res://Scenes/Menu.tscn");

        // Initial state (restart also re-emits ScoreChanged/TimerChanged).
        _scoreLabel.Text = $"Score: {game.Score}";
        _modeLabel.Text = game.Mode == GameMode.Classic ? "Classic" : "Zen";
        var seconds = game.SecondsLeft;
        _timeLabel.Visible = seconds >= 0;
        if (seconds >= 0) _timeLabel.Text = $"Time: {seconds}";
    }
}