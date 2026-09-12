using Godot;

namespace MatchThree.View;

public partial class GameOverScreen : CanvasLayer
{
    private Label _reasonLabel = null!;
    private Label _scoreLabel = null!;
    private Label _highScoreLabel = null!;
    private Label _newHighLabel = null!;
    private Button _playAgainButton = null!;
    private Button _menuButton = null!;
    private Game _game = null!;

    public override void _Ready()
    {
        Visible = false;
        _reasonLabel = GetNode<Label>("Panel/VBox/ReasonLabel");
        _scoreLabel = GetNode<Label>("Panel/VBox/ScoreLabel");
        _highScoreLabel = GetNode<Label>("Panel/VBox/HighScoreLabel");
        _newHighLabel = GetNode<Label>("Panel/VBox/NewHighLabel");
        _playAgainButton = GetNode<Button>("Panel/VBox/PlayAgainButton");
        _menuButton = GetNode<Button>("Panel/VBox/MenuButton");

        _game = Game.Instance;
        _game.RoundEnded += OnRoundEnded;
        _game.RoundStarted += OnRoundStarted;
        _playAgainButton.Pressed += () => Game.Instance.Restart();
        _menuButton.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/Menu.tscn");
    }

    public override void _ExitTree()
    {
        if (_game is null) return;
        _game.RoundEnded -= OnRoundEnded;
        _game.RoundStarted -= OnRoundStarted;
    }

    private void OnRoundEnded(string reason, int score)
    {
        var savedNew = _game.HighScores.SaveIfBeats(_game.Mode, score);
        _reasonLabel.Text = reason;
        _scoreLabel.Text = $"Score: {score}";
        _highScoreLabel.Text = $"Best: {_game.HighScores.Load().ForMode(_game.Mode)}";
        _newHighLabel.Visible = savedNew;
        Visible = true;
    }

    private void OnRoundStarted() => Visible = false;
}