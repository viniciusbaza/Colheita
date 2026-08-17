using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Rules;
using Xunit;

namespace FarmAndFriends.Api.Tests.Domain.Rules;

public sealed class FarmLayoutRulesTests
{
    [Fact]
    public void InitialLayout_HasThreeByThreeGridAndSixUnlockedPlots()
    {
        var farmId = Guid.NewGuid();
        var plots = FarmLayoutRules.CreateInitialPlots(farmId);

        Assert.Equal(9, plots.Count);
        Assert.Equal(6, plots.Count(plot => plot.Unlocked));
        Assert.Equal(
            Enumerable.Range(0, 3)
                .SelectMany(y => Enumerable.Range(0, 3)
                    .Select(x => (x, y))),
            plots.OrderBy(plot => plot.Y).ThenBy(plot => plot.X)
                .Select(plot => (plot.X, plot.Y)));
        Assert.All(plots, plot => Assert.Equal(farmId, plot.FarmId));
        Assert.Equal(
            new[]
            {
                (0, 0), (1, 0), (2, 0),
                (0, 1), (1, 1), (2, 1),
                (0, 2), (1, 2), (2, 2)
            },
            plots.Select(plot => (plot.X, plot.Y)));
    }

    [Fact]
    public void Offers_FollowApprovedPlotOrderAndExpansionTopology()
    {
        var farmId = Guid.NewGuid();
        var plots = FarmLayoutRules.CreateInitialPlots(farmId).ToList();

        AssertOffer(plots, 7, 0, 2);
        UnlockOffer(plots);
        AssertOffer(plots, 8, 1, 2);
        UnlockOffer(plots);
        var ninth = AssertOffer(plots, 9, 2, 2);
        Assert.Equal(new LandDimensions(7, 4), ninth.ExpandsTo);
        UnlockOffer(plots);

        plots.AddRange(FarmLayoutRules.ExpansionPlots().Select(definition =>
            new Plot
            {
                Id = Guid.NewGuid(),
                FarmId = farmId,
                X = definition.X,
                Y = definition.Y,
                Unlocked = false
            }));

        Assert.Equal(28, plots.Count);
        AssertOffer(plots, 10, 0, 3);
        UnlockOffer(plots);
        AssertOffer(plots, 11, 1, 3);
        UnlockOffer(plots);
        AssertOffer(plots, 12, 2, 3);
        UnlockOffer(plots);
        AssertOffer(plots, 13, 3, 0);
        Assert.Equal(
            (6, 3),
            FarmLayoutRules.ExpansionPlots()
                .Single(definition => definition.PlotNumber == 28)
                is var final ? (final.X, final.Y) : default);
    }

    [Fact]
    public void ExpansionDefinitions_TransposeOnlyPlotsTenThroughTwentyEight()
    {
        var expectedCoordinates = new (int X, int Y)[]
        {
            (0, 3), (1, 3), (2, 3),
            (3, 0), (3, 1), (3, 2), (3, 3),
            (4, 0), (4, 1), (4, 2), (4, 3),
            (5, 0), (5, 1), (5, 2), (5, 3),
            (6, 0), (6, 1), (6, 2), (6, 3)
        };

        Assert.Equal(
            expectedCoordinates,
            FarmLayoutRules.ExpansionPlots()
                .Select(definition => (definition.X, definition.Y)));

        Assert.Equal(
            Enumerable.Range(10, 19),
            FarmLayoutRules.ExpansionPlots()
                .Select(definition => definition.PlotNumber));
    }

    [Theory]
    [InlineData(7, 2, 500, 2)]
    [InlineData(8, 3, 1500, 3)]
    [InlineData(9, 4, 4000, 4)]
    [InlineData(10, 5, 10000, 5)]
    [InlineData(13, 5, 10000, 5)]
    [InlineData(14, 6, 25000, 6)]
    [InlineData(18, 6, 25000, 6)]
    [InlineData(19, 7, 60000, 8)]
    [InlineData(23, 7, 60000, 8)]
    [InlineData(24, 8, 150000, 10)]
    [InlineData(28, 8, 150000, 10)]
    public void PricesAndLevels_AreAuthoritative(
        int plotNumber,
        int minimumLevel,
        int coins,
        int premiumCoins)
    {
        var definition = FarmLayoutRules.GetDefinition(plotNumber);

        Assert.Equal(minimumLevel, definition.MinimumLevel);
        Assert.Equal(coins, definition.CoinsPrice);
        Assert.Equal(premiumCoins, definition.PremiumCoinsPrice);
    }

    [Fact]
    public void CompleteLayout_HasNoOffer()
    {
        var plots = FullLayout(unlockedThrough: 28);

        Assert.Equal(
            FarmLayoutStatus.Complete,
            FarmLayoutRules.Evaluate(plots).Status);
    }

    [Fact]
    public void GapOrUnexpectedTopology_IsUnsupported()
    {
        var gap = FullLayout(unlockedThrough: 10);
        gap.Single(plot => plot.X == 0 && plot.Y == 3).Unlocked = false;
        gap.Single(plot => plot.X == 1 && plot.Y == 3).Unlocked = true;
        var unexpected = FarmLayoutRules.CreateInitialPlots(Guid.NewGuid())
            .ToList();
        unexpected[8].X = 99;

        Assert.Equal(
            FarmLayoutStatus.Unsupported,
            FarmLayoutRules.Evaluate(gap).Status);
        Assert.Equal(
            FarmLayoutStatus.Unsupported,
            FarmLayoutRules.Evaluate(unexpected).Status);
    }

    private static LandPlotDefinition AssertOffer(
        IReadOnlyCollection<Plot> plots,
        int plotNumber,
        int x,
        int y)
    {
        var evaluation = FarmLayoutRules.Evaluate(plots);
        Assert.Equal(FarmLayoutStatus.OfferAvailable, evaluation.Status);
        Assert.Equal(plotNumber, evaluation.Offer!.PlotNumber);
        Assert.Equal((x, y), (evaluation.Offer.X, evaluation.Offer.Y));
        Assert.Equal(
            (x, y),
            (evaluation.OfferedPlot!.X, evaluation.OfferedPlot.Y));
        return evaluation.Offer;
    }

    private static void UnlockOffer(IReadOnlyCollection<Plot> plots)
    {
        FarmLayoutRules.Evaluate(plots).OfferedPlot!.Unlocked = true;
    }

    private static List<Plot> FullLayout(int unlockedThrough)
    {
        var plots = DefinitionsAsPlots();
        for (var index = 0; index < unlockedThrough; index++)
            plots[index].Unlocked = true;
        return plots;
    }

    private static List<Plot> DefinitionsAsPlots()
    {
        var farmId = Guid.NewGuid();
        return FarmLayoutRules.CreateInitialPlots(farmId)
            .Concat(FarmLayoutRules.ExpansionPlots().Select(definition =>
                new Plot
                {
                    Id = Guid.NewGuid(),
                    FarmId = farmId,
                    X = definition.X,
                    Y = definition.Y
                }))
            .Select(plot =>
            {
                plot.Unlocked = false;
                return plot;
            })
            .ToList();
    }
}
