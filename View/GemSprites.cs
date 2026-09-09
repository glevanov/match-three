using Godot;
using MatchThree.Engine.Model;

namespace MatchThree.View;

/// <summary>
/// Loads the PNG art bundled in Assets/Sprites and exposes it: one texture per
/// GemType, a standalone Hypercube sprite (colorless per MECHANICS.md), and
/// the Star's overlay art.
///
/// - Orange renders its own art again (user direction): Assets/Sprites/
///   orange.png is a 256px downscale of assets/orange.png. (A placeholder
///   era rendered orange gems white because the old orange art read almost
///   the same as yellow on the board.)
/// - Flame has no overlay art: the gem_fire.gdshader aura is its cue and the
///   center-icon art is retired (docs/DECISIONS.md "Flame aura (visual)").
///   Only Star keeps an overlay — Kenney star_06, zoomed ~2.2x so its bright
///   cross fills the gem, at 30% opacity, no outline (user direction).
/// </summary>
public static class GemSprites
{
    /// <summary>Star overlay alpha (see-through glint over the gem).</summary>
    public const float StarOverlayAlpha = 0.30f;

    /// <summary>Fraction of the cell a gem sprite spans; the rest is gutter between gems.</summary>
    public const float GemWidthFraction = 0.94f;

    /// <summary>Star overlay zoom: star_06's bright cross spans only ~43% of
    /// its texture (soft alpha falloff), so drawing it at 1x leaves a small
    /// star on the gem. 2.2x brings the cross to the gem's full width and lets
    /// the soft ray tails fade just past the gem edge.</summary>
    public const float StarOverlayFraction = 2.2f;

    private static Dictionary<GemType, Texture2D>? _color;
    private static Texture2D? _hypercube;
    private static Dictionary<Special, Texture2D>? _art;
    private static Dictionary<GemType, (Color Core, Color Rim)>? _aura;

    /// <summary>Loads all sprites once (cheap, synchronous; called by BoardView._Ready).</summary>
    public static void EnsureLoaded()
    {
        if (_color is not null) return;

        _color = new Dictionary<GemType, Texture2D>
        {
            [GemType.Red] = Load("red.png"),
            [GemType.Green] = Load("green.png"),
            [GemType.Blue] = Load("blue.png"),
            [GemType.Yellow] = Load("yellow.png"),
            [GemType.Purple] = Load("purple.png"),
            [GemType.Orange] = Load("orange.png"), // real orange art again (see class docs)
        };
        _hypercube = Load("hypercube.png");

        var star = Load("star_06.png");
        _art = new Dictionary<Special, Texture2D>
        {
            // Star is the only special with overlay art: Flame's center-icon
            // art is retired — the aura alone is its cue (docs/DECISIONS.md
            // "Flame aura (visual)") — and the star art gets no baked outline
            // (user direction: no border).
            [Special.Star] = star,
        };

        // Per-gem aura tints for the flame shader, derived from each gem's own
        // art (docs/DECISIONS.md "Flame aura (visual)"): a blue gem gets a light
        // blue glow. Base color is the mean over opaque pixels (the center
        // pixel skews to the art's highlight); rim = base lightened 15%,
        // core = base lightened 55% toward white — the "glowing" version.
        _aura = _color.ToDictionary(kv => kv.Key, kv => AuraFrom(kv.Value));
    }

    /// <summary>Base sprite for a gem: the Hypercube art, or the color sprite.</summary>
    public static Texture2D BaseFor(GemType type, Special? special)
    {
        EnsureLoaded();
        return special == Special.Hypercube ? _hypercube! : _color![type];
    }

    /// <summary>Center overlay art for Star (the only special with an overlay), or null.</summary>
    public static Texture2D? ArtFor(Special? special)
    {
        EnsureLoaded();
        return special == Special.Star ? _art![special.Value] : null;
    }

    /// <summary>Flame-aura colors for a gem type: core (inner glow, light) and
    /// rim (edge, closer to the gem's own color). Cached from the art.</summary>
    public static (Color Core, Color Rim) AuraColors(GemType type)
    {
        EnsureLoaded();
        return _aura![type];
    }

    private static (Color Core, Color Rim) AuraFrom(Texture2D texture)
    {
        var image = texture.GetImage();
        var w = image.GetWidth();
        var h = image.GetHeight();
        double r = 0, g = 0, b = 0;
        long n = 0;
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var p = image.GetPixel(x, y);
                if (p.A <= 0.5f) continue;
                r += p.R;
                g += p.G;
                b += p.B;
                n++;
            }
        }
        var baseColor = new Color((float)(r / n), (float)(g / n), (float)(b / n), 1f);
        return (baseColor.Lerp(Colors.White, 0.55f), baseColor.Lerp(Colors.White, 0.15f));
    }

    /// <summary>Size of the Star overlay as a fraction of the base sprite.</summary>
    public static float OverlayFraction(Special? special) =>
        special == Special.Star ? StarOverlayFraction : 1f;

    /// <summary>Alpha of the overlay (Star see-through; other specials have none).</summary>
    public static float OverlayAlpha(Special? special) =>
        special == Special.Star ? StarOverlayAlpha : 1f;

    private static Texture2D Load(string name) =>
        GD.Load<Texture2D>($"res://Assets/Sprites/{name}");
}