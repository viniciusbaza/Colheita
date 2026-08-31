using FarmAndFriends.Api.Contracts.Farms;
using FarmAndFriends.Api.Contracts.Land;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Domain.Rules;
using FarmAndFriends.Api.Domain.Services;
using FarmAndFriends.Api.Mappers;
using Xunit;

namespace FarmAndFriends.Api.Tests.Mappers;

public sealed class FarmMapperTests
{
    [Fact]
    public void OwnerCareState_ShowsObservedDeduplicatedCaregiversAndTotalCount()
    {
        var now = Utc(2026, 7, 26, 10);
        var ownerId = Guid.NewGuid();
        var farm = CreateFarm(ownerId, now);
        var plot = Assert.Single(farm.Plots);
        var ana = new CropCaregiverState(
            Guid.NewGuid(),
            "Ana",
            now.AddMinutes(-1));
        var bruno = new CropCaregiverState(
            Guid.NewGuid(),
            "Bruno",
            now.AddMinutes(-2));

        var response = FarmMapper.ToFarmResponse(
            farm,
            now,
            CareStates(
                plot,
                canCare: false,
                rewardAvailable: false,
                viewerCare: null,
                caregiverCount: 4,
                ana,
                bruno));

        var care = Assert.Single(response.Plots).Care;
        Assert.NotNull(care);
        Assert.Equal("observed", care.Status);
        Assert.False(care.CanCare);
        Assert.False(care.RewardAvailable);
        Assert.False(care.ViewerCared);
        Assert.Null(care.NextCareAt);
        Assert.Equal(4, care.CaregiverCount);
        Assert.Collection(
            care.Caregivers,
            caregiver => AssertCaregiver(caregiver, ana),
            caregiver => AssertCaregiver(caregiver, bruno));
        Assert.Equal(
            care.Caregivers.Count,
            care.Caregivers.Select(caregiver => caregiver.UserId)
                .Distinct()
                .Count());
        Assert.Equal(ana.CaredAt, care.CaredAt);
        Assert.Equal(ana.UserId, care.CaredByUserId);
        Assert.Equal(ana.Username, care.CaredByUsername);
    }

    [Fact]
    public void VisitorCareState_ShowsCooldownAndOnlyViewersOwnCompletion()
    {
        var now = Utc(2026, 7, 26, 10);
        var ownerId = Guid.NewGuid();
        var farm = CreateFarm(ownerId, now);
        var plot = Assert.Single(farm.Plots);
        var viewerCare = new CropCaregiverState(
            Guid.NewGuid(),
            "Bruno",
            now.AddSeconds(-10));
        var nextCareAt = viewerCare.CaredAt.AddHours(5);
        var cycleEndsAt = now.AddHours(4);

        var response = FarmMapper.ToFarmResponse(
            farm,
            now,
            CareStates(
                plot,
                canCare: false,
                rewardAvailable: false,
                viewerCare,
                caregiverCount: 0,
                nextCareAt,
                cycleEndsAt));

        var care = Assert.Single(response.Plots).Care;
        Assert.NotNull(care);
        Assert.Equal("cooldown", care.Status);
        Assert.False(care.CanCare);
        Assert.False(care.RewardAvailable);
        Assert.True(care.ViewerCared);
        Assert.Equal(nextCareAt, care.NextCareAt);
        Assert.Equal(cycleEndsAt, care.CareCycleEndsAt);
        Assert.Equal(viewerCare.CaredAt, care.CaredAt);
        Assert.Equal(viewerCare.UserId, care.CaredByUserId);
        Assert.Equal(viewerCare.Username, care.CaredByUsername);
        Assert.Equal(0, care.CaregiverCount);
        Assert.Empty(care.Caregivers);
    }

