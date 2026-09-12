using Godot;
using MatchThree.Engine.Data;
using MatchThree.Engine.Model;
using MatchThree.Engine.Rules;

namespace MatchThree.View;

public partial class Game : Node
{
    public static Game Instance { get; private set; } = null!;

    public const int ClassicTimerSeconds = 75;

    private static string[]? _selfTestArgs;

    public static string[] SelfTestArgs =>
        _selfTestArgs ??= OS.GetCmdlineUserArgs().Concat(OS.GetCmdlineArgs()).ToArray();

    public static bool IsSelfTestRun => SelfTestArgs.Any(a => a.StartsWith("--selftest"));

    [Signal]
    public delegate void ScoreChangedEventHandler(int score);

    [Signal]
    public delegate void TimerChangedEventHandler(int secondsLeft);

    [Signal]
    public delegate void RoundEndedEventHandler(string reason, int score);

    [Signal]
    public delegate void RoundStartedEventHandler();

    private readonly BoardConfig _config = new();
    private GameEngine _engine = null!;
    private Board _board = null!;
    private GamePhase _phase = GamePhase.Idle;
    private BoardView? _boardView;
    private readonly Queue<BoardOp> _opQueue = new();
    private TaskCompletionSource _queueSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task? _timerTask;
    private HighScoreStore _highScores = null!;

    private int _generation;

    public int SecondsLeft { get; private set; } = -1;

    public int Score { get; private set; }

    public string? GameOverReason { get; private set; }

    public GameMode Mode { get; private set; } = GameMode.Classic;

    public HighScoreStore HighScores => _highScores;

    private sealed record BufferedSwap(SwapIntent Intent, Gem? A, Gem? B);

    private BufferedSwap? _bufferedSwap;

    private int _drainEpoch;

    public Game() : this(GameMode.Classic)
    {
    }

    public Game(GameMode mode)
    {
        Mode = mode;
    }

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        _engine = new GameEngine(_config, new SeededRandom(DateTime.Now.Ticks));
        _board = _engine.NewGame();
        _phase = GamePhase.Idle;
        _highScores = new HighScoreStore(ProjectSettings.GlobalizePath("user://"));

