using Godot;
using MatchThree.Engine.Model;
using MatchThree.Engine.Rules;

namespace MatchThree.View;

/// <summary>
/// The board node: square board background + faint grid + selection marker drawn
/// in _Draw, one GemActor instance per gem managed by a StepPlayer, and input —
/// drag past 40% of a cell commits a directional swap; tap-tap select-adjacent
/// is the fallback (MECHANICS.md), both handing a SwapIntent to Game.cs.
///
/// Renders the board as per-gem nodes (one GemActor per cell); board math
/// stays in Engine/, not here.
/// </summary>
public partial class BoardView : Node2D
{
    private const float BoardFillFraction = 0.95f;
    /// <summary>
    /// How opaque the board's backdrop is (1 = solid, 0 = fully transparent):
    /// at ~0.75 the night-sky photo behind shows through as a dark tint,
    /// while the opaque gems stay fully readable on top. Lower = more
    /// see-through (DECISIONS.md "Board background (v1)").
    /// </summary>
    private const float BoardBackgroundAlpha = 0.75f;
    private static readonly Color BoardBackground = new(0x26 / 255f, 0x32 / 255f, 0x38 / 255f, BoardBackgroundAlpha);
    private static readonly Color GridColor = new(1f, 1f, 1f, 0.08f);

    private readonly BoardConfig _config = new();
    private StepPlayer? _player;
    private GameAudio? _audio;
    private float _cellSizePx;
    private Vector2 _boardSize;

    // Tap-tap fallback selection.
    private Position? _selected;

    // Drag state.
    private Position? _dragStart;
    private Vector2 _pressPosition;
    private Vector2 _dragAccum;

    public override void _Ready()
    {
        GemSprites.EnsureLoaded();
        LayoutBoard();
        _audio = GetNodeOrNull<GameAudio>("../GameAudio");
        _player = new StepPlayer(this, _config, _cellSizePx,
            onSwap: () => _audio?.PlaySwapSfx(),
            onDestroy: cascade => _audio?.PlayPop(cascade),
            onFlame: () => _audio?.PlayFlameSfx());
        Game.Instance.Bind(this);
    }

    /// <summary>Applies a board snapshot without animation (initial load, resync).</summary>
    public void ApplyBoard(Board board) => _player!.ApplyBoard(board);

    /// <summary>Plays a full engine resolution (steps in order, settle at the end).</summary>
    public Task PlayAsync(List<Step> steps, Action<Board>? onSettled, Action<int>? onScore) =>
        _player!.PlayAsync(steps, onSettled, onScore);

    /// <summary>Plays the invalid-swap there-and-back animation.</summary>
    public Task PlayRejectionAsync(Position a, Position b) =>
        _player!.PlayRejectionAsync(a, b);

    /// <summary>True when the cell currently holds a gem (input gating).</summary>
    public bool HasGem(Position position) => _player!.HasGem(position);

    /// <summary>Forces one redraw (after selection changes).</summary>
    public void RefreshSelection() => QueueRedraw();

    private void LayoutBoard()
    {
        var viewport = GetViewportRect().Size;
        // The HUD bar sits DIRECTLY on top of the board (not pinned to the
        // top of the screen): the bar + gap + square board are centered as
        // one block in the area below the device cutout. The board stays in
        // the middle of the screen and the bar follows its top edge (Hud.cs
        // reads this position). ~95% of viewport width.
        var clearanceTop = SafeArea.TopInsetPx + SafeArea.MarginPx;
        var hudBlockPx = SafeArea.HudBarHeightPx + SafeArea.HudGapPx;
        var side = Mathf.Min(
            viewport.X * BoardFillFraction,
            viewport.Y - clearanceTop - hudBlockPx - 2f * SafeArea.MarginPx);
        _boardSize = new Vector2(side, side);
        var blockTop = clearanceTop + (viewport.Y - clearanceTop - hudBlockPx - side) / 2f;
        Position = new Vector2((viewport.X - side) / 2f, blockTop + hudBlockPx);
        _cellSizePx = side / _config.Width;
    }