    [Fact]
    public void VisitorCareState_ReturnsToAvailableAtFiveHourBoundary()
    {
        var caredAt = Utc(2026, 7, 26, 10);
        var nextCareAt = caredAt.AddHours(5);
        var ownerId = Guid.NewGuid();
        var farm = CreateFarm(ownerId, caredAt);
        var plot = Assert.Single(farm.Plots);
        var viewerCare = new CropCaregiverState(
            Guid.NewGuid(),
            "Bruno",
            caredAt);

        var cooldownResponse = FarmMapper.ToFarmResponse(
            farm,
            nextCareAt.AddTicks(-1),
            CareStates(
                plot,
                canCare: false,
                rewardAvailable: false,
                viewerCare,
                caregiverCount: 0,
                nextCareAt,
                careCycleEndsAt: null));
        var availableResponse = FarmMapper.ToFarmResponse(
            farm,
            nextCareAt,
            CareStates(
                plot,
                canCare: true,
                rewardAvailable: true,
                viewerCare,
                caregiverCount: 0));

        var cooldownCare = Assert.Single(cooldownResponse.Plots).Care;
        Assert.NotNull(cooldownCare);
        Assert.Equal("cooldown", cooldownCare.Status);
        Assert.Equal(nextCareAt, cooldownCare.NextCareAt);

        var availableCare = Assert.Single(availableResponse.Plots).Care;
        Assert.NotNull(availableCare);
        Assert.Equal("available", availableCare.Status);
        Assert.True(availableCare.CanCare);
        Assert.True(availableCare.RewardAvailable);
        Assert.True(availableCare.ViewerCared);
        Assert.Null(availableCare.NextCareAt);
        Assert.False(Assert.Single(availableResponse.Plots).IsReady);
    }

    [Fact]
    public void OtherVisitor_CanCareWithoutSeeingExistingCaregivers()
    {
        var now = Utc(2026, 7, 26, 10);
        var ownerId = Guid.NewGuid();
        var farm = CreateFarm(ownerId, now);
        var plot = Assert.Single(farm.Plots);
        var existingCare = new CropCaregiverState(
            Guid.NewGuid(),
            "Bruno",
            now.AddSeconds(-10));

        var ownerResponse = FarmMapper.ToFarmResponse(
            farm,
            now,
            CareStates(
                plot,
                canCare: false,
                rewardAvailable: false,
                viewerCare: null,
                caregiverCount: 1,
                existingCare));
        var visitorResponse = FarmMapper.ToFarmResponse(
            farm,
            now,
            CareStates(
                plot,
                canCare: true,
                rewardAvailable: true,
                viewerCare: null,
                caregiverCount: 0));

        var ownerCare = Assert.Single(ownerResponse.Plots).Care;
        Assert.NotNull(ownerCare);
        Assert.Equal("observed", ownerCare.Status);
        Assert.Equal(1, ownerCare.CaregiverCount);
        Assert.Single(ownerCare.Caregivers);

        var visitorCare = Assert.Single(visitorResponse.Plots).Care;
        Assert.NotNull(visitorCare);
        Assert.Equal("available", visitorCare.Status);
        Assert.True(visitorCare.CanCare);
        Assert.True(visitorCare.RewardAvailable);
        Assert.False(visitorCare.ViewerCared);
        Assert.Null(visitorCare.CaredAt);
        Assert.Null(visitorCare.CaredByUserId);
        Assert.Null(visitorCare.CaredByUsername);
        Assert.Null(visitorCare.NextCareAt);
        Assert.Equal(0, visitorCare.CaregiverCount);
        Assert.Empty(visitorCare.Caregivers);
    }

    [Fact]
    public void MissingCareState_DoesNotExposePlotCare()
    {
        var now = Utc(2026, 7, 26, 10);
        var farm = CreateFarm(Guid.NewGuid(), now);

        var response = FarmMapper.ToFarmResponse(
            farm,
            now,
            new Dictionary<Guid, PlotCropCareState>());

        Assert.Null(Assert.Single(response.Plots).Care);
    }

