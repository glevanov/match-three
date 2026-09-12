using Godot;
using MatchThree.Engine.Model;

namespace MatchThree.View;

public static class GemSprites
{
    public const float StarOverlayAlpha = 0.30f;

    public const float GemWidthFraction = 0.94f;

    public const float StarOverlayFraction = 2.2f;

    private static Dictionary<GemType, Texture2D>? _color;
    private static Texture2D? _hypercube;
    private static Dictionary<Special, Texture2D>? _art;
    private static Dictionary<GemType, (Color Core, Color Rim)>? _aura;

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
            [GemType.Orange] = Load("orange.png"),
        };
        _hypercube = Load("hypercube.png");

        var star = Load("star_06.png");
        _art = new Dictionary<Special, Texture2D>
        {
            [Special.Star] = star,
        };

        _aura = _color.ToDictionary(kv => kv.Key, kv => AuraFrom(kv.Value));
    }

    public static Texture2D BaseFor(GemType type, Special? special)
    {
        EnsureLoaded();
        return special == Special.Hypercube ? _hypercube! : _color![type];
    }

    public static Texture2D? ArtFor(Special? special)
    {
        EnsureLoaded();
        return special == Special.Star ? _art![special.Value] : null;
    }

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

    public static float OverlayFraction(Special? special) =>
        special == Special.Star ? StarOverlayFraction : 1f;

    public static float OverlayAlpha(Special? special) =>
        special == Special.Star ? StarOverlayAlpha : 1f;

    private static Texture2D Load(string name) =>
        GD.Load<Texture2D>($"res://Assets/Sprites/{name}");
}