    public override void _Draw()
    {
        // Board background + faint grid.
        DrawRect(new Rect2(Vector2.Zero, _boardSize), BoardBackground);
        for (var row = 0; row <= _config.Height; row++)
        {
            var y = row * _cellSizePx;
            DrawLine(new Vector2(0f, y), new Vector2(_boardSize.X, y), GridColor, 1f);
        }
        for (var col = 0; col <= _config.Width; col++)
        {
            var x = col * _cellSizePx;
            DrawLine(new Vector2(x, 0f), new Vector2(x, _boardSize.Y), GridColor, 1f);
        }

        // Selection marker for the tap-tap fallback: white outline around the cell.
        if (_selected is { } selected)
        {
            var topLeft = new Vector2(selected.Col * _cellSizePx, selected.Row * _cellSizePx);
            DrawRect(
                new Rect2(topLeft, new Vector2(_cellSizePx, _cellSizePx)),
                Colors.White,
                filled: false,
                width: Mathf.Max(2f, _cellSizePx * 0.035f));
        }
    }

    // --- input (drag-to-swap + tap-tap) --------------------------------------

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventScreenTouch touch when touch.Pressed:
                BeginPress(ToLocal(touch.Position));
                break;
            case InputEventScreenTouch touch:
                EndPress(CellAt(ToLocal(touch.Position)));
                break;
            case InputEventScreenDrag drag:
                DragTo(ToLocal(drag.Position));
                break;
            case InputEventMouseButton mouse when mouse.ButtonIndex == MouseButton.Left && mouse.Pressed:
                BeginPress(ToLocal(mouse.Position));
                break;
            case InputEventMouseButton mouse when mouse.ButtonIndex == MouseButton.Left:
                EndPress(CellAt(ToLocal(mouse.Position)));
                break;
            case InputEventMouseMotion motion when motion.ButtonMask.HasFlag(MouseButtonMask.Left):
                DragTo(ToLocal(motion.Position));
                break;
        }
    }

    private void BeginPress(Vector2 local)
    {
        if (!IsInside(local)) return;
        _dragStart = CellAt(local);
        _pressPosition = local;
        _dragAccum = Vector2.Zero;
    }

    private void DragTo(Vector2 local)
    {
        if (_dragStart is not { } start) return;
        if (!HasGem(start)) return;

        _dragAccum = local - _pressPosition;
        // MECHANICS.md: drag past 40% of a cell commits the swap.
        var thresholdPx = Mathf.Min(_boardSize.X, _boardSize.Y) / _config.Width * 0.4f;
        if (_dragAccum.Length() < thresholdPx) return;

        var target = NeighborInDominantAxis(start, _dragAccum);
        if (target is not null)
        {
            _dragStart = null; // gesture consumed: no tap fallback on release
            _dragAccum = Vector2.Zero;
            Game.Instance.SubmitSwap(SwapIntent.Of(start, target.Value));
        }
    }

    private void EndPress(Position? releaseCell)
    {
        var start = _dragStart;
        _dragStart = null;
        _dragAccum = Vector2.Zero;
        if (start is null) return;

        // Tap fallback: select / deselect / swap-adjacent. A release outside
        // the board deselects.
        if (releaseCell is null)
        {
            SetSelected(null);
            return;
        }
        switch (releaseCell.Value)
        {
            case var c when !HasGem(c):
                SetSelected(null);
                break;
            case var c when _selected is null:
                SetSelected(c);
                break;
            case var c when c == _selected:
                SetSelected(null);
                break;
            case var c when c.IsOrthogonallyAdjacentTo(_selected!.Value):
                var selected = _selected.Value;
                SetSelected(null);
                Game.Instance.SubmitSwap(SwapIntent.Of(selected, c));
                break;
            case var c:
                SetSelected(c);
                break;
        }
    }

    private void SetSelected(Position? position)
    {
        if (_selected == position) return;
        _selected = position;
        QueueRedraw();
    }

    private Position? CellAt(Vector2 local)
    {
        if (!IsInside(local)) return null;
        var col = (int)(local.X / _cellSizePx);
        var row = (int)(local.Y / _cellSizePx);
        return new Position(row, col);
    }

    private Position? NeighborInDominantAxis(Position start, Vector2 accum)
    {
        var horizontal = Math.Abs(accum.X) >= Math.Abs(accum.Y);
        Position target = horizontal
            ? (accum.X > 0 ? new Position(start.Row, start.Col + 1) : new Position(start.Row, start.Col - 1))
            : (accum.Y > 0 ? new Position(start.Row + 1, start.Col) : new Position(start.Row - 1, start.Col));
        return target.Row >= 0 && target.Row < _config.Height &&
               target.Col >= 0 && target.Col < _config.Width
            ? target
            : null;
    }

    private bool IsInside(Vector2 local) =>
        local.X >= 0f && local.Y >= 0f && local.X < _boardSize.X && local.Y < _boardSize.Y;
}