namespace MatchThree.Engine.Model;

/// <summary>
/// A gem on the board.
/// </summary>
/// <param name="Id">
/// Stable identity across falls, spawns, and cascades so the UI can track gems
/// while animating. Never reused within a game session.
/// </param>
/// <param name="Type">Color/basic type.</param>
/// <param name="Special">
/// Special kind when the gem is a transformed special (Flame/Star/Hypercube),
/// or null for a plain gem.
/// </param>
/// <remarks>
/// A <see cref="Gem"/> is a value type (record struct), which is what makes the
/// <see cref="Board"/> copy semantics safe: a grid clone is automatically a
/// deep copy, no gem aliasing possible.
/// </remarks>
public readonly record struct Gem(int Id, GemType Type, Special? Special = null);