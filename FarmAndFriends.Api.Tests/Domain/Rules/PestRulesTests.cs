using FarmAndFriends.Api.Configuration;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Domain.Rules;
using Xunit;

namespace FarmAndFriends.Api.Tests.Domain.Rules;

public sealed class PestRulesTests
{
    private static readonly DateTime Now =
        new(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("PestScenario", "1")]
    public void GrowingCrop_IsNotEligible()
    {
        var plot = ReadyPlot(3);
        plot.ReadyAt = Now.AddMinutes(1);

        Assert.False(PestRules.IsEligible(
            plot,
            totalYield: 3,
            Now,
            Options()));
    }

    [Fact]
    [Trait("PestScenario", "2")]
    public void ReadyCropInsideSafetyPeriod_IsNotEligible()
    {
        var plot = ReadyPlot(3);
        plot.ReadyAt = Now.AddMinutes(-14);

        Assert.False(PestRules.IsEligible(
            plot,
            totalYield: 3,
            Now,
            Options()));
    }

    [Fact]
    [Trait("PestScenario", "3")]
    public void ReadyCropAtSafetyBoundary_IsEligible()
    {
        var plot = ReadyPlot(3);
        plot.ReadyAt = Now.AddMinutes(-15);

        Assert.True(PestRules.IsEligible(
            plot,
            totalYield: 3,
            Now,
            Options()));
    }

    [Fact]
    [Trait("PestScenario", "4")]
    public void CropWithTotalYieldOne_IsNotEligible()
    {
        var plot = ReadyPlot(1);

        Assert.False(PestRules.IsEligible(
            plot,
            totalYield: 1,
            Now,
            Options()));
    }

    [Fact]
    [Trait("PestScenario", "5")]
    public void ProtectedPlot_IsNotEligible()
    {
        var plot = ReadyPlot(3);
        plot.ProtectedUntil = Now.AddHours(1);

        Assert.False(PestRules.IsEligible(
            plot,
            totalYield: 3,
            Now,
            Options()));
    }

    [Fact]
    public void ExpiredProtection_DoesNotBlockEligibility()
    {
        var plot = ReadyPlot(3);
        plot.ProtectedUntil = Now;

        Assert.True(PestRules.IsEligible(
            plot,
            totalYield: 3,
            Now,
            Options()));
    }

    [Fact]
    [Trait("PestScenario", "6")]
    public void EveryEligiblePlot_CanHaveAnIndependentActivePest()
    {
        var plots = Enumerable.Range(0, 9)
            .Select(_ => ReadyPlot(3))
            .ToArray();

        foreach (var plot in plots)
        {
            Assert.True(PestRules.IsEligible(
                plot,
                totalYield: 3,
                Now,
                Options()));
            PestRules.Schedule(plot, Now, Now);
            PestRules.Activate(plot, Now, Options());
        }

        Assert.All(
            plots,
            plot => Assert.Equal(PestStatus.Active, plot.PestStatus));
    }

    [Fact]
    public void FarmActiveLimit_BlocksAnotherActivation()
    {
        var plot = ReadyPlot(3);
        PestRules.Schedule(plot, Now, Now);

        Assert.False(PestRules.CanActivate(
            plot,
            activePestCount: 9,
            lastFarmInfestationAt: null,
            Now,
            Options()));
    }

    [Fact]
    public void FarmInfestationInterval_BlocksUntilBoundary()
    {
        var plot = ReadyPlot(3);
        PestRules.Schedule(plot, Now, Now);

        Assert.False(PestRules.CanActivate(
            plot,
            activePestCount: 0,
            lastFarmInfestationAt: Now,
            Now,
            Options()));
        Assert.True(PestRules.CanActivate(
            plot,
            activePestCount: 0,
            lastFarmInfestationAt: Now,
            Now.AddMinutes(1),
            Options()));
    }

    [Fact]
    public void Activation_StartsFullReactionWindowAtActualActivation()
    {
        var plot = ReadyPlot(3);
        PestRules.Schedule(
            plot,
            Now.AddMinutes(-10),
            Now.AddMinutes(-5));

        PestRules.Activate(plot, Now, Options());

        Assert.Equal(Now, plot.PestAppearedAt);
        Assert.Equal(Now.AddMinutes(15), plot.PestConsumesAt);
    }

    [Fact]
    public void ScheduledAndActivePestKeepOneStableOccurrenceIdentity()
    {
        var plot = ReadyPlot(3);

        PestRules.Schedule(plot, Now, Now);
        var scheduledOccurrenceId = plot.PestOccurrenceId;
        PestRules.Activate(plot, Now, Options());

        Assert.NotNull(scheduledOccurrenceId);
        Assert.Equal(scheduledOccurrenceId, plot.PestOccurrenceId);
        Assert.Equal(
            scheduledOccurrenceId,
            PestRules.EnsureOccurrenceIdentity(plot));
    }

    [Fact]
    public void LegacyActivePestReceivesOccurrenceIdentityOnDemand()
    {
        var plot = ReadyPlot(3);
        plot.PestType = PestType.Caterpillar;
        plot.PestStatus = PestStatus.Active;

        var occurrenceId = PestRules.EnsureOccurrenceIdentity(plot);

        Assert.NotEqual(Guid.Empty, occurrenceId);
        Assert.Equal(occurrenceId, plot.PestOccurrenceId);
    }

