using Godot;
using MatchThree.Engine.Model;

namespace MatchThree.View;

/// <summary>
/// Loads the PNG art bundled in Assets/Sprites and exposes it: one texture per
/// GemType, a standalone Hypercube sprite (colorless per MECHANICS.md), and
/// Flame/Star overlay art whose alpha silhouettes are traced once into outline
/// textures.
///
/// - Orange uses assets/orange.png downscaled to 256px (it used to render with
///   white.png because the old orange art read almost the same as yellow on
///   the board; the real orange is used again now — user direction).
/// - Flame overlay is a small centered inset (0.45 of the gem); the Star's
///   glow-star art (Kenney star_06) is zoomed ~2.2x so its cross fills the
///   whole gem, at 30% opacity, no outline (user direction).
/// </summary>
public static class GemSprites
{
    /// <summary>Star overlay alpha (see-through sparkle over the gem).</summary>
    public const float StarOverlayAlpha = 0.30f;

    /// <summary>Flame overlay size as a fraction of the gem (inset keeps it inside).</summary>
    public const float FlameOverlayFraction = 0.45f;

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
    private static Dictionary<Special, Texture2D>? _outline;
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

        var flame = Load("flame.png");
        var star = Load("star_06.png");
        _art = new Dictionary<Special, Texture2D>
        {
            [Special.Flame] = flame,
            [Special.Star] = star,
        };
        _outline = new Dictionary<Special, Texture2D>
        {
            // Only the Flame silhouette is baked — reserved for its optional
            // center icon (docs/DECISIONS.md "Flame aura (visual)": re-enabling
            // the icon is a one-liner in GemActor.ApplyAppearance). The Star's
            // soft glow art gets NO outline (user direction: no border).
            [Special.Flame] = BakeSilhouette(flame, Colors.Black, 1f),
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

    /// <summary>Center overlay art for Flame/Star, or null.</summary>
    public static Texture2D? ArtFor(Special? special)
    {
        EnsureLoaded();
        return special is Special.Flame or Special.Star ? _art![special.Value] : null;
    }

    /// <summary>Baked alpha-silhouette outline for Flame/Star, or null.</summary>
    public static Texture2D? OutlineFor(Special? special)
    {
        EnsureLoaded();
        if (special is null) return null;
        // Flame's baked icon silhouette (star has no outline — user direction).
        return _outline!.GetValueOrDefault(special.Value);
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

    /// <summary>Size of the overlay as a fraction of the base sprite.</summary>
    public static float OverlayFraction(Special? special) =>
        special switch
        {
            Special.Flame => FlameOverlayFraction,
            Special.Star => StarOverlayFraction,
            _ => 1f,
        };

    /// <summary>Alpha of the overlay (Star see-through, Flame solid).</summary>
    public static float OverlayAlpha(Special? special) =>
        special == Special.Star ? StarOverlayAlpha : 1f;

    /// <summary>Alpha of the silhouette outline (flame's icon stroke, opaque black).</summary>
    public static float OutlineAlpha(Special? special) => 1f;

    private static Texture2D Load(string name) =>
        GD.Load<Texture2D>($"res://Assets/Sprites/{name}");

    /// <summary>
    /// Traces the outer boundary where <paramref name="texture"/>'s alpha channel
    /// transitions from transparent to opaque, dilates it one pixel (a ~2-3px
    /// stroke) and bakes it into a texture of <paramref name="color"/> at
    /// <paramref name="alpha"/>. Used only for Flame's optional icon silhouette
    /// (the Star has no outline); flame edges are hard, so the 0.4 alpha
    /// threshold matches the alpha > 0 boundary.
    /// </summary>
    private static Texture2D BakeSilhouette(Texture2D texture, Color color, float alpha)
    {
        var image = texture.GetImage();
        var w = image.GetWidth();
        var h = image.GetHeight();

        bool Opaque(int x, int y) => x >= 0 && x < w && y >= 0 && y < h && image.GetPixel(x, y).A > 0.4f;

        var boundary = new bool[w, h];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                if (!Opaque(x, y)) continue;
                var isBoundary = x == 0 || y == 0 || x == w - 1 || y == h - 1 ||
                                 !Opaque(x - 1, y) || !Opaque(x + 1, y) ||
                                 !Opaque(x, y - 1) || !Opaque(x, y + 1);
                if (isBoundary) boundary[x, y] = true;
            }
        }

        var outline = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        outline.Fill(new Color(0f, 0f, 0f, 0f));
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                // Boundary pixel or any of its 4-neighbours: a 2-3px stroke.
                if (boundary[x, y] ||
                    (x > 0 && boundary[x - 1, y]) ||
                    (x + 1 < w && boundary[x + 1, y]) ||
                    (y > 0 && boundary[x, y - 1]) ||
                    (y + 1 < h && boundary[x, y + 1]))
                {
                    outline.SetPixel(x, y, new Color(color.R, color.G, color.B, alpha));
                }
            }
        }
        return ImageTexture.CreateFromImage(outline);
    }
}