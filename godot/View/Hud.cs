using Godot;

namespace MatchThree.View;

/// <summary>
/// Top HUD strip: score, Classic timer, mode label (Kotlin GameScreen row).
/// Subscribes to Game.cs signals (the StateFlow -&gt; Godot signal mapping).
/// </summary>
public partial class Hud : CanvasLayer
{
    private Label _scoreLabel = null!;
    private Label _timeLabel = null!;
    private Label _modeLabel = null!;

    public override void _Ready()
    {
        _scoreLabel = GetNode<Label>("HudBox/ScoreLabel");
        _timeLabel = GetNode<Label>("HudBox/TimeLabel");
        _modeLabel = GetNode<Label>("HudBox/ModeLabel");

        var game = Game.Instance;
        game.ScoreChanged += score => _scoreLabel.Text = $"Score: {score}";
        game.TimerChanged += seconds =>
        {
            _timeLabel.Visible = seconds >= 0;
            if (seconds >= 0) _timeLabel.Text = $"Time: {seconds}";
        };

        // Initial state (restart also re-emits ScoreChanged/TimerChanged).
        _scoreLabel.Text = $"Score: {game.Score}";
        _modeLabel.Text = game.Mode == GameMode.Classic ? "Classic" : "Zen";
        var seconds = game.SecondsLeft;
        _timeLabel.Visible = seconds >= 0;
        if (seconds >= 0) _timeLabel.Text = $"Time: {seconds}";
    }
}