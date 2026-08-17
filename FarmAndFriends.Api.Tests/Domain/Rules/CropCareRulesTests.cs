using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Rules;
using Xunit;

namespace FarmAndFriends.Api.Tests.Domain.Rules;

public sealed class CropCareRulesTests
{
    [Fact]
    public void StartCurrentCropCycle_CreatesNewOpportunityToken()
    {
        var previousOpportunityId = Guid.NewGuid();
        var plot = new Plot
        {
            CareOpportunityId = previousOpportunityId
        };

        CropCareRules.StartCurrentCropCycle(plot);

        Assert.NotNull(plot.CareOpportunityId);
        Assert.NotEqual(Guid.Empty, plot.CareOpportunityId.Value);
        Assert.NotEqual(previousOpportunityId, plot.CareOpportunityId.Value);
    }

    [Fact]
    public void IsGrowing_RemainsTrueThroughoutSevenDayCrop()
    {
        var plantedAt = Utc(2026, 7, 26, 10);
        var opportunityId = Guid.NewGuid();
        var plot = GrowingPlot(
            opportunityId,
            plantedAt,
            plantedAt.AddDays(7));

        Assert.True(CropCareRules.IsGrowing(
            plot,
            opportunityId,
            plantedAt));
        Assert.True(CropCareRules.IsGrowing(
            plot,
            opportunityId,
            plantedAt.AddDays(6).AddHours(23)));
    }

    [Fact]
    public void IsGrowing_RejectsWrongOpportunityToken()
    {
        var plantedAt = Utc(2026, 7, 26, 10);
        var plot = GrowingPlot(
            Guid.NewGuid(),
            plantedAt,
            plantedAt.AddDays(7));

        Assert.False(CropCareRules.IsGrowing(
            plot,
            Guid.NewGuid(),
            plantedAt.AddDays(1)));
    }

    [Fact]
    public void IsGrowing_IsFalseExactlyAtReadyAt()
    {
        var plantedAt = Utc(2026, 7, 26, 10);
        var readyAt = plantedAt.AddDays(7);
        var opportunityId = Guid.NewGuid();
        var plot = GrowingPlot(
            opportunityId,
            plantedAt,
            readyAt);

        Assert.False(CropCareRules.IsGrowing(
            plot,
            opportunityId,
            readyAt));
    }

    [Fact]
    public void IsGrowing_RejectsLockedPlotWithInconsistentCropState()
    {
        var now = Utc(2026, 7, 26, 10);
        var opportunityId = Guid.NewGuid();
        var plot = GrowingPlot(
            opportunityId,
            now.AddMinutes(-1),
            now.AddHours(1));
        plot.Unlocked = false;

        Assert.False(CropCareRules.IsGrowing(
            plot,
            opportunityId,
            now));
    }

    [Fact]
    public void WasGrowingAt_UsesWholeHalfOpenGrowthWindow()
    {
        var plantedAt = Utc(2026, 7, 26, 10);
        var readyAt = plantedAt.AddDays(7);
        var opportunityId = Guid.NewGuid();
        var plot = GrowingPlot(
            opportunityId,
            plantedAt,
            readyAt);

        Assert.False(CropCareRules.WasGrowingAt(
            plot,
            opportunityId,
            plantedAt.AddTicks(-1)));
        Assert.True(CropCareRules.WasGrowingAt(
            plot,
            opportunityId,
            plantedAt));
        Assert.True(CropCareRules.WasGrowingAt(
            plot,
            opportunityId,
            readyAt.AddTicks(-1)));
        Assert.False(CropCareRules.WasGrowingAt(
            plot,
            opportunityId,
            readyAt));
        Assert.False(CropCareRules.WasGrowingAt(
            plot,
            Guid.NewGuid(),
            plantedAt.AddDays(1)));
    }

    [Fact]
    public void WasGrowingAt_RejectsLockedPlotWithInconsistentCropState()
    {
        var plantedAt = Utc(2026, 7, 26, 10);
        var opportunityId = Guid.NewGuid();
        var plot = GrowingPlot(
            opportunityId,
            plantedAt,
            plantedAt.AddHours(1));
        plot.Unlocked = false;

        Assert.False(CropCareRules.WasGrowingAt(
            plot,
            opportunityId,
            plantedAt.AddMinutes(1)));
    }

    [Fact]
    public void ClearCurrentOpportunity_RemovesCropCycleToken()
    {
        var plot = new Plot
        {
            CareOpportunityId = Guid.NewGuid()
        };

        CropCareRules.ClearCurrentOpportunity(plot);

        Assert.Null(plot.CareOpportunityId);
    }

    private static Plot GrowingPlot(
        Guid opportunityId,
        DateTime plantedAt,
        DateTime readyAt)
    {
        return new Plot
        {
            Unlocked = true,
            SeedId = "corn",
            PlantedAt = plantedAt,
            ReadyAt = readyAt,
            CareOpportunityId = opportunityId
        };
    }

    private static DateTime Utc(
        int year,
        int month,
        int day,
        int hour)
    {
        return new DateTime(
            year,
            month,
            day,
            hour,
            0,
            0,
            DateTimeKind.Utc);
    }
}
