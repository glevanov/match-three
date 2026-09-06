using MatchThree.Engine.Model;

namespace MatchThree.Engine.Rules;

/// <summary>
/// Generates fresh boards that satisfy MECHANICS.md invariants:
/// - no pre-existing match (guaranteed by placement rule), and
/// - at least one legal move (validated; regenerate while none exists).
///
/// Placement: left-to-right, top-to-bottom, excluding any color that would extend
/// a run of 2 with the already-placed neighbors above/left.
/// </summary>
public sealed class BoardGenerator
{
    private readonly int width;
    private readonly int height;
    private readonly SeededRandom rng;
    private readonly IdSource idSource;

    /// <param name="gemTypeCount">Accepted for signature parity with the Kotlin original; the Kotlin
    /// generator also ignores it (colors draw from all GemType entries).</param>
    public BoardGenerator(int width, int height, int gemTypeCount, SeededRandom rng, IdSource idSource)
    {
        this.width = width;
        this.height = height;
        this.rng = rng;
        this.idSource = idSource;
    }

    /// <summary>A fresh board satisfying the generation invariants.</summary>
    public Board NewBoard()
    {
        while (true)
        {
            var board = GenerateOnce();
            if (LegalMoveDetector.HasLegalMove(board)) return board;
        }
    }

    private Board GenerateOnce()
    {
        var cells = new Gem?[height, width];
        for (var row = 0; row < height; row++)
        {
            for (var col = 0; col < width; col++)
            {
                var type = PickTypeAvoidingRun(cells, row, col);
                cells[row, col] = new Gem(idSource.Next(), type);
            }
        }
        return Board.Of(width, height, cells);
    }

    private GemType PickTypeAvoidingRun(Gem?[,] cells, int row, int col)
    {
        var forbidden = new HashSet<GemType>();

        // Left: cells at (row, col-2) and (row, col-1) both present and equal?
        if (col >= 2)
        {
            var left1 = cells[row, col - 1]?.Type;
            if (left1 is not null && left1 == cells[row, col - 2]?.Type) forbidden.Add(left1.Value);
        }

        // Above: cells at (row-2, col) and (row-1, col) both present and equal?
        if (row >= 2)
        {
            var up1 = cells[row - 1, col]?.Type;
            if (up1 is not null && up1 == cells[row - 2, col]?.Type) forbidden.Add(up1.Value);
        }

        var allowed = Enum.GetValues<GemType>().Where(t => !forbidden.Contains(t)).ToArray();
        return allowed[rng.NextInt(allowed.Length)];
    }
}