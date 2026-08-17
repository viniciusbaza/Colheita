using FarmAndFriends.Api.Domain.Entities;

namespace FarmAndFriends.Api.Domain.Rules;

public static class FarmLayoutRules
{
    public const int InitialPlotCount = 9;
    public const int InitialUnlockedPlotCount = 6;
    public const int MaxPlotCount = 28;

    private static readonly IReadOnlyList<LandPlotDefinition> Definitions =
        BuildDefinitions();

    public static IReadOnlyList<Plot> CreateInitialPlots(Guid farmId) =>
        Definitions
            .Take(InitialPlotCount)
            .Select(definition => new Plot
            {
                Id = Guid.NewGuid(),
                FarmId = farmId,
                X = definition.X,
                Y = definition.Y,
                Unlocked = definition.PlotNumber
                    <= InitialUnlockedPlotCount
            })
            .ToList();

    public static FarmLayoutEvaluation Evaluate(
        IEnumerable<Plot> plots)
    {
        var actual = plots.ToList();
        var supportedCount = actual.Count is InitialPlotCount or MaxPlotCount;
        if (!supportedCount)
            return FarmLayoutEvaluation.Unsupported;

        var expected = Definitions.Take(actual.Count).ToList();
        var byCoordinate = actual
            .GroupBy(plot => (plot.X, plot.Y))
            .ToDictionary(group => group.Key, group => group.ToList());

        if (byCoordinate.Values.Any(group => group.Count != 1)
            || expected.Any(definition =>
                !byCoordinate.ContainsKey((definition.X, definition.Y)))
            || byCoordinate.Keys.Any(coordinate =>
                expected.All(definition =>
                    definition.X != coordinate.X
                    || definition.Y != coordinate.Y)))
        {
            return FarmLayoutEvaluation.Unsupported;
        }

        var orderedPlots = expected
            .Select(definition => byCoordinate[(definition.X, definition.Y)][0])
            .ToList();
        var unlockedCount = orderedPlots.Count(plot => plot.Unlocked);

        if (orderedPlots.Take(unlockedCount).Any(plot => !plot.Unlocked)
            || orderedPlots.Skip(unlockedCount).Any(plot => plot.Unlocked))
        {
            return FarmLayoutEvaluation.Unsupported;
        }

        if (actual.Count == InitialPlotCount)
        {
            if (unlockedCount < InitialUnlockedPlotCount
                || unlockedCount >= InitialPlotCount)
            {
                return FarmLayoutEvaluation.Unsupported;
            }
        }
        else if (unlockedCount < InitialPlotCount)
        {
            return FarmLayoutEvaluation.Unsupported;
        }

        if (unlockedCount == MaxPlotCount)
            return FarmLayoutEvaluation.Complete;

        var offer = Definitions[unlockedCount];
        return new FarmLayoutEvaluation(
            FarmLayoutStatus.OfferAvailable,
            offer,
            orderedPlots[unlockedCount]);
    }

    public static IReadOnlyList<LandPlotDefinition> ExpansionPlots() =>
        Definitions.Skip(InitialPlotCount).ToList();

    public static LandPlotDefinition GetDefinition(int plotNumber)
    {
        if (plotNumber < 1 || plotNumber > MaxPlotCount)
            throw new ArgumentOutOfRangeException(nameof(plotNumber));

        return Definitions[plotNumber - 1];
    }

    private static IReadOnlyList<LandPlotDefinition> BuildDefinitions()
    {
        var definitions = new List<LandPlotDefinition>(MaxPlotCount);
        var plotNumber = 1;

        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 3; x++)
                definitions.Add(CreateDefinition(plotNumber++, x, y));
        }

        for (var x = 0; x < 3; x++)
            definitions.Add(CreateDefinition(plotNumber++, x, 2));

        for (var x = 0; x < 3; x++)
            definitions.Add(CreateDefinition(plotNumber++, x, 3));

        for (var x = 3; x < 7; x++)
        {
            for (var y = 0; y < 4; y++)
                definitions.Add(CreateDefinition(plotNumber++, x, y));
        }

        return definitions;
    }

    private static LandPlotDefinition CreateDefinition(
        int plotNumber,
        int x,
        int y)
    {
        var (minimumLevel, coins, premiumCoins) = plotNumber switch
        {
            7 => (2, 500, 2),
            8 => (3, 1_500, 3),
            9 => (4, 4_000, 4),
            >= 10 and <= 13 => (5, 10_000, 5),
            >= 14 and <= 18 => (6, 25_000, 6),
            >= 19 and <= 23 => (7, 60_000, 8),
            >= 24 and <= 28 => (8, 150_000, 10),
            _ => (0, 0, 0)
        };

        return new LandPlotDefinition(
            plotNumber,
            x,
            y,
            minimumLevel,
            coins,
            premiumCoins,
            plotNumber == 9 ? new LandDimensions(7, 4) : null);
    }
}

public enum FarmLayoutStatus
{
    Unsupported,
    OfferAvailable,
    Complete
}

public sealed record FarmLayoutEvaluation(
    FarmLayoutStatus Status,
    LandPlotDefinition? Offer,
    Plot? OfferedPlot)
{
    public static FarmLayoutEvaluation Unsupported { get; } =
        new(FarmLayoutStatus.Unsupported, null, null);

    public static FarmLayoutEvaluation Complete { get; } =
        new(FarmLayoutStatus.Complete, null, null);
}

public sealed record LandPlotDefinition(
    int PlotNumber,
    int X,
    int Y,
    int MinimumLevel,
    int CoinsPrice,
    int PremiumCoinsPrice,
    LandDimensions? ExpandsTo);

public sealed record LandDimensions(int Width, int Height);
