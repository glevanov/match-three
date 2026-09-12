using Godot;
using MatchThree.Engine.Model;

namespace MatchThree.View;

public partial class DebugStarGlow : Node2D
{
    private const string DebugMenuScenePath = "res://Scenes/DebugMenu.tscn";

    private static readonly PackedScene GemActorScene =
        GD.Load<PackedScene>("res://Scenes/GemActor.tscn");

    private const float CellPx = 140f;

    private const float Pitch = CellPx;

    private static readonly (GemType Type, Special? Special, int Id)[] Grid =
    {
        (GemType.Red, Special.Star, 12),
        (GemType.Orange, Special.Star, 512),
        (GemType.Blue, Special.Star, 7),
        (GemType.Red, Special.Flame, 20),
        (GemType.Orange, Special.Flame, 21),
        (GemType.Blue, Special.Flame, 22),
        (GemType.Red, null, 30),
        (GemType.Orange, null, 31),
        (GemType.Blue, null, 32),
    };

    private static readonly string[] ColumnTags = { "red", "orange", "blue" };

    public override void _Ready()
    {
        var bg = new ColorRect
        {
            Color = new Color(0.07f, 0.09f, 0.13f),
            Position = Vector2.Zero,
            Size = GetViewportRect().Size,
        };
        AddChild(bg);

        var top = SafeArea.TopInsetPx + SafeArea.MarginPx;
        AddChild(MakeBackButton(new Vector2(20f, top)));

        var vp = GetViewportRect().Size;
        var headerY = top + 72f;
        AddChild(MakeLabel(20, $"Effect debug 3x3 · cell {CellPx:0}px (game ≈ 57px)", new Vector2(vp.X / 2f, headerY), 700));
        AddChild(MakeLabel(15, "top: star gems · middle: flame gems · bottom: plain", new Vector2(vp.X / 2f, headerY + 28f), 700));

        var origin = new Vector2((vp.X - 3f * Pitch) / 2f, (vp.Y - 3f * Pitch) / 2f + 20f);

        for (var i = 0; i < Grid.Length; i++)
        {
            var (type, special, id) = Grid[i];
            var col = i % 3;
            var row = i / 3;
            var pos = origin + new Vector2((col + 0.5f) * Pitch, (row + 0.5f) * Pitch);

            var gem = GemActorScene.Instantiate<GemActor>();
            AddChild(gem); // on-tree first so _Ready ran before Configure touches nodes
            gem.Configure(id, type, special, CellPx);
            gem.Position = pos;
        }

        for (var col = 0; col < 3; col++)
        {
            AddChild(MakeLabel(15, ColumnTags[col],
                new Vector2(origin.X + (col + 0.5f) * Pitch, origin.Y + 3.2f * Pitch), 260));
        }
    }

    private Button MakeBackButton(Vector2 pos)
    {
        var button = new Button
        {
            Text = "Back",
            Position = pos,
            Size = new Vector2(96f, 48f),
        };
        button.AddThemeFontSizeOverride("font_size", 22);
        button.Pressed += () => GetTree().ChangeSceneToFile(DebugMenuScenePath);
        return button;
    }

    private static Label MakeLabel(int size, string text, Vector2 pos, float width)
    {
        var label = new Label { Text = text, Position = pos - new Vector2(width / 2f, 0f), Size = new Vector2(width, 0f) };
        label.AddThemeFontSizeOverride("font_size", size);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        return label;
    }
}
