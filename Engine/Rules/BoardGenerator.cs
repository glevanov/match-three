using MatchThree.Engine.Model;

namespace MatchThree.Engine.Rules;

public sealed class BoardGenerator
{
    private readonly int width;
    private readonly int height;
    private readonly SeededRandom rng;
    private readonly IdSource idSource;

    public BoardGenerator(int width, int height, int gemTypeCount, SeededRandom rng, IdSource idSource)
    {
        this.width = width;
        this.height = height;
        this.rng = rng;
        this.idSource = idSource;
    }

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

        if (col >= 2)
        {
            var left1 = cells[row, col - 1]?.Type;
            if (left1 is not null && left1 == cells[row, col - 2]?.Type) forbidden.Add(left1.Value);
        }

        if (row >= 2)
        {
            var up1 = cells[row - 1, col]?.Type;
            if (up1 is not null && up1 == cells[row - 2, col]?.Type) forbidden.Add(up1.Value);
        }

        var allowed = Enum.GetValues<GemType>().Where(t => !forbidden.Contains(t)).ToArray();
        return allowed[rng.NextInt(allowed.Length)];
    }
}