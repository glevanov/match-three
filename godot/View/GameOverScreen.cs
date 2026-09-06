using Godot;

namespace MatchThree.View;

/// <summary>
/// GameOver overlay (placeholder for G3; real high-score UI lands in G5):
/// dims the board, shows the reason/score, and offers Play again. Hidden on
/// RoundStarted.
/// </summary>
public partial class GameOverScreen : CanvasLayer
{
    private Label _reasonLabel = null!;
    private Label _scoreLabel = null!;
    private Button _playAgainButton = null!;

    public override void _Ready()
    {
        Visible = false;
        _reasonLabel = GetNode<Label>("Panel/VBox/ReasonLabel");
        _scoreLabel = GetNode<Label>("Panel/VBox/ScoreLabel");
        _playAgainButton = GetNode<Button>("Panel/VBox/PlayAgainButton");

        Game.Instance.RoundEnded += (reason, score) =>
        {
            _reasonLabel.Text = reason;
            _scoreLabel.Text = $"Score: {score}";
            Visible = true;
        };
        Game.Instance.RoundStarted += () => Visible = false;
        _playAgainButton.Pressed += () => Game.Instance.Restart();
    }
}