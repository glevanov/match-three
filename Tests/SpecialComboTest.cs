using MatchThree.Engine.Model;
using MatchThree.Engine.Rules;
using NUnit.Framework;

namespace MatchThree.Engine.Tests;

public class SpecialComboTest
{

    private static GameEngine Engine() => new(new SeededRandom(1L));

    private int nextId;

    private Gem Gem(GemType type, Special? special = null) => new(nextId++, type, special);

    private static Board Board9(IDictionary<Position, Gem> placements)
    {
        var cells = new Gem?[9, 9];
        foreach (var (pos, gem) in placements) cells[pos.Row, pos.Col] = gem;
        return Board.Of(9, 9, cells);
    }

    private static Position Pos(int row, int col) => new(row, col);

    [Test]
    public void FiveRunBirthsAHypercube()
    {
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(0, 0)] = Gem(GemType.Red), [Pos(0, 1)] = Gem(GemType.Red),
            [Pos(0, 2)] = Gem(GemType.Red), [Pos(0, 3)] = Gem(GemType.Red),
            [Pos(0, 4)] = Gem(GemType.Red),
        });
        var matches = MatchDetector.FindMatches(board);
        var birth = SpecialRules.ResolveBirths(board, matches).Single();
        Assert.That(birth.Special, Is.EqualTo(Special.Hypercube));
        Assert.That(birth.Cell, Is.EqualTo(Pos(0, 0)));
    }

    [Test]
    public void TSlashShapeBirthsAStar()
    {
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(0, 0)] = Gem(GemType.Red), [Pos(0, 1)] = Gem(GemType.Red),
            [Pos(0, 2)] = Gem(GemType.Red),
            [Pos(1, 0)] = Gem(GemType.Red), [Pos(2, 0)] = Gem(GemType.Red),
        });
        var matches = MatchDetector.FindMatches(board);
        var birth = SpecialRules.ResolveBirths(board, matches).Single();
        Assert.That(birth.Special, Is.EqualTo(Special.Star));
        Assert.That(birth.Cell, Is.EqualTo(Pos(0, 0)));
    }

    [Test]
    public void FourRunBirthsAFlame()
    {
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(0, 0)] = Gem(GemType.Red), [Pos(0, 1)] = Gem(GemType.Red),
            [Pos(0, 2)] = Gem(GemType.Red), [Pos(0, 3)] = Gem(GemType.Red),
        });
        var matches = MatchDetector.FindMatches(board);
        var birth = SpecialRules.ResolveBirths(board, matches).Single();
        Assert.That(birth.Special, Is.EqualTo(Special.Flame));
    }

    [Test]
    public void PlainThreeRunBirthsNothing()
    {
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(0, 0)] = Gem(GemType.Red), [Pos(0, 1)] = Gem(GemType.Red),
            [Pos(0, 2)] = Gem(GemType.Red),
        });
        var matches = MatchDetector.FindMatches(board);
        Assert.That(SpecialRules.ResolveBirths(board, matches), Is.Empty);
    }

    [Test]
    public void PrecedenceFiveBeatsTSlashWhenTheyIntersect()
    {
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(0, 0)] = Gem(GemType.Red), [Pos(0, 1)] = Gem(GemType.Red),
            [Pos(0, 2)] = Gem(GemType.Red), [Pos(0, 3)] = Gem(GemType.Red),
            [Pos(0, 4)] = Gem(GemType.Red),
            [Pos(1, 0)] = Gem(GemType.Red), [Pos(2, 0)] = Gem(GemType.Red),
        });
        var matches = MatchDetector.FindMatches(board);
        Assert.That(SpecialRules.ResolveBirths(board, matches).Single().Special, Is.EqualTo(Special.Hypercube));
    }

    [Test]
    public void PrecedenceTSlashBeatsFourRunWhenTheyIntersect()
    {
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(0, 0)] = Gem(GemType.Red), [Pos(0, 1)] = Gem(GemType.Red),
            [Pos(0, 2)] = Gem(GemType.Red), [Pos(0, 3)] = Gem(GemType.Red),
            [Pos(1, 0)] = Gem(GemType.Red), [Pos(2, 0)] = Gem(GemType.Red),
        });
        var matches = MatchDetector.FindMatches(board);
        Assert.That(SpecialRules.ResolveBirths(board, matches).Single().Special, Is.EqualTo(Special.Star));
    }

    [Test]
    public void TwoNonOverlappingFourRunsBirthTwoFlames()
    {
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(0, 0)] = Gem(GemType.Red), [Pos(0, 1)] = Gem(GemType.Red),
            [Pos(0, 2)] = Gem(GemType.Red), [Pos(0, 3)] = Gem(GemType.Red),
            [Pos(4, 0)] = Gem(GemType.Blue), [Pos(4, 1)] = Gem(GemType.Blue),
            [Pos(4, 2)] = Gem(GemType.Blue), [Pos(4, 3)] = Gem(GemType.Blue),
        });
        var births = SpecialRules.ResolveBirths(board, MatchDetector.FindMatches(board));
        Assert.That(births, Has.Count.EqualTo(2));
        Assert.That(births.Select(b => b.Special), Is.EqualTo(new[] { Special.Flame, Special.Flame }));
        Assert.That(births.Select(b => b.Cell), Is.EqualTo(new[] { Pos(0, 0), Pos(4, 0) }));
    }

    [Test]
    public void AFourRunAndASeparateFiveRunBirthAFlameAndAHypercube()
    {
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(0, 0)] = Gem(GemType.Red), [Pos(0, 1)] = Gem(GemType.Red),
            [Pos(0, 2)] = Gem(GemType.Red), [Pos(0, 3)] = Gem(GemType.Red),
            [Pos(4, 0)] = Gem(GemType.Blue), [Pos(4, 1)] = Gem(GemType.Blue),
            [Pos(4, 2)] = Gem(GemType.Blue), [Pos(4, 3)] = Gem(GemType.Blue),
            [Pos(4, 4)] = Gem(GemType.Blue),
        });
        var births = SpecialRules.ResolveBirths(board, MatchDetector.FindMatches(board));
        Assert.That(births, Has.Count.EqualTo(2));
        Assert.That(births[0].Special, Is.EqualTo(Special.Flame));
        Assert.That(births[0].Cell, Is.EqualTo(Pos(0, 0)));
        Assert.That(births[1].Special, Is.EqualTo(Special.Hypercube));
        Assert.That(births[1].Cell, Is.EqualTo(Pos(4, 0)));
    }

    [Test]
    public void ATSlashGroupAndASeparateFourRunBirthOneStarAndOneFlame()
    {
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(0, 0)] = Gem(GemType.Red), [Pos(0, 1)] = Gem(GemType.Red),
            [Pos(0, 2)] = Gem(GemType.Red),
            [Pos(1, 0)] = Gem(GemType.Red), [Pos(2, 0)] = Gem(GemType.Red),
            [Pos(6, 0)] = Gem(GemType.Blue), [Pos(6, 1)] = Gem(GemType.Blue),
            [Pos(6, 2)] = Gem(GemType.Blue), [Pos(6, 3)] = Gem(GemType.Blue),
        });
        var births = SpecialRules.ResolveBirths(board, MatchDetector.FindMatches(board));
        Assert.That(births, Has.Count.EqualTo(2));
        Assert.That(births[0].Special, Is.EqualTo(Special.Star));
        Assert.That(births[0].Cell, Is.EqualTo(Pos(0, 0)));
        Assert.That(births[1].Special, Is.EqualTo(Special.Flame));
        Assert.That(births[1].Cell, Is.EqualTo(Pos(6, 0)));
    }

    [Test]
    public void HypercubeIsColorlessAndNeverPartOfARun()
    {
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(0, 0)] = Gem(GemType.Red), [Pos(0, 1)] = Gem(GemType.Red),
            [Pos(0, 2)] = Gem(GemType.Red, Special.Hypercube),
            [Pos(0, 3)] = Gem(GemType.Red),
        });
        Assert.That(MatchDetector.FindMatches(board), Is.Empty);
    }

    [Test]
    public void HypercubeTriggerWithAPlainPartnerClearsThePartnerColor()
    {
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(4, 4)] = Gem(GemType.Blue, Special.Hypercube),
            [Pos(4, 5)] = Gem(GemType.Red),
            [Pos(0, 0)] = Gem(GemType.Red),
            [Pos(8, 8)] = Gem(GemType.Green),
        });
        var cells = SpecialRules.HypercubeTriggerCells(board, Pos(4, 4), Pos(4, 5), null);
        Assert.That(cells, Is.EquivalentTo(new[] { Pos(4, 4), Pos(4, 5), Pos(0, 0) }));
    }

    [Test]
    public void FlamePlusFlameClearsAFiveByFiveArea()
    {
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(4, 4)] = Gem(GemType.Red), [Pos(4, 5)] = Gem(GemType.Red),
        });
        var cells = SpecialRules.ComboAffectedCells(board, Pos(4, 4), Pos(4, 5), Special.Flame, Special.Flame);
        Assert.That(cells, Has.Count.EqualTo(25));
        Assert.That(cells.Contains(Pos(4, 4)) && cells.Contains(Pos(4, 5)), Is.True);
        Assert.That(cells.Contains(Pos(2, 2)), Is.True);
        Assert.That(cells.Contains(Pos(6, 6)), Is.True);
    }

    [Test]
    public void FlamePlusStarClearsAThickCross()
    {
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(4, 4)] = Gem(GemType.Red), [Pos(4, 5)] = Gem(GemType.Red),
        });
        var cells = SpecialRules.ComboAffectedCells(board, Pos(4, 4), Pos(4, 5), Special.Flame, Special.Star);
        Assert.That(cells, Has.Count.EqualTo(45));
        Assert.That(cells.Contains(Pos(4, 4)) && cells.Contains(Pos(4, 5)), Is.True);
        Assert.That(cells.Contains(Pos(3, 0)), Is.True);
        Assert.That(cells.Contains(Pos(8, 4)), Is.True);
    }

    [Test]
    public void StarPlusStarClearsBothRowsAndColumnsDeduped()
    {
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(4, 4)] = Gem(GemType.Red), [Pos(4, 6)] = Gem(GemType.Red),
        });
        var cells = SpecialRules.ComboAffectedCells(board, Pos(4, 4), Pos(4, 6), Special.Star, Special.Star);
        Assert.That(cells, Has.Count.EqualTo(25));
        Assert.That(cells.Contains(Pos(4, 4)) && cells.Contains(Pos(4, 6)), Is.True);
    }

    [Test]
    public void FlamePlusHypercubePowersEveryGemOfThePartnerColor()
    {
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(4, 4)] = Gem(GemType.Blue),
            [Pos(4, 5)] = Gem(GemType.Red, Special.Flame),
            [Pos(0, 0)] = Gem(GemType.Red),
            [Pos(8, 8)] = Gem(GemType.Red),
        });
        var cells = SpecialRules.ComboAffectedCells(
            board, Pos(4, 4), Pos(4, 5), Special.Hypercube, Special.Flame);
        Assert.That(cells, Has.Count.EqualTo(17));
        Assert.That(cells.Contains(Pos(4, 4)) && cells.Contains(Pos(4, 5)) &&
                    cells.Contains(Pos(0, 0)) && cells.Contains(Pos(8, 8)), Is.True);
    }

    [Test]
    public void StarPlusHypercubeClearsRowsAndColumnsOfEveryPartnerColorGem()
    {
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(4, 4)] = Gem(GemType.Blue),
            [Pos(4, 5)] = Gem(GemType.Red, Special.Star),
            [Pos(0, 0)] = Gem(GemType.Red),
            [Pos(8, 8)] = Gem(GemType.Red),
        });
        var cells = SpecialRules.ComboAffectedCells(
            board, Pos(4, 4), Pos(4, 5), Special.Hypercube, Special.Star);
        Assert.That(cells, Has.Count.EqualTo(45));
        Assert.That(cells.Contains(Pos(4, 4)) && cells.Contains(Pos(4, 5)) &&
                    cells.Contains(Pos(0, 0)) && cells.Contains(Pos(8, 8)), Is.True);
    }

    [Test]
    public void HypercubePlusHypercubeClearsTheEntireBoard()
    {
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(4, 4)] = Gem(GemType.Blue, Special.Hypercube),
            [Pos(4, 5)] = Gem(GemType.Blue, Special.Hypercube),
        });
        var cells = SpecialRules.ComboAffectedCells(
            board, Pos(4, 4), Pos(4, 5), Special.Hypercube, Special.Hypercube);
        Assert.That(cells, Has.Count.EqualTo(81));
    }

    [Test]
    public void ComboCellsAreAlwaysUniqueEvenWhenRegionsOverlap()
    {
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(4, 4)] = Gem(GemType.Red),
            [Pos(4, 6)] = Gem(GemType.Red),
            [Pos(4, 8)] = Gem(GemType.Red),
        });
        var cells = SpecialRules.ComboAffectedCells(board, Pos(4, 4), Pos(4, 6), Special.Star, Special.Star);
        Assert.That(cells, Has.Count.EqualTo(cells.Count));
        Assert.That(Scorer.RoundScore(cells, 1) / Scorer.BASE_POINTS_PER_GEM, Is.EqualTo(25));
    }

    [Test]
    public void FlameSweptIntoACascadeMatchDetonatesAndBlastsTheArea()
    {
        var engine = Engine();
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(4, 3)] = Gem(GemType.Red),
            [Pos(4, 4)] = Gem(GemType.Red, Special.Flame),
            [Pos(4, 5)] = Gem(GemType.Yellow),
            [Pos(5, 5)] = Gem(GemType.Red),
        });
        var resolution = engine.ResolveSwap(board, Pos(4, 5), Pos(5, 5));
        Assert.That(resolution, Is.Not.Null);
        var steps = resolution!.Steps;
        var firstDestroy = steps.OfType<Step.Destroy>().First();

        Assert.That(firstDestroy.Positions, Has.Count.EqualTo(9));
        Assert.That(firstDestroy.Positions.Contains(Pos(3, 4)), Is.True);
        Assert.That(firstDestroy.Positions.Contains(Pos(5, 4)), Is.True);
    }

    [Test]
    public void FlameBlastChainsIntoABlastedStarAndHypercube()
    {
        var engine = Engine();
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(0, 0)] = Gem(GemType.Red),
            [Pos(3, 5)] = Gem(GemType.Green, Special.Star),
            [Pos(4, 3)] = Gem(GemType.Red),
            [Pos(4, 4)] = Gem(GemType.Red, Special.Flame),
            [Pos(4, 5)] = Gem(GemType.Yellow),
            [Pos(5, 3)] = Gem(GemType.Blue, Special.Hypercube),
            [Pos(5, 5)] = Gem(GemType.Red),
            [Pos(8, 8)] = Gem(GemType.Red),
        });
        var resolution = engine.ResolveSwap(board, Pos(4, 5), Pos(5, 5));
        Assert.That(resolution, Is.Not.Null);
        var firstDestroy = resolution!.Steps.OfType<Step.Destroy>().First();

        Assert.That(firstDestroy.Positions, Has.Count.EqualTo(23));
        Assert.That(firstDestroy.Positions.Contains(Pos(3, 8)), Is.True);
        Assert.That(firstDestroy.Positions.Contains(Pos(0, 5)), Is.True);
        Assert.That(firstDestroy.Positions.Contains(Pos(0, 0)), Is.True);
        Assert.That(firstDestroy.Positions.Contains(Pos(8, 8)), Is.True);
    }

    [Test]
    public void SwappingTwoFlamesFiresTheFlameFlameCombo()
    {
        var engine = Engine();
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(4, 4)] = Gem(GemType.Red, Special.Flame),
            [Pos(4, 5)] = Gem(GemType.Red, Special.Flame),
        });
        var resolution = engine.ResolveSwap(board, Pos(4, 4), Pos(4, 5));
        Assert.That(resolution, Is.Not.Null);
        var combo = resolution!.Steps.OfType<Step.ComboActivate>().First();
        Assert.That(combo.SpecialA, Is.EqualTo(Special.Flame));
        Assert.That(combo.SpecialB, Is.EqualTo(Special.Flame));
        Assert.That(combo.AffectedCells, Has.Count.EqualTo(25));

        var firstDestroy = resolution.Steps.OfType<Step.Destroy>().First();
        Assert.That(firstDestroy.Positions, Is.EquivalentTo(combo.AffectedCells));
    }

    [Test]
    public void SwappingAFlameAndAStarFiresTheThickCrossCombo()
    {
        var engine = Engine();
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(4, 4)] = Gem(GemType.Red, Special.Flame),
            [Pos(4, 5)] = Gem(GemType.Red, Special.Star),
        });
        var resolution = engine.ResolveSwap(board, Pos(4, 4), Pos(4, 5));
        Assert.That(resolution, Is.Not.Null);
        var combo = resolution!.Steps.OfType<Step.ComboActivate>().First();
        Assert.That(new[] { combo.SpecialA, combo.SpecialB }, Is.EquivalentTo(new[] { Special.Flame, Special.Star }));
        Assert.That(combo.AffectedCells, Has.Count.EqualTo(45));

        var firstDestroy = resolution.Steps.OfType<Step.Destroy>().First();
        Assert.That(firstDestroy.Positions, Is.EquivalentTo(combo.AffectedCells));
    }

    [Test]
    public void SwappingTwoStarsFiresTheDualRowColumnCombo()
    {
        var engine = Engine();
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(4, 4)] = Gem(GemType.Red, Special.Star),
            [Pos(4, 5)] = Gem(GemType.Red, Special.Star),
        });
        var resolution = engine.ResolveSwap(board, Pos(4, 4), Pos(4, 5));
        Assert.That(resolution, Is.Not.Null);
        var combo = resolution!.Steps.OfType<Step.ComboActivate>().First();
        Assert.That(combo.SpecialA, Is.EqualTo(Special.Star));
        Assert.That(combo.SpecialB, Is.EqualTo(Special.Star));
        Assert.That(combo.AffectedCells, Has.Count.EqualTo(25));
    }

    [Test]
    public void SwappingAFlameAndAHypercubeFiresTheAllFlamesCombo()
    {
        var engine = Engine();
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(4, 4)] = Gem(GemType.Red, Special.Flame),
            [Pos(4, 5)] = Gem(GemType.Blue, Special.Hypercube),
            [Pos(0, 0)] = Gem(GemType.Red),
            [Pos(8, 8)] = Gem(GemType.Red),
        });
        var resolution = engine.ResolveSwap(board, Pos(4, 4), Pos(4, 5));
        Assert.That(resolution, Is.Not.Null);
        var combo = resolution!.Steps.OfType<Step.ComboActivate>().First();
        Assert.That(new[] { combo.SpecialA, combo.SpecialB },
            Is.EquivalentTo(new[] { Special.Flame, Special.Hypercube }));
        Assert.That(combo.AffectedCells, Has.Count.EqualTo(17));
    }

    [Test]
    public void SwappingAHypercubeWithANormalGemClearsAllGemsOfItsColor()
    {
        var engine = Engine();
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(4, 4)] = Gem(GemType.Blue, Special.Hypercube),
            [Pos(4, 5)] = Gem(GemType.Red),
            [Pos(0, 0)] = Gem(GemType.Red),
        });
        var resolution = engine.ResolveSwap(board, Pos(4, 4), Pos(4, 5));
        Assert.That(resolution, Is.Not.Null);
        var steps = resolution!.Steps;
        var combo = steps.OfType<Step.ComboActivate>().First();
        Assert.That(combo.SpecialA, Is.EqualTo(Special.Hypercube));
        Assert.That(combo.SpecialB, Is.Null);
        Assert.That(combo.AffectedCells, Is.EquivalentTo(new[] { Pos(4, 4), Pos(4, 5), Pos(0, 0) }));
    }

    [Test]
    public void HypercubePlusHypercubeRegeneratesTheBoard()
    {
        var engine = Engine();
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(4, 4)] = Gem(GemType.Blue, Special.Hypercube),
            [Pos(4, 5)] = Gem(GemType.Blue, Special.Hypercube),
        });
        var resolution = engine.ResolveSwap(board, Pos(4, 4), Pos(4, 5));
        Assert.That(resolution, Is.Not.Null);
        var steps = resolution!.Steps;
        var combo = steps.OfType<Step.ComboActivate>().First();
        Assert.That(combo.SpecialA, Is.EqualTo(Special.Hypercube));
        Assert.That(combo.SpecialB, Is.EqualTo(Special.Hypercube));
        Assert.That(combo.AffectedCells, Has.Count.EqualTo(81));

        Assert.That(steps.OfType<Step.Destroy>().Last().Positions.Count, Is.EqualTo(81));
        Assert.That(steps.OfType<Step.Spawn>(), Is.Empty);

        var settled = (Step.Settled)steps[^1];
        var fresh = settled.Board;
        Assert.That(fresh.Positions().Count(p => fresh.GemAt(p) is not null), Is.EqualTo(81));
        Assert.That(fresh.Positions().All(p => fresh.GemAt(p)?.Special is null), Is.True);
        Assert.That(MatchDetector.FindMatches(fresh), Is.Empty);
        Assert.That(LegalMoveDetector.HasLegalMove(fresh), Is.True);
    }

    [Test]
    public void OneSwapCreatingTwoSeparateFourRunsBirthsTwoFlames()
    {
        var engine = Engine();
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(4, 3)] = Gem(GemType.Red),
            [Pos(4, 4)] = Gem(GemType.Red),
            [Pos(4, 6)] = Gem(GemType.Red),
            [Pos(4, 5)] = Gem(GemType.Green),
            [Pos(5, 5)] = Gem(GemType.Red),
            [Pos(6, 5)] = Gem(GemType.Green),
            [Pos(7, 5)] = Gem(GemType.Green),
            [Pos(8, 5)] = Gem(GemType.Green),
        });
        var resolution = engine.ResolveSwap(board, Pos(4, 5), Pos(5, 5));
        Assert.That(resolution, Is.Not.Null);
        var steps = resolution!.Steps;

        var births = steps.OfType<Step.SpecialBirth>().ToList();
        Assert.That(births, Has.Count.EqualTo(2));
        Assert.That(births.All(b => b.Special == Special.Flame), Is.True);
        Assert.That(births[0].GemId, Is.Not.EqualTo(births[1].GemId), "distinct birth gems");
        Assert.That(births.Select(b => b.Position), Is.EqualTo(new[] { Pos(8, 3), Pos(8, 5) }));

        var firstDestroy = steps.OfType<Step.Destroy>().First();
        Assert.That(firstDestroy.Positions, Is.EquivalentTo(new[]
        {
            Pos(4, 4), Pos(4, 5), Pos(4, 6), Pos(6, 5), Pos(7, 5), Pos(8, 5),
        }));
        var firstScore = steps.OfType<Step.Score>().First();
        Assert.That(firstScore.Delta, Is.EqualTo(6 * Scorer.BASE_POINTS_PER_GEM));
        Assert.That(firstScore.CascadeDepth, Is.EqualTo(1));
    }

    [Test]
    public void ASwapCreatingAFiveRunTransformsAGemIntoAHypercube()
    {
        var engine = Engine();
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(0, 0)] = Gem(GemType.Yellow),
            [Pos(0, 1)] = Gem(GemType.Red), [Pos(0, 2)] = Gem(GemType.Red),
            [Pos(0, 3)] = Gem(GemType.Red), [Pos(0, 4)] = Gem(GemType.Red),
            [Pos(0, 5)] = Gem(GemType.Red),
            [Pos(1, 0)] = Gem(GemType.Red),
        });
        var resolution = engine.ResolveSwap(board, Pos(0, 0), Pos(1, 0));
        Assert.That(resolution, Is.Not.Null);
        var steps = resolution!.Steps;
        var birth = steps.OfType<Step.SpecialBirth>().First();
        Assert.That(birth.Special, Is.EqualTo(Special.Hypercube));

        var settled = (Step.Settled)steps[^1];
        var survivors = settled.Board.Positions()
            .Select(p => settled.Board.GemAt(p))
            .Where(g => g is not null && g.Value.Special == Special.Hypercube)
            .ToList();
        Assert.That(survivors, Is.Not.Empty);
        Assert.That(survivors.Any(g => g.Value.Id == birth.GemId), Is.True);
    }

    [Test]
    public void ASwapCreatingATSlashTransformsAGemIntoAStar()
    {
        var engine = Engine();
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(0, 0)] = Gem(GemType.Red), [Pos(0, 1)] = Gem(GemType.Red),
            [Pos(0, 2)] = Gem(GemType.Red),
            [Pos(1, 0)] = Gem(GemType.Red), [Pos(2, 0)] = Gem(GemType.Red),
            [Pos(8, 0)] = Gem(GemType.Green), [Pos(8, 1)] = Gem(GemType.Blue),
        });
        var resolution = engine.ResolveSwap(board, Pos(8, 0), Pos(8, 1));
        Assert.That(resolution, Is.Not.Null);
        var steps = resolution!.Steps;
        var birth = steps.OfType<Step.SpecialBirth>().First();
        Assert.That(birth.Special, Is.EqualTo(Special.Star));

        var settled = (Step.Settled)steps[^1];
        Assert.That(settled.Board.Positions().Any(p => settled.Board.GemAt(p)?.Special == Special.Star), Is.True);
    }

    [Test]
    public void ComboRoundScoresUniqueCellsAtDepthOne()
    {
        var engine = Engine();
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(4, 4)] = Gem(GemType.Red, Special.Star),
            [Pos(4, 5)] = Gem(GemType.Red, Special.Star),
        });
        var steps = engine.ResolveSwap(board, Pos(4, 4), Pos(4, 5))!.Steps;
        var firstScore = steps.OfType<Step.Score>().First();
        Assert.That(firstScore.CascadeDepth, Is.EqualTo(1));
        Assert.That(firstScore.Delta, Is.EqualTo(25 * Scorer.BASE_POINTS_PER_GEM));
    }

    [Test]
    public void FullBoardHypercubeClearScores81UniqueCells()
    {
        var engine = Engine();
        var board = Board9(new Dictionary<Position, Gem>
        {
            [Pos(4, 4)] = Gem(GemType.Blue, Special.Hypercube),
            [Pos(4, 5)] = Gem(GemType.Blue, Special.Hypercube),
        });
        var res = engine.ResolveSwap(board, Pos(4, 4), Pos(4, 5))!;
        var comboScore = res.Steps.OfType<Step.Score>().First();
        Assert.That(comboScore.Delta, Is.EqualTo(81 * Scorer.BASE_POINTS_PER_GEM));
    }
}