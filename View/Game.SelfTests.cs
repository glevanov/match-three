using Godot;
using MatchThree.Engine.Data;
using MatchThree.Engine.Model;
using MatchThree.Engine.Rules;

namespace MatchThree.View;

public partial class Game
{

    private async Task SelfTestSwapAsync()
    {
        try
        {
            await WaitUntilReadyAsync();
            var (a, b) = FindLegalSwap() ?? throw new InvalidOperationException("no legal swap on a fresh board");

            SubmitSwap(SwapIntent.Of(a, b));
            await WaitForIdleAsync();
            await NextFrameAsync();

            VerifySettled("SELFTEST-OK-swap", $"swap ({a.Row},{a.Col})<->({b.Row},{b.Col}) played");
        }
        catch (Exception e)
        {
            Fail($"SELFTEST-swap: {e}");
        }
    }

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

    private async Task SelfTestMenuAsync()
    {
        try
        {
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

    private async Task SelfTestFlowAsync()
    {
        try
        {
            await WaitUntilReadyAsync();

            StartRound(GameMode.Classic);
            await WaitUntilReadyAsync();
            if (Mode != GameMode.Classic || SecondsLeft <= 0)
                throw new InvalidOperationException($"round 1 is not a live Classic round (mode={Mode}, seconds={SecondsLeft})");
            VerifyHud("round 1 start");
            await SubmitScoringSwapAsync("round 1");
            VerifyHud("round 1 after swap");
            VerifyViewMatchesBoard("round 1 after swap");

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

    private void VerifyHud(string label)
    {
        var scoreText = HudLabel("ScoreLabel").Text;
        if (scoreText != $"Score: {Score}")
            throw new InvalidOperationException($"{label}: HUD score label '{scoreText}' != engine score {Score}");
        var timeText = HudLabel("TimeLabel").Text;
        if (timeText != $"Time: {SecondsLeft}")
            throw new InvalidOperationException($"{label}: HUD time label '{timeText}' != engine seconds {SecondsLeft}");
    }

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
        while (_boardView is null || !GodotObject.IsInstanceValid(_boardView) || _phase != GamePhase.Idle)
            await NextFrameAsync();
        await NextFrameAsync();
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
