using Godot;
using MatchThree.Engine.Data;

namespace MatchThree.View;

/// <summary>
/// Top HUD bar (Menu, score, Classic timer, mode label) that sits DIRECTLY
/// on top of the board — the bar hugs the board's top edge wherever the
/// board is centered, instead of being pinned to the top of the screen.
/// A translucent backdrop keeps the bar legible over the dark background.
/// Subscribes to Game.cs signals; exit-to-menu goes through a confirmation
/// dialog.
/// </summary>
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

        // The bar is positioned relative to the BOARD, not the screen top:
        // BoardView centers board + bar as one block below the safe area, and
        // the bar sits right above the board's top edge (Board._Ready runs
        // before HUD's, so LayoutBoard already placed it). HUD is a
        // CanvasLayer (draws above the Board Node2D's canvas layer), so no
        // z-order changes are needed.
        var board = GetNode<BoardView>("../Board");
        var top = board.Position.Y - SafeArea.HudBarHeightPx - SafeArea.HudGapPx;
        _backdrop.OffsetTop = top;
        _backdrop.OffsetBottom = top + SafeArea.HudBarHeightPx;
        _hudBox.OffsetTop = top;
        _hudBox.OffsetBottom = top + SafeArea.HudBarHeightPx;

        // Block board swipes from starting on the bar: everything under the
        // overlay consumes the touch instead of letting it fall through to
        // BoardView's _UnhandledInput (explicit STOP; the buttons already
        // stop their own area).
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

        // Background-music toggle: state lives on GameAudio (persisted via
        // SettingsStore); the button just mirrors it.
        _musicButton.Pressed += () =>
        {
            _audio.ToggleMusic();
            UpdateMusicButton();
        };
        UpdateMusicButton();

        // Initial state (restart also re-emits ScoreChanged/TimerChanged).
        _scoreLabel.Text = $"Score: {game.Score}";
        _modeLabel.Text = game.Mode == GameMode.Classic ? "Classic" : "Zen";
        var seconds = game.SecondsLeft;
        _timeLabel.Visible = seconds >= 0;
        if (seconds >= 0) _timeLabel.Text = $"Time: {seconds}";
    }

    /// <summary>
    /// The autoload outlives this scene: a stale handler would throw on the
    /// next signal (disposed node) and, running first, abort delivery to the
    /// live HUD — the classic "timer/score stop updating after a menu trip".
    /// </summary>
    public override void _ExitTree()
    {
        _game.ScoreChanged -= OnScoreChanged;
        _game.TimerChanged -= OnTimerChanged;
    }

    private void OnScoreChanged(int score) => _scoreLabel.Text = $"Score: {score}";

    private void OnTimerChanged(int seconds)
    {
        _timeLabel.Visible = seconds >= 0;
        if (seconds >= 0) _timeLabel.Text = $"Time: {seconds}";
    }

    /// <summary>Mirrors the persisted music toggle into the button label.</summary>
    private void UpdateMusicButton() =>
        _musicButton.Text = _audio.MusicEnabled ? "♪ On" : "♪ Off";

    /// <summary>
    /// Sizes the exit confirmation popup to match the hand-set font sizes
    /// used everywhere else (fix: "when I click Menu things look small"). The
    /// stock dialog theme draws ~16px text, which reads tiny next to the
    /// 26-30px HUD/menu fonts.
    /// </summary>
    private void StyleExitDialog(Button cancelButton)
    {
        _exitDialog.MinSize = new Vector2I(360, 160);
        // Body text, its title and the OK/Cancel buttons all get the same
        // 22px so the popup matches the app's look.
        _exitDialog.GetLabel().AddThemeFontSizeOverride("font_size", 22);
        _exitDialog.AddThemeFontSizeOverride("title_font_size", 22);
        _exitDialog.GetOkButton().AddThemeFontSizeOverride("font_size", 22);
        cancelButton.AddThemeFontSizeOverride("font_size", 22);
    }
}