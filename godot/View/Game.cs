using Godot;
using MatchThree.Engine.Model;
using MatchThree.Engine.Rules;

namespace MatchThree.View;

/// <summary>
/// Autoload singleton that replaces the Kotlin GameViewModel (StateFlow -&gt;
/// Godot signals). Owns the run loop: submit -&gt; engine resolves -&gt; steps
/// handed to the BoardView to play back.
///
/// The UI receives work as an ORDERED op queue that exactly one consumer drains
/// sequentially (Kotlin commit c45deee: "serialize all board animations through
/// one consumer coroutine"). StepPlayer animation state is only ever mutated
/// from that one consumer task — two coroutines touching the same GemActor
/// cancel each other's Tweens, which used to abort playback before the settle
/// callback and wedge the phase for good.
///
/// Input lock (MECHANICS.md/decisions log): while steps are resolving OR a
/// rejection animation is playing, new swap intents are buffered (most recent
/// wins) and executed when the current animation settles. Nothing is dropped —
/// except a stale intent whose pair gained or lost a Hypercube during the
/// resolution (<see cref="BufferedSwapGuard"/>): a Hypercube must only be
/// consumed by a gesture that targeted it.
///
/// G3: score accumulates as Score steps play (ScoreChanged signal), Classic
/// mode runs the 75s countdown (TimerChanged), game over is signalled with
/// round reason + score (RoundEnded) and Restart() resets the round.
/// </summary>
public partial class Game : Node
{
    /// <summary>The singleton; BoardView binds itself in _Ready.</summary>
    public static Game Instance { get; private set; } = null!;

    /// <summary>Placeholder Classic round length (MECHANICS.md: tunable per round).</summary>
    public const int ClassicTimerSeconds = 75;

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

    /// <summary>Bumped on restart; in-flight ops/callbacks from older rounds no-op.</summary>
    private int _generation;

    /// <summary>Classic countdown state; -1 = no timer active (Zen).</summary>
    public int SecondsLeft { get; private set; } = -1;

    /// <summary>Current score; Score steps accumulate live as they play.</summary>
    public int Score { get; private set; }

    /// <summary>Set when the round ended ("Time's up!", "No moves left"); null while playing.</summary>
    public string? GameOverReason { get; private set; }

    /// <summary>The mode this round runs in (menu wiring lands in G5).</summary>
    public GameMode Mode { get; }

    /// <summary>A swap buffered during input lock, with the pair's gems as the drag saw them.</summary>
    private sealed record BufferedSwap(SwapIntent Intent, Gem? A, Gem? B);

    private BufferedSwap? _bufferedSwap;

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

