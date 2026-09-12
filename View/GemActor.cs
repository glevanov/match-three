using Godot;
using MatchThree.Engine.Model;

namespace MatchThree.View;

public partial class GemActor : Node2D
{
    private const float AuraRectScale = 1.35f;

    private const float StarGlowRectScale = 1.5f;

    private Sprite2D _base = null!;
    private Sprite2D _art = null!;
    private ColorRect _fireRing = null!;
    private ShaderMaterial _fireRingMaterial = null!;
    private ColorRect _starGlow = null!;
    private ShaderMaterial _starGlowMaterial = null!;
    private float _cellSizePx;

    public int GemId { get; private set; }

    public GemType Type { get; private set; }

    public Special? SpecialKind { get; private set; }

    public override void _Ready()
    {
        _base = GetNode<Sprite2D>("Base");
        var overlay = GetNode<Node2D>("Overlay");
        _art = overlay.GetNode<Sprite2D>("Art");

        _fireRing = GetNode<ColorRect>("FireRing");
        _fireRing.Material = (ShaderMaterial)((ShaderMaterial)_fireRing.Material).Duplicate();
        _fireRingMaterial = (ShaderMaterial)_fireRing.Material;

        _starGlow = GetNode<ColorRect>("StarGlow");
        _starGlow.Material = (ShaderMaterial)((ShaderMaterial)_starGlow.Material).Duplicate();
        _starGlowMaterial = (ShaderMaterial)_starGlow.Material;
    }

    public void Configure(int gemId, GemType type, Special? special, float cellSizePx)
    {
        GemId = gemId;
        Type = type;
        _cellSizePx = cellSizePx;
        SetSpecial(special);
    }

    public void SetSpecial(Special? special)
    {
        SpecialKind = special;
        ApplyAppearance();
    }

    public async Task MoveToAsync(Vector2 target, float durationMs)
    {
        EnsureReady();
        var tween = CreateTween();
        tween.TweenProperty(this, "position", target, durationMs / 1000f)
             .SetTrans(Tween.TransitionType.Cubic)
             .SetEase(Tween.EaseType.Out);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

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

        var baseTexture = GemSprites.BaseFor(Type, SpecialKind);
        _base.Texture = baseTexture;
        var scale = span / Mathf.Max(baseTexture.GetWidth(), baseTexture.GetHeight());
        _base.Scale = Vector2.One * scale;

        if (SpecialKind == Special.Flame)
        {
            var fireSize = new Vector2(baseTexture.GetWidth(), baseTexture.GetHeight()) * scale * AuraRectScale;
            _fireRing.Size = fireSize;
            _fireRing.Position = -fireSize / 2f;
            _fireRingMaterial.SetShaderParameter("mask_texture", baseTexture);
            _fireRingMaterial.SetShaderParameter("rect_scale", AuraRectScale);
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

        var overlayTexture = SpecialKind == Special.Star ? GemSprites.ArtFor(Special.Star) : null;
        _art.Visible = overlayTexture is not null;
        if (overlayTexture is null)
        {
            _starGlow.Visible = false;
            return;
        }

        var overlaySpan = span * GemSprites.OverlayFraction(SpecialKind);
        _art.Texture = overlayTexture;
        _art.Scale = Vector2.One * (overlaySpan / Mathf.Max(overlayTexture.GetWidth(), overlayTexture.GetHeight()));
        _art.Modulate = new Color(1f, 1f, 1f, GemSprites.OverlayAlpha(SpecialKind));

        if (SpecialKind == Special.Star)
        {
            var glowSize = new Vector2(overlayTexture.GetWidth(), overlayTexture.GetHeight())
                * (overlaySpan / Mathf.Max(overlayTexture.GetWidth(), overlayTexture.GetHeight()))
                * StarGlowRectScale;
            _starGlow.Size = glowSize;
            _starGlow.Position = -glowSize / 2f;
            _starGlowMaterial.SetShaderParameter("mask_texture", overlayTexture);
            _starGlowMaterial.SetShaderParameter("rect_scale", StarGlowRectScale);
            _starGlowMaterial.SetShaderParameter("time_offset", (GemId * 37 % 1000) / 1000f * Mathf.Tau);
            _starGlow.Visible = true;
        }
        else
        {
            _starGlow.Visible = false;
        }
    }

    private void EnsureReady()
    {
        if (_base is null) _Ready(); // Configure may run before the node entered the tree
    }
}