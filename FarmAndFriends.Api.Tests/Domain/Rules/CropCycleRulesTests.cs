using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Rules;
using Xunit;

namespace FarmAndFriends.Api.Tests.Domain.Rules;

public sealed class CropCycleRulesTests
{
    [Fact]
    public void ValidateSeedConfiguration_AcceptsSingleAndMultiHarvestSeeds()
    {
        CropCycleRules.ValidateSeedConfiguration(Seed(harvestCycles: 1));
        CropCycleRules.ValidateSeedConfiguration(
            Seed(harvestCycles: 3, regrowTime: TimeSpan.FromHours(1)));
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(1, 60.0)]
    [InlineData(2, null)]
    [InlineData(2, 0.0)]
    [InlineData(2, -1.0)]
    public void ValidateSeedConfiguration_RejectsInvalidCycleCombination(
        int harvestCycles,
        double? regrowMinutes)
    {
        var seed = Seed(
            harvestCycles,
            regrowMinutes.HasValue
                ? TimeSpan.FromMinutes(regrowMinutes.Value)
                : null);

        Assert.Throws<InvalidOperationException>(() =>
            CropCycleRules.ValidateSeedConfiguration(seed));
    }

    [Fact]
    public void StartPlanting_InitializesFirstCycleAndFullYield()
    {
        var now = Utc(10);
        var plot = new Plot();
        var seed = Seed(3, TimeSpan.FromHours(1));

        CropCycleRules.StartPlanting(plot, seed, now);

        Assert.Equal(seed.Id, plot.SeedId);
        Assert.Equal(now, plot.PlantedAt);
        Assert.Equal(1, plot.CurrentHarvestCycle);
        Assert.Equal(now, plot.CurrentHarvestCycleStartedAt);
        Assert.Equal(now.AddHours(2), plot.ReadyAt);
        Assert.Equal(3, plot.RemainingYield);
    }

    [Fact]
    public void CompleteHarvest_AdvancesCyclesThenClearsFinalCrop()
    {
        var plantedAt = Utc(10);
        var seed = Seed(3, TimeSpan.FromHours(1));
        var plot = new Plot();
        CropCycleRules.StartPlanting(plot, seed, plantedAt);
        plot.RemainingYield = 1;

        var second = CropCycleRules.CompleteHarvest(
            plot,
            seed,
            plantedAt.AddHours(2));

        Assert.True(second.HasNextCycle);
        Assert.Equal(2, plot.CurrentHarvestCycle);
        Assert.Equal(plantedAt, plot.PlantedAt);
        Assert.Equal(plantedAt.AddHours(2), plot.CurrentHarvestCycleStartedAt);
        Assert.Equal(plantedAt.AddHours(3), plot.ReadyAt);
        Assert.Equal(3, plot.RemainingYield);

        var third = CropCycleRules.CompleteHarvest(
            plot,
            seed,
            plantedAt.AddHours(3));
        Assert.True(third.HasNextCycle);
        Assert.Equal(3, plot.CurrentHarvestCycle);

        var final = CropCycleRules.CompleteHarvest(
            plot,
            seed,
            plantedAt.AddHours(4));

        Assert.False(final.HasNextCycle);
        Assert.Null(plot.SeedId);
        Assert.Null(plot.PlantedAt);
        Assert.Null(plot.CurrentHarvestCycle);
        Assert.Null(plot.CurrentHarvestCycleStartedAt);
        Assert.Null(plot.ReadyAt);
        Assert.Null(plot.RemainingYield);
    }

    [Fact]
    public void CompleteHarvest_SingleHarvestCropClearsImmediately()
    {
        var now = Utc(10);
        var seed = Seed(1);
        var plot = new Plot();
        CropCycleRules.StartPlanting(plot, seed, now);

        var result = CropCycleRules.CompleteHarvest(
            plot,
            seed,
            now.AddHours(2));

        Assert.False(result.HasNextCycle);
        Assert.True(plot.IsEmpty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(4)]
    public void ValidateCurrentCycle_RejectsMissingOrOutOfRangeCycle(
        int? currentCycle)
    {
        var now = Utc(10);
        var seed = Seed(3, TimeSpan.FromHours(1));
        var plot = new Plot
        {
            SeedId = seed.Id,
            PlantedAt = now,
            CurrentHarvestCycle = currentCycle,
            CurrentHarvestCycleStartedAt = now,
            ReadyAt = now.AddHours(1),
            RemainingYield = seed.CropAmount
        };

        Assert.Throws<InvalidOperationException>(() =>
            CropCycleRules.ValidateCurrentCycle(plot, seed));
    }

    private static Seed Seed(
        int harvestCycles,
        TimeSpan? regrowTime = null) => new()
    {
        Id = "apple_tree",
        Name = "Macieira",
        CropName = "Maçã",
        Icon = "🍎",
        GrowTime = TimeSpan.FromHours(2),
        RegrowTime = regrowTime,
        CropId = "apple_crop",
        CropAmount = 3,
        HarvestCycles = harvestCycles
    };

    private static DateTime Utc(int hour) =>
        new(2026, 8, 23, hour, 0, 0, DateTimeKind.Utc);
}
