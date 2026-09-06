using Godot;
using MatchThree.Engine.Model;

namespace MatchThree.View;

/// <summary>
/// Loads the PNG art bundled in Assets/Sprites and exposes it: one texture per
/// GemType, a standalone Hypercube sprite (colorless per MECHANICS.md), and
/// Flame/Star overlay art whose alpha silhouettes are traced once into outline
/// textures.
///
/// - Orange renders with white.png — the orange art reads almost the same as
///   yellow on the board; the gem type stays Orange logically.
/// - Flame overlay is a small centered inset (0.45 of the gem); the Star
///   sparkle spans the gem at 55% opacity with a 70%-alpha white outline.
/// </summary>
public static class GemSprites
{
    /// <summary>Star overlay alpha (see-through sparkle over the gem).</summary>
    public const float StarOverlayAlpha = 0.55f;

    /// <summary>Star outline alpha — readable on any base color.</summary>
    public const float StarOutlineAlpha = 0.7f;

    /// <summary>Flame overlay size as a fraction of the gem (inset keeps it inside).</summary>
    public const float FlameOverlayFraction = 0.45f;

    /// <summary>Fraction of the cell a gem sprite spans; the rest is gutter between gems.</summary>
    public const float GemWidthFraction = 0.94f;

    private static Dictionary<GemType, Texture2D>? _color;
    private static Texture2D? _hypercube;
    private static Dictionary<Special, Texture2D>? _art;
    private static Dictionary<Special, Texture2D>? _outline;

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
            [GemType.Orange] = Load("white.png"), // see class docs
        };
        _hypercube = Load("hypercube.png");

        var flame = Load("flame.png");
        var sparkle = Load("sparkle.png");
        _art = new Dictionary<Special, Texture2D>
        {
            [Special.Flame] = flame,
            [Special.Star] = sparkle,
        };
        _outline = new Dictionary<Special, Texture2D>
        {
            // Trace the alpha silhouette and stroke it black (flame) / white at
            // 0.7 (star); bake that stroke into textures once.
            [Special.Flame] = BakeSilhouette(flame, Colors.Black, 1f),
            [Special.Star] = BakeSilhouette(sparkle, Colors.White, StarOutlineAlpha),
        };
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
        return special is Special.Flame or Special.Star ? _outline![special.Value] : null;
    }

    /// <summary>Size of the overlay as a fraction of the base sprite.</summary>
    public static float OverlayFraction(Special? special) =>
        special == Special.Flame ? FlameOverlayFraction : 1f;

    /// <summary>Alpha of the overlay (Star see-through, Flame solid).</summary>
    public static float OverlayAlpha(Special? special) =>
        special == Special.Star ? StarOverlayAlpha : 1f;

    /// <summary>Alpha of the silhouette outline.</summary>
    public static float OutlineAlpha(Special? special) =>
        special == Special.Star ? StarOutlineAlpha : 1f;

    private static Texture2D Load(string name) =>
        GD.Load<Texture2D>($"res://Assets/Sprites/{name}");

    /// <summary>
    /// Traces the outer boundary where <paramref name="texture"/>'s alpha channel
    /// transitions from transparent to opaque, dilates it one pixel (a ~2-3px
    /// stroke) and bakes it into a
    /// texture of <paramref name="color"/> at <paramref name="alpha"/>.
    /// </summary>
    private static Texture2D BakeSilhouette(Texture2D texture, Color color, float alpha)
    {
        var image = texture.GetImage();
        var w = image.GetWidth();
        var h = image.GetHeight();

        bool Opaque(int x, int y) => x >= 0 && x < w && y >= 0 && y < h && image.GetPixel(x, y).A > 0f;

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