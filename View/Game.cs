using Godot;
using MatchThree.Engine.Data;
using MatchThree.Engine.Model;
using MatchThree.Engine.Rules;

namespace MatchThree.View;

/// <summary>
/// Autoload singleton that owns the run loop: submit -&gt; engine resolves -&gt;
/// steps handed to the BoardView to play back. State flows to the UI through
/// Godot signals (ScoreChanged/TimerChanged/RoundEnded/RoundStarted).
///
/// The UI receives work as an ORDERED op queue that exactly one consumer drains
/// sequentially ("serialize all board animations through one consumer").
/// StepPlayer animation state is only ever mutated from that one consumer task
/// — two concurrent writers on the same GemActor cancel each other's Tweens,
/// which used to abort playback before the settle callback and wedge the phase
/// for good.
///
/// Input lock (MECHANICS.md/decisions log): while steps are resolving OR a
/// rejection animation is playing, new swap intents are buffered (most recent
/// wins) and executed when the current animation settles. Nothing is dropped —
/// except a stale intent whose pair gained or lost a Hypercube during the
/// resolution (<see cref="BufferedSwapGuard"/>): a Hypercube must only be
/// consumed by a gesture that targeted it.
///
/// Score accumulates as Score steps play (ScoreChanged signal), Classic
/// mode runs the 75s countdown (TimerChanged), game over is signalled with
/// round reason + score (RoundEnded) and Restart() resets the round.
/// </summary>
public partial class Game : Node
{
    /// <summary>The singleton; BoardView binds itself in _Ready.</summary>
    public static Game Instance { get; private set; } = null!;

    /// <summary>Placeholder Classic round length (MECHANICS.md: tunable per round).</summary>
    public const int ClassicTimerSeconds = 75;

    private static string[]? _selfTestArgs;

    /// <summary>
    /// Command line args that drive the headless self-tests. On desktop they
    /// are user args (after `--`); on Android the launch activity is not
    /// exported and Godot ignores launch-intent extras, so the flags are
    /// packed into the export preset's extra args (assets/_cl_) and surface as
    /// regular engine args instead. Scan both.
    /// </summary>
    public static string[] SelfTestArgs =>
        _selfTestArgs ??= OS.GetCmdlineUserArgs().Concat(OS.GetCmdlineArgs()).ToArray();

    /// <summary>True for any --selftest* run (CI/desktop headless or on-device).</summary>
    public static bool IsSelfTestRun => SelfTestArgs.Any(a => a.StartsWith("--selftest"));

    /// <summary>Score changed; HUD listens.</summary>
    [Signal]
    public delegate void ScoreChangedEventHandler(int score);

    /// <summary>Classic countdown tick; -1 means no timer (Zen).</summary>
    [Signal]
    public delegate void TimerChangedEventHandler(int secondsLeft);

    /// <summary>A round ended (timer, dead board, ...); UI shows the GameOver overlay.</summary>
    [Signal]
    public delegate void RoundEndedEventHandler(string reason, int score);

    /// <summary>A new round started (restart); UI resets.</summary>
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

    /// <summary>Bumped on restart; in-flight ops/callbacks from older rounds no-op.</summary>
    private int _generation;

    /// <summary>Classic countdown state; -1 = no timer active (Zen).</summary>
    public int SecondsLeft { get; private set; } = -1;

    /// <summary>Current score; Score steps accumulate live as they play.</summary>
    public int Score { get; private set; }

    /// <summary>Set when the round ended ("Time's up!", "No moves left"); null while playing.</summary>
    public string? GameOverReason { get; private set; }

    /// <summary>The mode this round runs in; chosen on the menu.</summary>
    public GameMode Mode { get; private set; } = GameMode.Classic;

    /// <summary>Persistent per-mode high scores (user://highscores.json).</summary>
    public HighScoreStore HighScores => _highScores;

    /// <summary>A swap buffered during input lock, with the pair's gems as the drag saw them.</summary>
    private sealed record BufferedSwap(SwapIntent Intent, Gem? A, Gem? B);

    private BufferedSwap? _bufferedSwap;

    /// <summary>
    /// Bumped by every <see cref="Bind"/>. Drain loops capture their epoch and
    /// exit once a newer scene's consumer replaces them — a superseded loop
    /// must never touch the queue or the view again (two writers cancel each
    /// other's Tweens and wedge the phase for good).
    /// </summary>
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
        // Seeded per session for reproducibility of the session (AGENTS.md:
        // all randomness flows through SeededRandom).
        _engine = new GameEngine(_config, new SeededRandom(DateTime.Now.Ticks));
        _board = _engine.NewGame();
        _phase = GamePhase.Idle;
        _highScores = new HighScoreStore(ProjectSettings.GlobalizePath("user://"));

