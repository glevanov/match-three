using Godot;

namespace MatchThree.View;

/// <summary>
/// Full-screen photo background for the board scene (Game.tscn): one of four
/// night-sky photos, chosen randomly at the start of the round. Lives on its
/// own canvas layer BELOW the board, HUD and game-over overlay, so it only
/// ever shows while the board is up — the menu scene has its own flat color
/// and never loads these photos.
///
/// Positioning: the node is a CanvasLayer at layer -10 (below the root
/// canvas's 0 and the HUD/GameOver layers at 1), and its TextureRect child
/// is anchored full-rect to the viewport with IgnoreSize +
/// KeepAspectCovered. The photos are landscape (full-res Pixabay originals,
/// 2560 px long side); the viewport is the 540x960 design canvas, height-expanded on tall
/// phones (stretch/aspect = expand), which is far more portrait than the
/// photos — KeepAspectCovered scales the photo to fill the viewport and
/// crops the best-fitting center slice, so the image never stretches or
/// letterboxes. The board stays centered on top; the photo shows in the
/// margins, dimmed behind the HUD strip's translucent backdrop.
///
/// A new photo is picked on scene entry and again on every RoundStarted
/// ("Play again"), so each round gets a fresh background. Reusing the
/// same photo twice in a row is allowed (uniform pick, 4 options).
/// </summary>
public partial class Background : CanvasLayer
{
    /// <summary>The night-sky photos, one picked per round (provenance:
    /// docs/ASSET_SOURCES.md). Kept in this order so the pick is stable
    /// across sessions.</summary>
    private static readonly string[] Photos =
    {
        "res://Assets/Backgrounds/starry_night.jpg",
        "res://Assets/Backgrounds/starry_sky.jpg",
        "res://Assets/Backgrounds/milky_way.jpg",
        "res://Assets/Backgrounds/cosmos.jpg",
    };

    private TextureRect _texture = null!;

    public override void _Ready()
    {
        Layer = -10;

        _texture = new TextureRect();
        AddChild(_texture);
        _texture.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _texture.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _texture.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
        _texture.MouseFilter = Control.MouseFilterEnum.Ignore;

        PickRandom();
        // Play again / new round = new photo. (Menu → StartRound fires
        // RoundStarted before this scene exists; the _Ready pick above covers
        // that path.)
        Game.Instance.RoundStarted += PickRandom;
    }

    public override void _ExitTree()
    {
        // The autoload outlives the scene; don't leave a dangling hook on
        // the way back to the menu.
        if (Game.Instance is { } game) game.RoundStarted -= PickRandom;
    }

    /// <summary>Applies one of the photos, chosen uniformly at random.</summary>
    private void PickRandom()
    {
        _texture.Texture = GD.Load<Texture2D>(Photos[System.Random.Shared.Next(Photos.Length)]);
    }
}