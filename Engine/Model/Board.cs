namespace MatchThree.Engine.Model;

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

    public Gem? GemAt(int row, int col) =>
        row >= 0 && row < Height && col >= 0 && col < Width ? Cells[row, col] : null;

    public Gem? GemAt(Position position) => GemAt(position.Row, position.Col);

    public Position? PositionOf(int gemId)
    {
        foreach (var p in Positions())
        {
            if (GemAt(p) is { } gem && gem.Id == gemId) return p;
        }
        return null;
    }

    public bool IsInside(Position position) =>
        position.Row >= 0 && position.Row < Height && position.Col >= 0 && position.Col < Width;

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

    public Board WithGem(Position position, Gem? gem)
    {
        if (!IsInside(position))
            throw new ArgumentException($"position must be inside the board: {position}");

        var copy = CopyCells();
        copy[position.Row, position.Col] = gem;
        return new Board(Width, Height, copy);
    }

    internal Gem?[,] CopyCells() => (Gem?[,])Cells.Clone();

    public static Board Of(int width, int height, Gem?[,] cells) => new(width, height, cells);

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