using Godot;
using MatchThree.Engine.Model;
using MatchThree.Engine.Rules;

namespace MatchThree.View;

/// <summary>
/// Plays back engine <see cref="Step"/> events as animations over a pool of
/// instanced <see cref="GemActor"/> nodes.
///
/// The player owns the only mutable render state: a gem-id -&gt; actor map plus a
/// logical id grid (which gem sits in which cell). BoardView renders the nodes;
/// Game.cs resolves steps and hands them to the UI to play back (AGENTS.md).
///
/// Timing constants (from MECHANICS.md where they exist):
///  - swap: ~150ms
///  - invalid swap there-and-back: ~150ms
///  - destroy/shrink: 200ms
///  - falls/spawns: ~70ms per row, min 90ms (constant speed, simultaneous landing)
/// </summary>
public sealed class StepPlayer
{
    /// <summary>Swap duration (ms).</summary>
    public const float SwapMillis = 150f;

    /// <summary>Destroy/shrink duration (ms).</summary>
    public const float DestroyMillis = 200f;

    /// <summary>Base fall duration (ms).</summary>
    public const float FallBaseMillis = 90f;

    /// <summary>Additional duration per row fallen (ms).</summary>
    public const float FallPerRowMillis = 70f;

    private readonly Node _parent;
    private readonly BoardConfig _config;
    private readonly PackedScene _actorScene;
    private readonly float _cellSizePx;
    private readonly Dictionary<int, GemActor> _actors = new();
    private readonly int?[,] _ids;
    private readonly Action? _onSwap;
    private readonly Action<int>? _onDestroy;
    private int _destroyCount;

    /// <param name="parent">Node the actor instances are added to (the BoardView).</param>
    /// <param name="config">Board geometry (9x9).</param>
    /// <param name="cellSizePx">Pixel size of one board cell.</param>
    /// <param name="onSwap">Invoked when a genuine engine <see cref="Step.Swap"/>
    /// animates (accepted swap only; rejection playback stays silent).</param>
    /// <param name="onDestroy">Invoked when a <see cref="Step.Destroy"/> animates,
    /// with the 1-based cascade generation (the engine emits one Destroy step per
    /// cascade round, so this doubles as cascade depth for pitch rising).</param>
    public StepPlayer(Node parent, BoardConfig config, float cellSizePx, Action? onSwap = null, Action<int>? onDestroy = null)
    {
        _parent = parent;
        _config = config;
        _cellSizePx = cellSizePx;
        _ids = new int?[config.Height, config.Width];
        _onSwap = onSwap;
        _onDestroy = onDestroy;
        _actorScene = GD.Load<PackedScene>("res://Scenes/GemActor.tscn");
    }

    /// <summary>True when the cell currently holds a gem.</summary>
    public bool HasGem(Position position) =>
        position.Row >= 0 && position.Row < _config.Height &&
        position.Col >= 0 && position.Col < _config.Width &&
        _ids[position.Row, position.Col] is not null;

    /// <summary>Snaps the actor pool to a settled <paramref name="board"/> (initial load and post-playback).</summary>
    public void ApplyBoard(Board board)
    {
        var liveIds = board.Positions()
            .Select(p => board.GemAt(p))
            .Where(g => g is not null)
            .Select(g => g!.Value.Id)
            .ToHashSet();

        foreach (var id in _actors.Keys.Where(id => !liveIds.Contains(id)).ToList())
        {
            _actors[id].QueueFree();
            _actors.Remove(id);
        }

        for (var row = 0; row < board.Height; row++)
        {
            for (var col = 0; col < board.Width; col++)
            {
                var gem = board.GemAt(row, col);
                if (gem is null) continue;

                _ids[row, col] = gem.Value.Id;
                if (_actors.TryGetValue(gem.Value.Id, out var existing))
                {
                    existing.Position = CellCenter(row, col);
                }
                else
                {
                    var actor = SpawnActor(gem.Value);
                    actor.Position = CellCenter(row, col);
                }
            }
        }
    }

    /// <summary>Plays a full engine resolution; invokes <paramref name="onSettled"/> after the last step.</summary>
    public async Task PlayAsync(List<Step> steps, Action<Board>? onSettled, Action<int>? onScore)
    {
        // One resolution = one swap's full cascade; the destroy counter (and
        // with it the pop pitch) resets per resolution.
        _destroyCount = 0;
        foreach (var step in steps)
        {
            await PlayStepAsync(step, onScore);
        }

        var settled = steps.OfType<Step.Settled>().LastOrDefault();
        if (settled is not null) onSettled?.Invoke(settled.Board);
    }

    /// <summary>Animates an invalid swap: swap across and right back (no onSettled).</summary>
    public async Task PlayRejectionAsync(Position a, Position b)
    {
        await SwapActorsAsync(a, b);
        await SwapActorsAsync(a, b);
    }

