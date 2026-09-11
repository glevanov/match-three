using Godot;
using MatchThree.Engine.Data;
using MatchThree.Engine.Model;
using MatchThree.Engine.Rules;

namespace MatchThree.View;

public partial class Game
{
    // headless self-tests — see AGENTS.md for context.
    // Moved out of Game.cs to keep the gameplay state machine readable.
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

    /// <summary>
    /// `--selftest-special`: loads a board with two adjacent Flame gems, swaps
    /// them (Flame+Flame combo: 25 unique cells at depth 1 = 250 points), and
    /// verifies the combo plays through the full pipeline to a settled board.
    /// </summary>
    private async Task SelfTestSpecialAsync()
    {
        try
        {
            await WaitUntilReadyAsync();

            var id = 0;
            var board = Board.Create(9, 9, pos =>
                (pos.Row == 4 && pos.Col == 4) || (pos.Row == 4 && pos.Col == 5)
                    ? new Gem(id++, GemType.Red, Special.Flame)
                    : null);
            LoadBoardForSelfTest(board);
            await NextFrameAsync();

            SubmitSwap(SwapIntent.Of(new Position(4, 4), new Position(4, 5)));
            await WaitForIdleAsync();
            await NextFrameAsync();

            if (Score < 250) throw new InvalidOperationException($"flame combo scored {Score}, expected >= 250");
            VerifySettled("SELFTEST-OK-special", $"flame+flame combo played; first round 25*10=250 (total {Score})");
        }
        catch (Exception e)
        {
            Fail($"SELFTEST-special: {e}");
        }
    }

    /// <summary>
    /// `--selftest-hypercube`: swaps two adjacent Hypercubes — full-board clear
    /// (81 cells = 810 points), immediate regeneration, invariant-clean board
    /// with no specials.
    /// </summary>
    private async Task SelfTestHypercubeAsync()
    {
        try
        {
            await WaitUntilReadyAsync();

            var board = Board.Create(9, 9, pos =>
                (pos.Row == 4 && pos.Col == 4) || (pos.Row == 4 && pos.Col == 5)
                    ? new Gem(100 + pos.Row * 9 + pos.Col, GemType.Blue, Special.Hypercube)
                    : null);
            LoadBoardForSelfTest(board);
            await NextFrameAsync();

            SubmitSwap(SwapIntent.Of(new Position(4, 4), new Position(4, 5)));
            await WaitForIdleAsync();
            await NextFrameAsync();

            if (Score != 810) throw new InvalidOperationException($"H+H scored {Score}, expected 810");
            var specials = _board.Positions().Count(p => _board.GemAt(p)?.Special is not null);
            if (specials != 0) throw new InvalidOperationException($"regenerated board has {specials} specials");
            VerifySettled("SELFTEST-OK-hypercube", "H+H full clear + regeneration; 81*10=810 points");
        }
        catch (Exception e)
        {
            Fail($"SELFTEST-hypercube: {e}");
        }
    }

    /// <summary>
    /// `--selftest-menu`: starts from the menu scene, picks Zen mode, verifies
    /// the scene switch lands in the game with Mode=Zen, no timer, and the
    /// board settled — exit 0.
    /// </summary>
    private async Task SelfTestMenuAsync()
    {
        try
        {
            // We are already in the game scene (Game._Ready switched from menu).
            // Simulate a menu-driven round start in the other direction: back to
            // the menu, then start a Zen round.
            StartRound(GameMode.Zen);
            await WaitUntilReadyAsync();

            if (Mode != GameMode.Zen) throw new InvalidOperationException($"mode is {Mode}, expected Zen");
            if (SecondsLeft != -1) throw new InvalidOperationException($"Zen timer is {SecondsLeft}, expected -1");
            VerifySettled("SELFTEST-OK-menu", "Zen round started from the menu; no timer; board settled");
        }
        catch (Exception e)
        {
            Fail($"SELFTEST-menu: {e}");
        }
    }

