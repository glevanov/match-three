using Godot;

namespace MatchThree.View;

public partial class Background : CanvasLayer
{
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
        _texture.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _texture.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _texture.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
        _texture.MouseFilter = Control.MouseFilterEnum.Ignore;

        PickRandom();
        Game.Instance.RoundStarted += PickRandom;
    }

    public override void _ExitTree()
    {
        if (Game.Instance is { } game) game.RoundStarted -= PickRandom;
    }

    private void PickRandom()
    {
        _texture.Texture = GD.Load<Texture2D>(Photos[System.Random.Shared.Next(Photos.Length)]);
    }
}