        var args = OS.GetCmdlineUserArgs();
        var timerSeconds = args.Contains("--selftest-timer") ? 2 : ClassicTimerSeconds;
        StartTimerIfClassic(timerSeconds);
    }

    /// <summary>
    /// Called by BoardView._Ready; starts the single consumer loop.
    /// </summary>
    public void Bind(BoardView view)
    {
        _boardView = view;
        _ = DrainLoopAsync();

        var args = OS.GetCmdlineUserArgs();
        if (args.Contains("--selftest-swap")) _ = SelfTestSwapAsync();
        if (args.Contains("--selftest-reject")) _ = SelfTestRejectAsync();
        if (args.Contains("--selftest-timer")) _ = SelfTestTimerAsync();
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
        // the reshuffle fails there is no way to keep playing (G3 surfaces it).
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

    /// <summary>Classic mode: 75s countdown placeholder (timer spec: M3 detail).</summary>
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
    /// The single consumer of the op queue — the ONLY writer of StepPlayer
    /// animation state. An op completes only when its animation fully played,
    /// and the actor pool reconciles with the settled board between ops
    /// (mirrors the Kotlin GameScreen drain effect).
    /// </summary>
    private async Task DrainLoopAsync()
    {
        Board? settledBoard = null;
        while (true)
        {
            if (!_opQueue.TryDequeue(out var op))
            {
                // Queue drained: reconcile the actor pool with the last board
                // this consumer settled, or the published board on cold start.
                _boardView!.ApplyBoard(settledBoard ?? _board);
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
                    if (gen != _generation)
                    {
                        settledBoard = null;
                        continue;
                    }
                    OnRejectionPlayed();
                    break;
                }
                case BoardOp.Resync resync:
                    _boardView!.ApplyBoard(resync.Board);
                    settledBoard = resync.Board;
                    break;
            }
        }
    }

    // --- headless self-tests ------------------------------------------------

    /// <summary>
    /// `--selftest-swap` runtime verification (CI/automation): waits for the
    /// BoardView, submits the first legal swap found by brute force, awaits the
    /// full playback, then verifies the actor pool matches the settled board
    /// and exits 0. Any error aborts with exit code 1.
    /// </summary>
    private async Task SelfTestSwapAsync()
    {
        try
        {
            await WaitUntilReadyAsync();
            var (a, b) = FindLegalSwap() ?? throw new InvalidOperationException("no legal swap on a fresh board");

            SubmitSwap(SwapIntent.Of(a, b));
            await WaitForIdleAsync();
            await NextFrameAsync(); // let the drain loop reconcile the actor pool

            VerifySettled("SELFTEST-OK-swap", $"swap ({a.Row},{a.Col})<->({b.Row},{b.Col}) played");
        }
        catch (Exception e)
        {
            Fail($"SELFTEST-swap: {e}");
        }
    }

    /// <summary>
    /// `--selftest-reject`: submits an adjacent-but-illegal swap (no match) and
    /// verifies the there-and-back rejection animation returns the phase to Idle.
    /// </summary>
    private async Task SelfTestRejectAsync()
    {
        try
        {
            await WaitUntilReadyAsync();
            var (a, b) = FindIllegalAdjacentSwap() ?? throw new InvalidOperationException("no illegal adjacent swap");

            SubmitSwap(SwapIntent.Of(a, b));
            await WaitForIdleAsync();

            VerifySettled("SELFTEST-OK-reject", $"illegal swap ({a.Row},{a.Col})<->({b.Row},{b.Col}) rejected and settled");
        }
        catch (Exception e)
        {
            Fail($"SELFTEST-reject: {e}");
        }
    }

    /// <summary>
    /// `--selftest-burst=N`: submits N legal swaps back-to-back without waiting
    /// — exercising the input-lock buffer (most-recent wins) and the drain loop
    /// under rapid cascades. Ends when the queue drains and the phase is Idle.
    /// </summary>
    private async Task SelfTestBurstAsync(int burst)
    {
        try
        {
            await WaitUntilReadyAsync();
            var submitted = 0;
            for (var i = 0; i < burst; i++)
            {
                var (a, b) = FindLegalSwap() ?? throw new InvalidOperationException($"round {i}: no legal swap");
                SubmitSwap(SwapIntent.Of(a, b));
                submitted++;
            }

            await WaitForIdleAsync();
            await NextFrameAsync();

            VerifySettled("SELFTEST-OK-burst", $"{submitted} rapid swaps submitted through input lock");
        }
        catch (Exception e)
        {
            Fail($"SELFTEST-burst: {e}");
        }
    }

    /// <summary>
    /// `--selftest-timer`: runs with a 2-second Classic timer, waits for the
    /// round to end (RoundEnded / GameOver phase), then restarts and verifies
    /// the round resets to Idle with a fresh board.
    /// </summary>
    private async Task SelfTestTimerAsync()
    {
        try
        {
            await WaitUntilReadyAsync();
            while (GameOverReason is null) await NextFrameAsync();

            var reason = GameOverReason;
            var endedScore = Score;
            if (_phase != GamePhase.GameOver)
                throw new InvalidOperationException("phase is not GameOver when the timer ends");

            Restart();
            await NextFrameAsync();
            await NextFrameAsync();

            if (_phase != GamePhase.Idle || GameOverReason is not null)
                throw new InvalidOperationException("restart did not reset the round");

            GD.Print($"SELFTEST-OK-timer: round ended ({reason}, score={endedScore}) and restart reset to Idle");
            GetTree().Quit(0);
        }
        catch (Exception e)
        {
            Fail($"SELFTEST-timer: {e}");
        }
    }

    private async Task WaitUntilReadyAsync()
    {
        while (_boardView is null || _phase != GamePhase.Idle) await NextFrameAsync();
        await NextFrameAsync(); // actor pool reconciled at least once by the drain loop
    }

    private async Task WaitForIdleAsync()
    {
        while (_phase != GamePhase.Idle || _opQueue.Count > 0) await NextFrameAsync();
    }

    private (Position A, Position B)? FindLegalSwap()
    {
        foreach (var pos in _board.Positions())
        {
            foreach (var neighbor in new[] { new Position(pos.Row, pos.Col + 1), new Position(pos.Row + 1, pos.Col) })
            {
                if (_board.IsInside(neighbor) && _engine.IsLegalSwap(_board, pos, neighbor))
                {
                    return (pos, neighbor);
                }
            }
        }
        return null;
    }

    private (Position A, Position B)? FindIllegalAdjacentSwap()
    {
        var any = _board.Positions().FirstOrDefault(p => _board.GemAt(p) is not null);
        foreach (var pos in _board.Positions())
        {
            foreach (var neighbor in new[] { new Position(pos.Row, pos.Col + 1), new Position(pos.Row + 1, pos.Col) })
            {
                if (_board.IsInside(neighbor) && _board.GemAt(pos) is not null && _board.GemAt(neighbor) is not null
                    && !_engine.IsLegalSwap(_board, pos, neighbor))
                {
                    return (pos, neighbor);
                }
            }
        }
        return null;
    }

    /// <summary>The settled board must be full, match-free, and the phase back to Idle.</summary>
    private void VerifySettled(string label, string detail)
    {
        var settledCount = _board.Positions().Count(p => _board.GemAt(p) is not null);
        var matches = MatchDetector.FindMatches(_board).Count;
        GD.Print($"{label}: {detail}; settled board {settledCount}/81 gems, {matches} matches; score={Score}; phase={_phase}");
        if (settledCount != 81 || matches != 0 || _phase != GamePhase.Idle)
        {
            Fail($"{label}: invariants violated");
            return;
        }
        GetTree().Quit(0);
    }

    private void Fail(string message)
    {
        GD.PrintErr(message);
        GetTree().Quit(1);
    }

    private async Task NextFrameAsync() =>
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
}