        var args = SelfTestArgs;
        if (IsSelfTestRun)
        {
            // Main scene is the menu; self-tests drive the game scene directly.
            // Deferred: changing scenes inside an autoload _Ready hits the tree
            // mid-instantiation ("Parent node is busy adding/removing children").
            GetTree().CallDeferred("change_scene_to_file", "res://Scenes/Game.tscn");
        }
        // The countdown belongs to a running round, not to the app: a round
        // only starts via StartRound (menu), which starts its timer inside
        // Restart(). Starting one here (default Mode=Classic) used to run a
        // phantom 75s countdown on the menu and EndGame() invisibly while the
        // player was still choosing a mode. Only --selftest-timer needs a
        // pre-round timer.
        if (args.Contains("--selftest-timer"))
        {
            StartTimerIfClassic(2);
        }
    }

    /// <summary>Selftest launchers run once per process, not per scene bind.</summary>
    private bool _selftestsStarted;

    /// <summary>
    /// Called by BoardView._Ready; starts the single consumer loop.
    /// </summary>
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

    /// <summary>Called by the UI when it starts a swap/drag intent.</summary>
    public void SubmitSwap(SwapIntent intent)
    {
        if (_phase == GamePhase.GameOver) return;
        if (_phase != GamePhase.Idle)
        {
            // _board is the last settled board — exactly what the drag was made against.
            _bufferedSwap = new BufferedSwap(intent, _board.GemAt(intent.A), _board.GemAt(intent.B));
            return;
        }
        StartResolution(intent);
    }

    /// <summary>Called by the StepPlayer after it has played a full resolution.</summary>
    public void OnStepsPlayed(Board settledBoard, int generation)
    {
        if (_phase == GamePhase.GameOver) return;
        if (generation != _generation) return;
        Attach(settledBoard);
    }

    /// <summary>Called by the StepPlayer after an invalid-swap there-and-back.</summary>
    public void OnRejectionPlayed()
    {
        if (_phase == GamePhase.GameOver) return;
        Attach(_board);
    }

    /// <summary>Called by the StepPlayer as each Score step is played back.</summary>
    public void AddScore(int delta, int generation)
    {
        if (_phase == GamePhase.GameOver) return;
        if (generation != _generation) return;
        Score += delta;
        EmitSignal(SignalName.ScoreChanged, Score);
    }

    /// <summary>New game with the current mode (used by the GameOver screen).</summary>
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

        // Wake the (parked) consumer so it snaps the actor pool to the new
        // board. Without this, "Play again" leaves the previous round's gems
        // on screen while the engine silently resolves against this new board
        // — every visible move then rejects there-and-back, nothing scores,
        // and the board feels unresponsive for the whole round.
        Enqueue(new BoardOp.Resync(_board));
    }

    /// <summary>
    /// Menu entry point: picks the mode, starts a fresh round, and swaps
    /// to the game scene.
    /// </summary>
    public void StartRound(GameMode mode)
    {
        Mode = mode;
        Restart();
        GetTree().ChangeSceneToFile("res://Scenes/Game.tscn");
    }

    /// <summary>Selftest hook: replaces the board and score without restarting the round.</summary>
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
        // Auto-reset style: signal the currently awaited TCS (a second enqueue
        // before the drain re-arms is harmless — it just sets the same TCS).
        _ = _queueSignal.TrySetResult();
    }

    private void Attach(Board settledBoard)
    {
        _board = settledBoard;
        _phase = GamePhase.Idle;

        // Board invariants (MECHANICS.md): a dead board is reshuffled; if even
        // the reshuffle fails there is no way to keep playing.
        if (!LegalMoveDetector.HasLegalMove(_board))
        {
            var reshuffled = _engine.Reshuffle(_board);
            if (reshuffled is null)
            {
                EndGame("No moves left");
                return;
            }
            _bufferedSwap = null; // layout changed; stale intents are dropped
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
            // Stale per MECHANICS.md: a Hypercube entered or left the pair during
            // resolution, so this gesture must not consume a Hypercube it never
            // targeted. Same precedent as the reshuffle branch above.
        }
    }

    private void EndGame(string reason)
    {
        if (_phase == GamePhase.GameOver) return;
        _phase = GamePhase.GameOver;
        GameOverReason = reason;
        GD.Print($"MatchThree game over: {reason}, score={Score}");
        // Queued ops intentionally survive: the drain loop plays what's left (its
        // callbacks re-check the phase and no-op), then the overlay is shown.
        EmitSignal(SignalName.RoundEnded, reason, Score);
    }

    /// <summary>Classic mode: 75s countdown placeholder (timer spec, MECHANICS.md).</summary>
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

    /// <summary>
    /// Applies a board snapshot to the view, tolerating a stale
    /// <see cref="BoardView"/> reference after a scene change: the freed node
    /// throws inside ApplyBoard, and the next Bind installs the fresh view.
    /// Kept exception-safe so a bad wake can never kill the consumer loop.
    /// </summary>
    private void TryApplyBoard(Board board)
    {
        // A stale reference to a scene node freed by a scene change must not
        // be touched (the next Bind installs the fresh view).
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

    /// <summary>
    /// The single consumer of the op queue — the ONLY writer of StepPlayer
    /// animation state. An op completes only when its animation fully played,
    /// and the actor pool reconciles with the settled board between ops.
    /// </summary>
    private async Task DrainLoopAsync(int epoch)
    {
        Board? settledBoard = null;
        while (true)
        {
            // Superseded by a newer scene's consumer: stop without dequeuing,
            // applying a board, or re-arming the wake-up signal.
            if (epoch != _drainEpoch) return;

            if (!_opQueue.TryDequeue(out var op))
            {
                // Queue drained: reconcile the actor pool with the last board
                // this consumer settled, or the published board on cold start.
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
                        settledBoard = null; // round restarted mid-play; drop stale state
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
