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
///   ├── FireRing (ColorRect)     procedural shader aura over the gem body,
///   │                            Flame gems only (same span as the gem)
///   └── Overlay (Node2D)
///       ├── Art (Sprite2D)       Star sparkle art (Flame dropped its icon)
///       └── Silhouette (Sprite2D) baked alpha-silhouette outline
///
/// Animations are Tween-driven; the async methods complete when the tween's
/// Finished signal fires, so StepPlayer can sequence steps with async/await.
/// Easing: Godot's Cubic+Out (fast-out-slow-in style curve).
/// </summary>
public partial class GemActor : Node2D
{
    /// <summary>FireRing rect vs the base sprite's footprint. The shader maps
    /// texUV = (UV - 0.5) / rect_scale + 0.5, so the rect is a zoomed-out
    /// view of the gem texture: margin around the silhouette lets the soft
    /// halo render past the gem edge without being clipped (uniform set from
    /// here so the two stay in sync).</summary>
    private const float AuraRectScale = 1.35f;

    private Sprite2D _base = null!;
    private Sprite2D _art = null!;
    private Sprite2D _silhouette = null!;
    private ColorRect _fireRing = null!;
    private ShaderMaterial _fireRingMaterial = null!;
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

        // Per-instance material: every GemActor instantiates the same .tscn, so
        // the shared ShaderMaterial must be duplicated or setting time_offset on
        // one gem would desync (and then lock-step) all Flame gems at once.
        _fireRing = GetNode<ColorRect>("FireRing");
        _fireRing.Material = (ShaderMaterial)((ShaderMaterial)_fireRing.Material).Duplicate();
        _fireRingMaterial = (ShaderMaterial)_fireRing.Material;
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
        // Shared scale: Base and the fire-aura rect must use the SAME scale so
        // FireRing occupies exactly Base's drawn footprint (texture dims x
        // scale, preserving each gem's real aspect ratio) and the shader's UVs
        // line up 1:1 with baseTexture's pixels for the alpha mask below.
        var scale = span / Mathf.Max(baseTexture.GetWidth(), baseTexture.GetHeight());
        _base.Scale = Vector2.One * scale;

        // Fire aura: shader covering the gem's own body (Flame only).
        // Evaluated before the overlay early-return so every appearance state
        // ends with the aura either on or off. The shader masks with
        // baseTexture's alpha (docs/DECISIONS.md "Flame aura (visual)"), so
        // the flame band traces each GemType's actual silhouette; the rect is
        // AuraRectScale x Base's footprint to leave margin for the soft halo.
        if (SpecialKind == Special.Flame)
        {
            var fireSize = new Vector2(baseTexture.GetWidth(), baseTexture.GetHeight()) * scale * AuraRectScale;
            _fireRing.Size = fireSize;
            _fireRing.Position = -fireSize / 2f;
            _fireRingMaterial.SetShaderParameter("mask_texture", baseTexture);
            _fireRingMaterial.SetShaderParameter("rect_scale", AuraRectScale);
            // Per-gem aura tint: a blue gem glows light blue (GemSprites.
            // AuraColors derives core/rim from the gem's own art).
            var (coreColor, rimColor) = GemSprites.AuraColors(Type);
            _fireRingMaterial.SetShaderParameter("core_color", coreColor);
            _fireRingMaterial.SetShaderParameter("rim_color", rimColor);
            _fireRingMaterial.SetShaderParameter("time_offset", (GemId * 37 % 1000) / 1000f * Mathf.Tau);
            _fireRing.Visible = true;
        }
        else
        {
            _fireRing.Visible = false;
        }

        // Overlay: the aura alone is now the "on fire" cue, so Flame dropped
        // its center icon (docs/DECISIONS.md "Flame aura (visual)"); only Star
        // keeps a separate see-through overlay (55% opacity, full gem).
        var overlayTexture = SpecialKind == Special.Star ? GemSprites.ArtFor(Special.Star) : null;
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