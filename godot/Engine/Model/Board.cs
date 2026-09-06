namespace MatchThree.Engine.Model;

/// <summary>
/// An immutable 9x9 (or config-sized) grid of gems. <c>null</c> marks an empty
/// cell (e.g. a cell awaiting a spawned gem). Engine passes return snapshots;
/// the grid itself is never mutated after construction.
/// </summary>
/// <remarks>
/// Immutability contract (GODOT_PORT_PLAN §7): <c>Gem</c> is a value type
/// (readonly record struct), so cloning the <see cref="Gem?[,]"/> grid is
/// automatically a deep copy — <c>WithSwapped</c>/<c>WithGem</c> always
/// allocate a fresh grid and never mutate in place, exactly like the Kotlin
/// Board. The grid field is internal: only engine code (same assembly) may
/// read it for gravity/refill snapshots; external callers go through the
/// public API.
/// </remarks>
public sealed class Board
{
    public int Width { get; }
    public int Height { get; }

    internal Gem?[,] Cells { get; }

    private Board(int width, int height, Gem?[,] cells)
    {
        if (cells.GetLength(0) != height)
            throw new ArgumentException($"cells rows != height ({cells.GetLength(0)} != {height})", nameof(cells));
        for (var row = 0; row < height; row++)
        {
            if (cells.GetLength(1) != width)
                throw new ArgumentException($"cells cols != width ({cells.GetLength(1)} != {width})", nameof(cells));
        }

        Width = width;
        Height = height;
        Cells = cells;
    }

    /// <summary>The gem at (row, col), or null for an empty/out-of-bounds cell.</summary>
    public Gem? GemAt(int row, int col) =>
        row >= 0 && row < Height && col >= 0 && col < Width ? Cells[row, col] : null;

    /// <summary>The gem at <paramref name="position"/>, or null for an empty/out-of-bounds cell.</summary>
    public Gem? GemAt(Position position) => GemAt(position.Row, position.Col);

    /// <summary>The position holding the gem with <paramref name="gemId"/>, or null when absent.</summary>
    /// <remarks>
    /// Not LINQ FirstOrDefault: for a value-type Position, "not found" would be
    /// indistinguishable from the real cell (0,0) once widened to Position?.
    /// </remarks>
    public Position? PositionOf(int gemId)
    {
        foreach (var p in Positions())
        {
            if (GemAt(p) is { } gem && gem.Id == gemId) return p;
        }
        return null;
    }

    /// <summary>True when <paramref name="position"/> lies inside the board.</summary>
    public bool IsInside(Position position) =>
        position.Row >= 0 && position.Row < Height && position.Col >= 0 && position.Col < Width;

    /// <summary>All board positions, row-major.</summary>
    public IEnumerable<Position> Positions()
    {
        for (var row = 0; row < Height; row++)
        {
            for (var col = 0; col < Width; col++)
            {
                yield return new Position(row, col);
            }
        }
    }

    /// <summary>A deep-copied board with the two positions swapped.</summary>
    public Board WithSwapped(Position a, Position b)
    {
        if (!IsInside(a) || !IsInside(b))
            throw new ArgumentException($"swap positions must be inside the board: {a}, {b}");

        var copy = CopyCells();
        var gemA = copy[a.Row, a.Col];
        copy[a.Row, a.Col] = copy[b.Row, b.Col];
        copy[b.Row, b.Col] = gemA;
        return new Board(Width, Height, copy);
    }

    /// <summary>A deep-copied board with <paramref name="gem"/> placed at <paramref name="position"/>.</summary>
    public Board WithGem(Position position, Gem? gem)
    {
        if (!IsInside(position))
            throw new ArgumentException($"position must be inside the board: {position}");

        var copy = CopyCells();
        copy[position.Row, position.Col] = gem;
        return new Board(Width, Height, copy);
    }

    /// <summary>
    /// Fresh grid clone. Cells hold value-type gems, so this is a full deep
    /// copy — the returned array shares nothing with the original.
    /// </summary>
    internal Gem?[,] CopyCells() => (Gem?[,])Cells.Clone();

    /// <summary>Wraps a cell grid. The grid must match <paramref name="width"/> x <paramref name="height"/>.</summary>
    public static Board Of(int width, int height, Gem?[,] cells) => new(width, height, cells);

    /// <summary>A board builder; <paramref name="factory"/> returns the gem for each position (row-major).</summary>
    public static Board Create(int width, int height, Func<Position, Gem?> factory)
    {
        var cells = new Gem?[height, width];
        for (var row = 0; row < height; row++)
        {
            for (var col = 0; col < width; col++)
            {
                cells[row, col] = factory(new Position(row, col));
            }
        }
        return new Board(width, height, cells);
    }
}