    [Fact]
    [Trait("PestScenario", "7")]
    public void TerminalPest_MakesCycleIneligible()
    {
        var plot = ReadyPlot(3);
        plot.PestType = PestType.Caterpillar;
        plot.PestStatus = PestStatus.Removed;

        Assert.True(PestRules.HasHadPestThisCycle(plot));
        Assert.False(PestRules.IsEligible(
            plot,
            totalYield: 3,
            Now,
            Options()));
    }

    [Fact]
    [Trait("PestScenario", "8")]
    public void RemoveActivePest_IsFreeStateOnlyResolution()
    {
        var plot = ActivePlot(3);

        PestRules.Remove(plot, Now);

        Assert.Equal(PestStatus.Removed, plot.PestStatus);
        Assert.Equal(3, plot.RemainingYield);
        Assert.Equal(Now, plot.PestResolvedAt);
        Assert.Equal(0, plot.PestConsumedAmount);
    }

    [Fact]
    [Trait("PestScenario", "10")]
    public void Consume_DecrementsExactlyOne()
    {
        var plot = ActivePlot(3);

        var consumed = PestRules.Consume(plot, Now, Options());

        Assert.Equal(1, consumed);
        Assert.Equal(2, plot.RemainingYield);
        Assert.Equal(PestStatus.Consumed, plot.PestStatus);
        Assert.Equal(1, plot.PestConsumedAmount);
    }

    [Fact]
    [Trait("PestScenario", "11")]
    public void Consume_NeverTakesLastUnit()
    {
        var plot = ActivePlot(1);

        var consumed = PestRules.Consume(plot, Now, Options());

        Assert.Equal(0, consumed);
        Assert.Equal(1, plot.RemainingYield);
        Assert.Equal(
            PestStatus.CancelledByTheft,
            plot.PestStatus);
    }

    [Fact]
    [Trait("PestScenario", "12")]
    public void RepeatedConsume_IsIdempotent()
    {
        var plot = ActivePlot(3);

        var first = PestRules.Consume(plot, Now, Options());
        var second = PestRules.Consume(
            plot,
            Now.AddMinutes(1),
            Options());

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Equal(2, plot.RemainingYield);
        Assert.Equal(1, plot.PestConsumedAmount);
    }

    [Fact]
    [Trait("PestScenario", "13")]
    public void Theft_DoesNotCancelScheduledPest()
    {
        var plot = ReadyPlot(3);
        PestRules.Schedule(plot, Now, Now.AddMinutes(1));

        var cancelled = PestRules.CancelActiveByTheft(plot, Now);

        Assert.False(cancelled);
        Assert.Equal(PestStatus.Scheduled, plot.PestStatus);
    }

    [Fact]
    [Trait("PestScenario", "14")]
    public void Theft_CancelsActivePest()
    {
        var plot = ActivePlot(3);

        var cancelled = PestRules.CancelActiveByTheft(plot, Now);

        Assert.True(cancelled);
        Assert.Equal(
            PestStatus.CancelledByTheft,
            plot.PestStatus);
    }

    [Theory]
    [Trait("PestScenario", "15")]
    [InlineData(PestStatus.Scheduled)]
    [InlineData(PestStatus.Active)]
    public void Harvest_CancelsUnresolvedPest(PestStatus status)
    {
        var plot = ReadyPlot(3);
        plot.PestType = PestType.Caterpillar;
        plot.PestStatus = status;

        Assert.True(PestRules.CancelByHarvest(plot, Now));
        Assert.Equal(
            PestStatus.CancelledByHarvest,
            plot.PestStatus);
    }

    [Fact]
    [Trait("PestScenario", "17")]
    public void Protection_CancelsActivePest()
    {
        var plot = ActivePlot(3);

        Assert.True(PestRules.CancelByProtection(plot, Now));
        Assert.Equal(
            PestStatus.CancelledByProtection,
            plot.PestStatus);
    }

    [Fact]
    [Trait("PestScenario", "18")]
    public void ResetCycle_ClearsPestButPreservesProtection()
    {
        var plot = ActivePlot(3);
        var protectedUntil = Now.AddHours(4);
        plot.ProtectedUntil = protectedUntil;
        Assert.NotNull(plot.PestOccurrenceId);

        PestRules.ResetCycle(plot);

        Assert.Equal(PestStatus.None, plot.PestStatus);
        Assert.Null(plot.PestType);
        Assert.Null(plot.PestScheduledAt);
        Assert.Null(plot.PestAppearsAt);
        Assert.Null(plot.PestAppearedAt);
        Assert.Null(plot.PestConsumesAt);
        Assert.Null(plot.PestResolvedAt);
        Assert.Equal(0, plot.PestConsumedAmount);
        Assert.Null(plot.PestOccurrenceId);
        Assert.Equal(protectedUntil, plot.ProtectedUntil);
    }

    private static PestOptions Options() => new()
    {
        Enabled = true,
        SafetyPeriodMinutes = 15,
        ReactionWindowMinutes = 15,
        DamageAmount = 1,
        MaxActivePestsPerFarm = 9,
        MinimumInfestationIntervalMinutes = 1
    };

    private static Plot ReadyPlot(int remainingYield) => new()
    {
        Id = Guid.NewGuid(),
        Unlocked = true,
        SeedId = "corn",
        PlantedAt = Now.AddMinutes(-30),
        ReadyAt = Now.AddMinutes(-15),
        RemainingYield = remainingYield
    };

    private static Plot ActivePlot(int remainingYield)
    {
        var plot = ReadyPlot(remainingYield);
        PestRules.Schedule(plot, Now.AddMinutes(-15), Now.AddMinutes(-15));
        PestRules.Activate(plot, Now.AddMinutes(-15), Options());
        return plot;
    }
}