    [Fact]
    public void ActivePestAndExpiredProtection_AreMappedAuthoritatively()
    {
        var now = Utc(2026, 7, 26, 10);
        var farm = CreateFarm(Guid.NewGuid(), now);
        var plot = Assert.Single(farm.Plots);
        plot.ReadyAt = now.AddMinutes(-30);
        plot.RemainingYield = 2;
        plot.PestType = PestType.Caterpillar;
        plot.PestStatus = PestStatus.Active;
        plot.PestScheduledAt = now.AddMinutes(-10);
        plot.PestAppearsAt = now.AddMinutes(-5);
        plot.PestAppearedAt = now.AddMinutes(-5);
        plot.PestConsumesAt = now.AddMinutes(10);
        plot.ProtectedUntil = now.AddHours(-1);

        var response = FarmMapper.ToFarmResponse(
            farm,
            now,
            new Dictionary<Guid, PlotCropCareState>());
        var mappedPlot = Assert.Single(response.Plots);

        Assert.Null(mappedPlot.ProtectedUntil);
        Assert.Equal(2, mappedPlot.RemainingYield);
        Assert.NotNull(mappedPlot.Pest);
        Assert.Equal("caterpillar", mappedPlot.Pest.Type);
        Assert.Equal("active", mappedPlot.Pest.Status);
        Assert.True(mappedPlot.Pest.CanRemove);
        Assert.Equal(plot.PestConsumesAt, mappedPlot.Pest.ConsumesAt);
    }

    [Fact]
    public void FarmResponse_ExposesServerComputedNextPestCheckAt()
    {
        var now = Utc(2026, 7, 26, 10);
        var nextPestCheckAt = now.AddMinutes(15);
        var farm = CreateFarm(Guid.NewGuid(), now);

        var response = FarmMapper.ToFarmResponse(
            farm,
            now,
            new Dictionary<Guid, PlotCropCareState>(),
            nextPestCheckAt);

        Assert.Equal(nextPestCheckAt, response.NextPestCheckAt);
    }

    [Fact]
    public void OwnerView_IncludesLockedPlotsAndLandOffer()
    {
        var now = Utc(2026, 8, 11, 10);
        var farm = CreateFarm(Guid.NewGuid(), now);
        farm.Plots.Add(new Plot
        {
            Id = Guid.NewGuid(),
            FarmId = farm.Id,
            Farm = farm,
            X = 1,
            Y = 0,
            Unlocked = false
        });
        var locked = farm.Plots.Single(plot => !plot.Unlocked);
        var offer = new LandOfferResponse(
            locked.Id,
            7,
            28,
            2,
            new LandPricesResponse(500, 2),
            null);

        var response = FarmMapper.ToFarmResponse(
            farm,
            now,
            new Dictionary<Guid, PlotCropCareState>(),
            landOffer: offer,
            isOwnerView: true);

        Assert.Equal(2, response.Plots.Count);
        Assert.Equal(offer, response.LandOffer);
    }

    [Fact]
    public void VisitorView_IncludesFullInitialGridAndAlwaysHidesLandOffer()
    {
        var now = Utc(2026, 8, 11, 10);
        var farm = CreateFarm(Guid.NewGuid(), now);
        PopulateCanonicalLayout(farm, plotCount: 9, unlockedCount: 6);
        var locked = farm.Plots.First(plot => !plot.Unlocked);
        var offer = new LandOfferResponse(
            locked.Id,
            7,
            28,
            2,
            new LandPricesResponse(500, 2),
            null);

        var response = FarmMapper.ToFarmResponse(
            farm,
            now,
            new Dictionary<Guid, PlotCropCareState>(),
            landOffer: offer,
            isOwnerView: false);

        Assert.Equal(9, response.Plots.Count);
        Assert.Equal(6, response.Plots.Count(plot => plot.Unlocked));
        Assert.Equal(3, response.Plots.Count(plot => !plot.Unlocked));
        Assert.Null(response.LandOffer);
    }