    /// <summary>
    /// `--selftest-flow`: the basic player flow end to end, including the
    /// regression case that used to break it (a menu round-trip):
    ///
    ///   1. Classic round starts; a legal swap scores; the LIVE HUD labels
    ///      mirror the engine's score and seconds.
    ///   2. Back to the menu scene, then a second round (exactly what the menu
    ///      button does). The timer must keep ticking and the live HUD must
    ///      keep updating — dead scene UI used to stay subscribed to the
    ///      autoload's signals and abort delivery to the live scene.
    ///   3. The view must render the engine board cell-for-cell (Play again /
    ///      Restart used to leave the previous round's gems on screen).
    ///   4. Round end shows the game-over overlay on the LIVE scene, and Play
    ///      again resets the round, hides the overlay and resyncs the view.
    ///
    /// Exercised on device with:
    ///   adb shell am start -n com.matchthree/com.godot.game.GodotAppLauncher \
    ///       --esa parameters "--selftest-flow"
    /// </summary>
    private async Task SelfTestFlowAsync()
    {
        try
        {
            await WaitUntilReadyAsync();

            // --- round 1: start, score, HUD mirrors the engine ---------------
            StartRound(GameMode.Classic);
            await WaitUntilReadyAsync();
            if (Mode != GameMode.Classic || SecondsLeft <= 0)
                throw new InvalidOperationException($"round 1 is not a live Classic round (mode={Mode}, seconds={SecondsLeft})");
            VerifyHud("round 1 start");
            await SubmitScoringSwapAsync("round 1");
            VerifyHud("round 1 after swap");
            VerifyViewMatchesBoard("round 1 after swap");

            // --- menu round-trip, then round 2 (the regression case) --------
            GetTree().ChangeSceneToFile("res://Scenes/Menu.tscn");
            await NextFrameAsync();
            await NextFrameAsync();
            StartRound(GameMode.Classic);
            await WaitUntilReadyAsync();
            if (Score != 0)
                throw new InvalidOperationException($"score carried into round 2: {Score}");

            var beforeTick = SecondsLeft;
            await WaitSecondsAsync(1.2);
            if (SecondsLeft >= beforeTick)
                throw new InvalidOperationException($"timer did not tick in round 2 ({beforeTick} -> {SecondsLeft})");
            VerifyHud("round 2 after a timer tick");

            await SubmitScoringSwapAsync("round 2");
            VerifyHud("round 2 after swap");
            VerifyViewMatchesBoard("round 2 after swap");

            // --- round end: live overlay, then Play again --------------------
            EndGame("selftest flow end");
            await NextFrameAsync();
            if (!GameOverVisible())
                throw new InvalidOperationException("game-over overlay did not appear on the live scene");
            if (GameOverReasonText() != "selftest flow end")
                throw new InvalidOperationException($"overlay reason is '{GameOverReasonText()}', expected the round's reason");

            Restart();
            await WaitUntilReadyAsync();
            await NextFrameAsync();
            if (_phase != GamePhase.Idle || GameOverReason is not null || Score != 0)
                throw new InvalidOperationException("Play again did not reset the round");
            if (SecondsLeft <= 0)
                throw new InvalidOperationException($"Play again did not re-arm the Classic timer ({SecondsLeft})");
            if (GameOverVisible())
                throw new InvalidOperationException("game-over overlay is still visible after Play again");
            VerifyViewMatchesBoard("Play again");
            await SubmitScoringSwapAsync("round 3 (Play again)");
            VerifyHud("round 3 after swap");

            GD.Print($"SELFTEST-OK-flow: menu round-trip, HUD labels, view/engine sync, " +
                     $"game-over overlay and Play again resync all verified (score={Score})");
            GetTree().Quit(0);
        }
        catch (Exception e)
        {
            Fail($"SELFTEST-flow: {e}");
        }
    }

    /// <summary>Submits the first legal swap and asserts that it scored.</summary>
    private async Task SubmitScoringSwapAsync(string label)
    {
        var (a, b) = FindLegalSwap() ?? throw new InvalidOperationException($"{label}: no legal swap on the settled board");
        var before = Score;
        SubmitSwap(SwapIntent.Of(a, b));
        await WaitForIdleAsync();
        await NextFrameAsync();
        if (Score <= before)
            throw new InvalidOperationException($"{label}: legal swap ({a.Row},{a.Col})<->({b.Row},{b.Col}) scored nothing");
        GD.Print($"FLOW: {label}: swap ({a.Row},{a.Col})<->({b.Row},{b.Col}) scored {Score - before} (total {Score})");
    }

    /// <summary>
    /// The live HUD labels must mirror the engine state. A dead scene's HUD
    /// used to keep receiving these signals and throw first, which aborted
    /// delivery to the live HUD — the labels then froze on their initial text.
    /// </summary>
    private void VerifyHud(string label)
    {
        var scoreText = HudLabel("ScoreLabel").Text;
        if (scoreText != $"Score: {Score}")
            throw new InvalidOperationException($"{label}: HUD score label '{scoreText}' != engine score {Score}");
        var timeText = HudLabel("TimeLabel").Text;
        if (timeText != $"Time: {SecondsLeft}")
            throw new InvalidOperationException($"{label}: HUD time label '{timeText}' != engine seconds {SecondsLeft}");
    }

    /// <summary>The view must render exactly the engine board: same gem id per cell.</summary>
    private void VerifyViewMatchesBoard(string label)
    {
        var view = _boardView;
        if (view is null || !GodotObject.IsInstanceValid(view))
            throw new InvalidOperationException($"{label}: no live BoardView bound");
        var mismatches = _board.Positions().Count(p => _board.GemAt(p)?.Id != view.GemIdAt(p));
        if (mismatches != 0)
            throw new InvalidOperationException($"{label}: view diverged from the engine board at {mismatches} cell(s)");
    }

    private Label HudLabel(string name) =>
        GetTree().CurrentScene?.GetNodeOrNull<Label>($"HUD/HudBox/{name}")
            ?? throw new InvalidOperationException($"HUD label {name} not found on the live scene");

    private bool GameOverVisible() =>
        GetTree().CurrentScene?.GetNodeOrNull<CanvasLayer>("GameOver")?.Visible ?? false;

    private string GameOverReasonText() =>
        GetTree().CurrentScene?.GetNodeOrNull<Label>("GameOver/Panel/VBox/ReasonLabel")?.Text ?? string.Empty;

    private async Task WaitSecondsAsync(double seconds) =>
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private async Task WaitUntilReadyAsync()
    {
        // A stale (freed) BoardView from a previous scene is not "ready":
        // IsInstanceValid goes false the moment the old scene is freed, so
        // this wait survives the menu round-trips the flow test performs.
        while (_boardView is null || !GodotObject.IsInstanceValid(_boardView) || _phase != GamePhase.Idle)
            await NextFrameAsync();
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