        var args = SelfTestArgs;
        if (IsSelfTestRun)
        {
            GetTree().CallDeferred("change_scene_to_file", "res://Scenes/Game.tscn");
        }
        if (args.Contains("--selftest-timer"))
        {
            StartTimerIfClassic(2);
        }
    }

    private bool _selftestsStarted;

    public void Bind(BoardView view)
    {
        _boardView = view;
        var epoch = ++_drainEpoch;
        _ = DrainLoopAsync(epoch);

        if (_selftestsStarted) return;
        _selftestsStarted = true;

        var args = SelfTestArgs;
        if (args.Contains("--selftest-swap")) _ = SelfTestSwapAsync();
        if (args.Contains("--selftest-reject")) _ = SelfTestRejectAsync();
        if (args.Contains("--selftest-timer")) _ = SelfTestTimerAsync();
        if (args.Contains("--selftest-special")) _ = SelfTestSpecialAsync();
        if (args.Contains("--selftest-hypercube")) _ = SelfTestHypercubeAsync();
        if (args.Contains("--selftest-menu")) _ = SelfTestMenuAsync();
        if (args.Contains("--selftest-flow")) _ = SelfTestFlowAsync();
        var burstArg = args.FirstOrDefault(a => a.StartsWith("--selftest-burst="));
        if (burstArg is not null && int.TryParse(burstArg.Split('=')[1], out var burst) && burst > 0)
        {
            _ = SelfTestBurstAsync(burst);
        }
    }

    public void SubmitSwap(SwapIntent intent)
    {
        if (_phase == GamePhase.GameOver) return;
        if (_phase != GamePhase.Idle)
        {
            _bufferedSwap = new BufferedSwap(intent, _board.GemAt(intent.A), _board.GemAt(intent.B));
            return;
        }
        StartResolution(intent);
    }

    public void OnStepsPlayed(Board settledBoard, int generation)
    {
        if (_phase == GamePhase.GameOver) return;
        if (generation != _generation) return;
        Attach(settledBoard);
    }

    public void OnRejectionPlayed()
    {
        if (_phase == GamePhase.GameOver) return;
        Attach(_board);
    }

    public void AddScore(int delta, int generation)
    {
        if (_phase == GamePhase.GameOver) return;
        if (generation != _generation) return;
        Score += delta;
        EmitSignal(SignalName.ScoreChanged, Score);
    }

    public void Restart()
    {
        _generation++;
        _timerTask = null;
        _bufferedSwap = null;
        _opQueue.Clear();
        GameOverReason = null;
        Score = 0;
        _board = _engine.NewGame();
        _phase = GamePhase.Idle;
        SetSecondsLeft(-1);
        EmitSignal(SignalName.ScoreChanged, 0);
        EmitSignal(SignalName.RoundStarted);
        StartTimerIfClassic(ClassicTimerSeconds);

        Enqueue(new BoardOp.Resync(_board));
    }

    public void StartRound(GameMode mode)
    {
        Mode = mode;
        Restart();
        GetTree().ChangeSceneToFile("res://Scenes/Game.tscn");
    }

    public void LoadBoardForSelfTest(Board board)
    {
        _generation++;
        _opQueue.Clear();
        _bufferedSwap = null;
        _board = board;
        Score = 0;
        _phase = GamePhase.Idle;
        EmitSignal(SignalName.ScoreChanged, 0);
    }

    private void StartResolution(SwapIntent intent)
    {
        var resolved = _engine.ResolveSwap(_board, intent.A, intent.B);
        if (resolved is null)
        {
            _phase = GamePhase.Rejecting;
            Enqueue(new BoardOp.Reject(intent));
            return;
        }
        _phase = GamePhase.Resolving;
        Enqueue(new BoardOp.Play(resolved.Steps));
    }

    private void Enqueue(BoardOp op)
    {
        _opQueue.Enqueue(op);
        _ = _queueSignal.TrySetResult();
    }

    private void Attach(Board settledBoard)
    {
        _board = settledBoard;
        _phase = GamePhase.Idle;

        if (!LegalMoveDetector.HasLegalMove(_board))
        {
            var reshuffled = _engine.Reshuffle(_board);
            if (reshuffled is null)
            {
                EndGame("No moves left");
                return;
            }
            _bufferedSwap = null;
            _board = reshuffled;
            Enqueue(new BoardOp.Resync(_board));
        }

        if (_bufferedSwap is { } swap)
        {
            _bufferedSwap = null;
            var stale = BufferedSwapGuard.BufferedSwapIsStale(
                swap.A,
                swap.B,
                _board.GemAt(swap.Intent.A),
                _board.GemAt(swap.Intent.B));
            if (!stale) StartResolution(swap.Intent);
        }
    }

    private void EndGame(string reason)
    {
        if (_phase == GamePhase.GameOver) return;
        _phase = GamePhase.GameOver;
        GameOverReason = reason;
        GD.Print($"MatchThree game over: {reason}, score={Score}");
        EmitSignal(SignalName.RoundEnded, reason, Score);
    }

    private void StartTimerIfClassic(int seconds)
    {
        if (Mode != GameMode.Classic)
        {
            SetSecondsLeft(-1);
            return;
        }
        var gen = _generation;
        _timerTask = RunTimerAsync(gen, seconds);
    }

    private async Task RunTimerAsync(int gen, int seconds)
    {
        SetSecondsLeft(seconds);
        while (SecondsLeft > 0 && _phase != GamePhase.GameOver)
        {
            await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
            if (gen != _generation) return;
            if (SecondsLeft <= 0) break;
            SetSecondsLeft(SecondsLeft - 1);
        }
        if (gen != _generation || _phase == GamePhase.GameOver) return;
        if (SecondsLeft <= 0) EndGame("Time's up!");
    }

    private void SetSecondsLeft(int seconds)
    {
        SecondsLeft = seconds;
        EmitSignal(SignalName.TimerChanged, seconds);
    }

    private void TryApplyBoard(Board board)
    {
        if (_boardView is null || !GodotObject.IsInstanceValid(_boardView)) return;
        try
        {
            _boardView.ApplyBoard(board);
        }
        catch (Exception e)
        {
            GD.PrintErr($"apply board failed: {e}");
        }
    }

    private async Task DrainLoopAsync(int epoch)
    {
        Board? settledBoard = null;
        while (true)
        {
            if (epoch != _drainEpoch) return;

            if (!_opQueue.TryDequeue(out var op))
            {
                TryApplyBoard(settledBoard ?? _board);
                _queueSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                await _queueSignal.Task;
                continue;
            }

            switch (op)
            {
                case BoardOp.Play play:
                {
                    var gen = _generation;
                    try
                    {
                        await _boardView!.PlayAsync(play.Steps,
                            settled => OnStepsPlayed(settled, gen),
                            delta => AddScore(delta, gen));
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"op playback failed: {e}");
                    }
                    if (epoch != _drainEpoch) return;
                    if (gen != _generation)
                    {
                        settledBoard = null;
                        continue;
                    }
                    settledBoard = play.Steps.OfType<Step.Settled>().LastOrDefault()?.Board ?? settledBoard;
                    break;
                }
                case BoardOp.Reject reject:
                {
                    var gen = _generation;
                    try
                    {
                        await _boardView!.PlayRejectionAsync(reject.Intent.A, reject.Intent.B);
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"rejection playback failed: {e}");
                    }
                    if (epoch != _drainEpoch) return;
                    if (gen != _generation)
                    {
                        settledBoard = null;
                        continue;
                    }
                    OnRejectionPlayed();
                    break;
                }
                case BoardOp.Resync resync:
                    TryApplyBoard(resync.Board);
                    settledBoard = resync.Board;
                    break;
            }
        }
    }

}