    [Fact]
    public void VisitorView_IncludesFullExpandedGridAndAlwaysHidesLandOffer()
    {
        var now = Utc(2026, 8, 11, 10);
        var farm = CreateFarm(Guid.NewGuid(), now);
        PopulateCanonicalLayout(farm, plotCount: 28, unlockedCount: 12);
        var locked = farm.Plots.First(plot => !plot.Unlocked);
        locked.SeedId = "corn";
        locked.PlantedAt = now.AddHours(-2);
        locked.CurrentHarvestCycle = 1;
        locked.CurrentHarvestCycleStartedAt = now.AddHours(-2);
        locked.ReadyAt = now.AddHours(-1);
        locked.RemainingYield = 3;
        locked.ProtectedUntil = now.AddHours(1);
        locked.CareOpportunityId = Guid.NewGuid();
        var offer = new LandOfferResponse(
            locked.Id,
            13,
            28,
            5,
            new LandPricesResponse(10_000, 5),
            null);

        var response = FarmMapper.ToFarmResponse(
            farm,
            now,
            new Dictionary<Guid, PlotCropCareState>(),
            landOffer: offer,
            isOwnerView: false);

        Assert.Equal(28, response.Plots.Count);
        Assert.Equal(12, response.Plots.Count(plot => plot.Unlocked));
        Assert.Equal(16, response.Plots.Count(plot => !plot.Unlocked));
        Assert.Equal(6, response.Plots.Max(plot => plot.X));
        Assert.Equal(3, response.Plots.Max(plot => plot.Y));
        var lockedResponse = response.Plots.Single(plot => plot.Id == locked.Id);
        Assert.Null(lockedResponse.SeedId);
        Assert.Null(lockedResponse.PlantedAt);
        Assert.Null(lockedResponse.CurrentHarvestCycle);
        Assert.False(lockedResponse.IsReady);
        Assert.Null(lockedResponse.ReadyAt);
        Assert.Null(lockedResponse.RemainingYield);
        Assert.Null(lockedResponse.ProtectedUntil);
        Assert.Null(lockedResponse.Pest);
        Assert.Null(lockedResponse.Care);
        Assert.Null(response.LandOffer);
    }

    private static void PopulateCanonicalLayout(
        Farm farm,
        int plotCount,
        int unlockedCount)
    {
        farm.Plots.Clear();

        for (var plotNumber = 1; plotNumber <= plotCount; plotNumber++)
        {
            var definition = FarmLayoutRules.GetDefinition(plotNumber);
            farm.Plots.Add(new Plot
            {
                Id = Guid.NewGuid(),
                FarmId = farm.Id,
                Farm = farm,
                X = definition.X,
                Y = definition.Y,
                Unlocked = plotNumber <= unlockedCount
            });
        }
    }

    private static Farm CreateFarm(Guid ownerId, DateTime now)
    {
        var farm = new Farm
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda de teste",
            UserId = ownerId,
            User = CreateUser("Dona")
        };
        farm.User.Id = ownerId;

        farm.Plots.Add(new Plot
        {
            Id = Guid.NewGuid(),
            FarmId = farm.Id,
            Farm = farm,
            X = 0,
            Y = 0,
            Unlocked = true,
            SeedId = "corn",
            PlantedAt = now,
            CurrentHarvestCycle = 1,
            CurrentHarvestCycleStartedAt = now,
            ReadyAt = now.AddDays(7),
            CareOpportunityId = Guid.NewGuid()
        });

        return farm;
    }

    private static IReadOnlyDictionary<Guid, PlotCropCareState> CareStates(
        Plot plot,
        bool canCare,
        bool rewardAvailable,
        CropCaregiverState? viewerCare,
        int caregiverCount,
        params CropCaregiverState[] caregivers)
    {
        return CareStates(
            plot,
            canCare,
            rewardAvailable,
            viewerCare,
            caregiverCount,
            nextCareAt: null,
            careCycleEndsAt: null,
            caregivers);
    }

    private static IReadOnlyDictionary<Guid, PlotCropCareState> CareStates(
        Plot plot,
        bool canCare,
        bool rewardAvailable,
        CropCaregiverState? viewerCare,
        int caregiverCount,
        DateTime? nextCareAt,
        DateTime? careCycleEndsAt,
        params CropCaregiverState[] caregivers)
    {
        var state = new PlotCropCareState(
            plot.CareOpportunityId!.Value,
            canCare,
            rewardAvailable,
            nextCareAt,
            careCycleEndsAt,
            viewerCare,
            caregiverCount,
            caregivers);

        return new Dictionary<Guid, PlotCropCareState>
        {
            [plot.Id] = state
        };
    }

    private static void AssertCaregiver(
        PlotCaregiverResponse actual,
        CropCaregiverState expected)
    {
        Assert.Equal(expected.UserId, actual.UserId);
        Assert.Equal(expected.Username, actual.Username);
        Assert.Equal(expected.CaredAt, actual.CaredAt);
    }

    private static User CreateUser(string username)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            NormalizedUsername = username.ToUpperInvariant(),
            PasswordHash = "test"
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
