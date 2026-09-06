using Godot;

namespace MatchThree.View;

/// <summary>
/// GameOver overlay: final score, mode high score (with a "New high score!"
/// note when the round beat it), Play again, and a Back-to-menu button. Hidden
/// on RoundStarted.
/// </summary>
public partial class GameOverScreen : CanvasLayer
{
    private Label _reasonLabel = null!;
    private Label _scoreLabel = null!;
    private Label _highScoreLabel = null!;
    private Label _newHighLabel = null!;
    private Button _playAgainButton = null!;
    private Button _menuButton = null!;

    public override void _Ready()
    {
        Visible = false;
        _reasonLabel = GetNode<Label>("Panel/VBox/ReasonLabel");
        _scoreLabel = GetNode<Label>("Panel/VBox/ScoreLabel");
        _highScoreLabel = GetNode<Label>("Panel/VBox/HighScoreLabel");
        _newHighLabel = GetNode<Label>("Panel/VBox/NewHighLabel");
        _playAgainButton = GetNode<Button>("Panel/VBox/PlayAgainButton");
        _menuButton = GetNode<Button>("Panel/VBox/MenuButton");

        Game.Instance.RoundEnded += (reason, score) =>
        {
            var game = Game.Instance;
            // Persist a new high score exactly once when a round ends: saving
            // here (not in Game.cs) keeps it tied to the round-end UI.
            var savedNew = game.HighScores.SaveIfBeats(game.Mode, score);
            _reasonLabel.Text = reason;
            _scoreLabel.Text = $"Score: {score}";
            _highScoreLabel.Text = $"Best: {game.HighScores.Load().ForMode(game.Mode)}";
            _newHighLabel.Visible = savedNew;
            Visible = true;
        };
        Game.Instance.RoundStarted += () => Visible = false;
        _playAgainButton.Pressed += () => Game.Instance.Restart();
        _menuButton.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/Menu.tscn");
    }
}