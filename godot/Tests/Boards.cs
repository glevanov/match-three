using MatchThree.Engine.Model;

namespace MatchThree.Engine.Tests;

/// <summary>
/// Builds a <see cref="Board"/> from rows of single-character codes for readable
/// test fixtures: R=RED, G=GREEN, B=BLUE, Y=YELLOW, A/other=PURPLE or ORANGE via
/// <see cref="CharToType"/>, '.' = empty cell. Gem ids are assigned row-major
/// starting at <paramref name="idOffset"/>.
/// </summary>
public static class Boards
{
    public static Board FromRows(params string[] rows) => FromRows(rows.ToList());

    public static Board FromRows(IEnumerable<string> rows, int idOffset = 0)
    {
        var rowList = rows.ToList();
        if (rowList.Count == 0)
            throw new ArgumentException("at least one row", nameof(rows));
        var width = rowList[0].Length;
        if (rowList.Any(r => r.Length != width))
            throw new ArgumentException("all rows must have equal width", nameof(rows));

        var nextId = idOffset;
        return Board.Create(width, rowList.Count, pos =>
        {
            var type = CharToType(rowList[pos.Row][pos.Col]);
            return type is { } t ? new Gem(nextId++, t) : (Gem?)null;
        });
    }

    public static GemType? CharToType(char c) => c switch
    {
        'R' => GemType.Red,
        'G' => GemType.Green,
        'B' => GemType.Blue,
        'Y' => GemType.Yellow,
        'P' or 'A' => GemType.Purple,
        'O' => GemType.Orange,
        '.' => null,
        _ => throw new ArgumentException($"unknown gem char: {c}"),
    };

    /// <summary>Reverse of <see cref="CharToType"/>; PURPLE renders as 'P'.</summary>
    public static char TypeToChar(GemType type) => type switch
    {
        GemType.Red => 'R',
        GemType.Green => 'G',
        GemType.Blue => 'B',
        GemType.Yellow => 'Y',
        GemType.Purple => 'P',
        GemType.Orange => 'O',
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}