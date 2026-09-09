using Godot;
using MatchThree.Engine.Model;

namespace MatchThree.View;

/// <summary>
/// Debug-only 3x3 effect screen: big static gems on a dark backdrop so the
/// special effects can be eyeballed side by side (gem_star.gdshader /
/// gem_fire.gdshader) without playing a full board. Not part of the game
/// flow; launch it by pointing run/main_scene at Scenes/DebugStarGlow.tscn
/// while exporting (then revert the main scene).
///
/// Layout (design units, portrait 540x1200 canvas):
///   top row    : Star gems   (red / orange / blue)
///   middle row : Flame gems  (same colors — compare effects per column)
///   bottom row : plain gems  (same colors — no-effect control)
///   cell = 140px, pitch = cell: gems sit edge-to-edge like the real 9x9
///   board, so the star's ray overflow and the flame halo bleed into
///   neighbours exactly the way they do in game (real cell ≈ 57px).
///
/// GemIds 12/512/7 spread the star pulse phases; 12 and 512 are anti-phase
/// (offset differs by pi), so a bright and a dim Star always coexist.
/// </summary>
public partial class DebugStarGlow : Node2D
{
    private static readonly PackedScene GemActorScene =
        GD.Load<PackedScene>("res://Scenes/GemActor.tscn");

    /// <summary>Gem footprint px (design units) — ~2.5x the real game's ~57px cell.</summary>
    private const float CellPx = 140f;

    /// <summary>Pitch equals the cell: gems sit edge-to-edge like the board.</summary>
    private const float Pitch = CellPx;

    private static readonly (GemType Type, Special? Special, int Id)[] Grid =
    {
        // top row: star
        (GemType.Red, Special.Star, 12),
        (GemType.Orange, Special.Star, 512),
        (GemType.Blue, Special.Star, 7),
        // middle row: flame (same colors — compare effects per column)
        (GemType.Red, Special.Flame, 20),
        (GemType.Orange, Special.Flame, 21),
        (GemType.Blue, Special.Flame, 22),
        // bottom row: plain (no effect control)
        (GemType.Red, null, 30),
        (GemType.Orange, null, 31),
        (GemType.Blue, null, 32),
    };

    private static readonly string[] ColumnTags = { "red", "orange", "blue" };

    public override void _Ready()
    {
        // Dark backdrop so the additive effects read clearly.
        var bg = new ColorRect
        {
            Color = new Color(0.07f, 0.09f, 0.13f),
            Position = Vector2.Zero,
            Size = GetViewportRect().Size,
        };
        AddChild(bg);

        var vp = GetViewportRect().Size;
        AddChild(MakeLabel(20, $"Effect debug 3x3 · cell {CellPx:0}px (game ≈ 57px)", new Vector2(vp.X / 2f, 40f), 700));
        AddChild(MakeLabel(15, "top: star gems · middle: flame gems · bottom: plain", new Vector2(vp.X / 2f, 68f), 700));

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

    private static Label MakeLabel(int size, string text, Vector2 pos, float width)
    {
        var label = new Label { Text = text, Position = pos - new Vector2(width / 2f, 0f), Size = new Vector2(width, 0f) };
        label.AddThemeFontSizeOverride("font_size", size);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        return label;
    }
}