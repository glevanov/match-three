using Godot;

namespace MatchThree.View;

/// <summary>
/// Autoload singleton that replaces the Kotlin GameViewModel (StateFlow ->
/// Godot signals). Placeholder for G0; the engine wiring lands in G2.
/// </summary>
public partial class Game : Node
{
    public override void _Ready()
    {
        GD.Print("MatchThree autoload ready");
    }
}