    private async Task PlayStepAsync(Step step, Action<int>? onScore)
    {
        switch (step)
        {
            case Step.Swap swap:
                _onSwap?.Invoke();
                await SwapActorsAsync(swap.A, swap.B);
                break;
            case Step.ComboActivate:
                break; // the following Destroy animates the clear
            case Step.SpecialBirth birth:
                MarkSpecial(birth.GemId, birth.Special);
                break;
            case Step.Destroy destroy:
                _destroyCount++;
                _onDestroy?.Invoke(_destroyCount);
                await DestroyActorsAsync(destroy.Positions);
                break;
            case Step.Score score:
                onScore?.Invoke(score.Delta); // points accumulate live
                break;
            case Step.Fall fall:
                await FallActorsAsync(fall.Moves);
                break;
            case Step.Spawn spawn:
                await SpawnActorsAsync(spawn.Gems);
                break;
            case Step.Settled:
                break; // handled in PlayAsync
        }
    }

    /// <summary>Marks an existing actor as a special after a birth step.</summary>
    private void MarkSpecial(int gemId, Special special)
    {
        if (_actors.TryGetValue(gemId, out var actor)) actor.SetSpecial(special);
    }

    private async Task SwapActorsAsync(Position a, Position b)
    {
        var idA = _ids[a.Row, a.Col];
        var idB = _ids[b.Row, b.Col];
        if (idA is null || idB is null) return;
        if (!_actors.TryGetValue(idA.Value, out var actorA) || !_actors.TryGetValue(idB.Value, out var actorB)) return;

        var targetA = CellCenter(b.Row, b.Col);
        var targetB = CellCenter(a.Row, a.Col);
        await Task.WhenAll(
            actorA.MoveToAsync(targetA, SwapMillis),
            actorB.MoveToAsync(targetB, SwapMillis));

        _ids[a.Row, a.Col] = idB;
        _ids[b.Row, b.Col] = idA;
    }

    private async Task DestroyActorsAsync(ISet<Position> positions)
    {
        var doomed = new List<GemActor>();
        foreach (var p in positions)
        {
            var id = _ids[p.Row, p.Col];
            if (id is not null)
            {
                if (_actors.TryGetValue(id.Value, out var actor)) doomed.Add(actor);
                _ids[p.Row, p.Col] = null;
            }
        }
        if (doomed.Count == 0) return;

        await Task.WhenAll(doomed.Select(a => a.VanishAsync(DestroyMillis)));

        foreach (var actor in doomed)
        {
            _actors.Remove(actor.GemId);
            actor.QueueFree();
        }
    }

    private async Task FallActorsAsync(List<Step.Fall.FallMove> moves)
    {
        if (moves.Count == 0) return;
        await Task.WhenAll(moves.Select(FallActorAsync));
    }

    private async Task FallActorAsync(Step.Fall.FallMove fall)
    {
        if (!_actors.TryGetValue(fall.GemId, out var actor)) return;

        var target = CellCenter(fall.To.Row, fall.To.Col);
        var rows = Math.Abs(fall.To.Row - fall.From.Row);
        await actor.MoveToAsync(target, FallSpec(rows));

        _ids[fall.To.Row, fall.To.Col] = fall.GemId;
        _ids[fall.From.Row, fall.From.Col] = null;
    }

    private async Task SpawnActorsAsync(List<Step.Spawn.Placement> spawned)
    {
        if (spawned.Count == 0) return;

        // All gems start the same distance above their target so every column's
        // refill lands simultaneously at constant speed.
        var maxTargetRow = spawned.Max(p => p.Position.Row);
        var distance = maxTargetRow + 1;

        await Task.WhenAll(spawned.Select(p => SpawnActorAsync(p.Gem, p.Position, distance)));
    }

    private async Task SpawnActorAsync(Gem gem, Position position, int distance)
    {
        var target = CellCenter(position.Row, position.Col);
        var start = target - new Vector2(0f, distance * _cellSizePx);

        var actor = SpawnActor(gem);
        actor.Position = start;
        _ids[position.Row, position.Col] = gem.Id;

        await actor.MoveToAsync(target, FallSpec(distance));
    }

    private GemActor SpawnActor(Gem gem)
    {
        var actor = _actorScene.Instantiate<GemActor>();
        _parent.AddChild(actor);
        actor.Configure(gem.Id, gem.Type, gem.Special, _cellSizePx);
        _actors[gem.Id] = actor;
        return actor;
    }

    private Vector2 CellCenter(int row, int col) =>
        new((col + 0.5f) * _cellSizePx, (row + 0.5f) * _cellSizePx);

    private float FallSpec(int rows) => FallBaseMillis + rows * FallPerRowMillis;
}