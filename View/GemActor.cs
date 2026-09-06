using Godot;
using MatchThree.Engine.Model;

namespace MatchThree.View;

/// <summary>
/// One instanced scene per gem (GemActor.tscn), keyed by the gem's stable
/// <see cref="GemId"/> for continuity across falls/spawns.
///
/// Node layout:
///   GemActor (Node2D, this script)
///   ├── Base (Sprite2D)          color sprite, or the Hypercube art
///   └── Overlay (Node2D)
///       ├── Art (Sprite2D)       Flame/Star art, centered on the gem
///       └── Silhouette (Sprite2D) baked alpha-silhouette outline
///
/// Animations are Tween-driven; the async methods complete when the tween's
/// Finished signal fires, so StepPlayer can sequence steps with async/await.
/// Easing: Godot's Cubic+Out (fast-out-slow-in style curve).
/// </summary>
public partial class GemActor : Node2D
{
    private Sprite2D _base = null!;
    private Sprite2D _art = null!;
    private Sprite2D _silhouette = null!;
    private float _cellSizePx;

    /// <summary>Stable gem id; never changes for the lifetime of the actor.</summary>
    public int GemId { get; private set; }

    /// <summary>Gem color.</summary>
    public GemType Type { get; private set; }

    /// <summary>Current special kind, or null (updated in place by SpecialBirth).</summary>
    public Special? SpecialKind { get; private set; }

    public override void _Ready()
    {
        _base = GetNode<Sprite2D>("Base");
        var overlay = GetNode<Node2D>("Overlay");
        _art = overlay.GetNode<Sprite2D>("Art");
        _silhouette = overlay.GetNode<Sprite2D>("Silhouette");
    }

    /// <summary>Configures identity + appearance. Called exactly once at spawn/creation.</summary>
    public void Configure(int gemId, GemType type, Special? special, float cellSizePx)
    {
        GemId = gemId;
        Type = type;
        _cellSizePx = cellSizePx;
        SetSpecial(special);
    }

    /// <summary>Changes the appearance without touching identity (special birth).</summary>
    public void SetSpecial(Special? special)
    {
        SpecialKind = special;
        ApplyAppearance();
    }

    /// <summary>Moves to <paramref name="target"/> (pixels) over <paramref name="durationMs"/>.</summary>
    public async Task MoveToAsync(Vector2 target, float durationMs)
    {
        EnsureReady();
        var tween = CreateTween();
        tween.TweenProperty(this, "position", target, durationMs / 1000f)
             .SetTrans(Tween.TransitionType.Cubic)
             .SetEase(Tween.EaseType.Out);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    /// <summary>Shrinks to zero scale and fades out over <paramref name="durationMs"/>.</summary>
    public async Task VanishAsync(float durationMs)
    {
        EnsureReady();
        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(this, "scale", Vector2.Zero, durationMs / 1000f)
             .SetTrans(Tween.TransitionType.Cubic)
             .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "modulate:a", 0f, durationMs / 1000f)
             .SetTrans(Tween.TransitionType.Cubic)
             .SetEase(Tween.EaseType.Out);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    private void ApplyAppearance()
    {
        EnsureReady();
        var span = _cellSizePx * GemSprites.GemWidthFraction;

        // Base: Hypercube has its own sprite; everything else uses the color art.
        var baseTexture = GemSprites.BaseFor(Type, SpecialKind);
        _base.Texture = baseTexture;
        _base.Scale = Vector2.One * (span / Mathf.Max(baseTexture.GetWidth(), baseTexture.GetHeight()));

        // Overlay: Flame (solid, 0.45 inset) and Star (55% opacity, full gem).
        var overlayTexture = GemSprites.ArtFor(SpecialKind);
        _art.Visible = overlayTexture is not null;
        _silhouette.Visible = overlayTexture is not null;
        if (overlayTexture is null) return;

        var overlaySpan = span * GemSprites.OverlayFraction(SpecialKind);
        _art.Texture = overlayTexture;
        _art.Scale = Vector2.One * (overlaySpan / Mathf.Max(overlayTexture.GetWidth(), overlayTexture.GetHeight()));
        _art.Modulate = new Color(1f, 1f, 1f, GemSprites.OverlayAlpha(SpecialKind));

        var outlineTexture = GemSprites.OutlineFor(SpecialKind);
        if (outlineTexture is null) return;
        _silhouette.Texture = outlineTexture;
        _silhouette.Scale = Vector2.One * (overlaySpan / Mathf.Max(outlineTexture.GetWidth(), outlineTexture.GetHeight()));
        _silhouette.Modulate = new Color(1f, 1f, 1f, GemSprites.OutlineAlpha(SpecialKind));
    }

    private void EnsureReady()
    {
        if (_base is null) _Ready(); // Configure may run before the node entered the tree
    }
}