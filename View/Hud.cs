using Godot;
using MatchThree.Engine.Data;

namespace MatchThree.View;

public partial class Hud : CanvasLayer
{
    private ColorRect _backdrop = null!;
    private Control _hudBox = null!;
    private Label _scoreLabel = null!;
    private Label _timeLabel = null!;
    private Label _modeLabel = null!;
    private Button _menuButton = null!;
    private Button _musicButton = null!;
    private AcceptDialog _exitDialog = null!;
    private GameAudio _audio = null!;
    private Game _game = null!;

    public override void _Ready()
    {
        _backdrop = GetNode<ColorRect>("HudBackdrop");
        _hudBox = GetNode<Control>("HudBox");
        _scoreLabel = GetNode<Label>("HudBox/ScoreLabel");
        _timeLabel = GetNode<Label>("HudBox/TimeLabel");
        _modeLabel = GetNode<Label>("HudBox/ModeLabel");
        _menuButton = GetNode<Button>("HudBox/MenuButton");
        _musicButton = GetNode<Button>("HudBox/MusicButton");
        _exitDialog = GetNode<AcceptDialog>("ExitDialog");
        _audio = GetNode<GameAudio>("../GameAudio");

        var board = GetNode<BoardView>("../Board");
        var top = board.Position.Y - SafeArea.HudBarHeightPx - SafeArea.HudGapPx;
        _backdrop.OffsetTop = top;
        _backdrop.OffsetBottom = top + SafeArea.HudBarHeightPx;
        _hudBox.OffsetTop = top;
        _hudBox.OffsetBottom = top + SafeArea.HudBarHeightPx;

        _backdrop.MouseFilter = Control.MouseFilterEnum.Stop;
        _hudBox.MouseFilter = Control.MouseFilterEnum.Stop;

        _exitDialog.Title = "Exit game?";
        _exitDialog.DialogText = "The current round will be lost.";
        _exitDialog.OkButtonText = "Exit";
        var cancelButton = _exitDialog.AddCancelButton("Keep playing");
        StyleExitDialog(cancelButton);

        var game = Game.Instance;
        _game = game;
        game.ScoreChanged += OnScoreChanged;
        game.TimerChanged += OnTimerChanged;
        _menuButton.Pressed += () => _exitDialog.PopupCentered();
        _exitDialog.Confirmed += () => GetTree().ChangeSceneToFile("res://Scenes/Menu.tscn");

        _musicButton.Pressed += () =>
        {
            _audio.ToggleMusic();
            UpdateMusicButton();
        };
        UpdateMusicButton();

        _scoreLabel.Text = $"Score: {game.Score}";
        _modeLabel.Text = game.Mode == GameMode.Classic ? "Classic" : "Zen";
        var seconds = game.SecondsLeft;
        _timeLabel.Visible = seconds >= 0;
        if (seconds >= 0) _timeLabel.Text = $"Time: {seconds}";
    }

    public override void _ExitTree()
    {
        if (_game is null) return;
        _game.ScoreChanged -= OnScoreChanged;
        _game.TimerChanged -= OnTimerChanged;
    }

    private void OnScoreChanged(int score) => _scoreLabel.Text = $"Score: {score}";

    private void OnTimerChanged(int seconds)
    {
        _timeLabel.Visible = seconds >= 0;
        if (seconds >= 0) _timeLabel.Text = $"Time: {seconds}";
    }

    private void UpdateMusicButton() =>
        _musicButton.Text = _audio.MusicEnabled ? "♪ On" : "♪ Off";

    private void StyleExitDialog(Button cancelButton)
    {
        _exitDialog.MinSize = new Vector2I(360, 160);
        _exitDialog.GetLabel().AddThemeFontSizeOverride("font_size", 22);
        _exitDialog.AddThemeFontSizeOverride("title_font_size", 22);
        _exitDialog.GetOkButton().AddThemeFontSizeOverride("font_size", 22);
        cancelButton.AddThemeFontSizeOverride("font_size", 22);
    }
}