using System.Security.Claims;
using FarmAndFriends.Api.Configuration;
using FarmAndFriends.Api.Contracts.Plots;
using FarmAndFriends.Api.Controllers;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Domain.Services;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FarmAndFriends.Api.Tests.Integration;

public sealed class MultiHarvestCropIntegrationTests
{
    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task AppleTree_PlantAndThreeHarvests_UsesOneSeedAndEmptiesLast()
    {
        var options = DatabaseOptions();
        var plantedAt = Utc(10);
        var clock = new MutableTimeProvider(plantedAt);
        var userId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var inventoryId = Guid.NewGuid();
        var protectedUntil = plantedAt.AddHours(8);

        await using (var arrange = new AppDbContext(options))
        {
            arrange.Users.Add(CreateUser(userId));
            arrange.Farms.Add(CreateFarm(farmId, userId));
            arrange.Plots.Add(new Plot
            {
                Id = plotId,
                FarmId = farmId,
                X = 0,
                Y = 0,
                Unlocked = true,
                ProtectedUntil = protectedUntil
            });
            arrange.Inventories.Add(new Inventory
            {
                Id = inventoryId,
                UserId = userId,
                Coins = 100
            });
            arrange.InventoryItems.Add(new InventoryItem
            {
                Id = Guid.NewGuid(),
                InventoryId = inventoryId,
                ItemType = ItemType.Seed,
                ItemId = "apple_tree",
                Quantity = 1
            });
            await arrange.SaveChangesAsync();
        }

        Guid firstCareOpportunity;
        await using (var plantContext = new AppDbContext(options))
        {
            var result = await Controller(plantContext, clock, userId)
                .Plant(plotId, new PlantSeedRequest("apple_tree"));
            var response = Assert.IsType<PlantResponse>(
                Assert.IsType<OkObjectResult>(result).Value);

            Assert.Equal(1, response.CurrentHarvestCycle);
            Assert.Equal(plantedAt.AddHours(2), response.ReadyAt);
        }

        await using (var state = new AppDbContext(options))
        {
            var plot = await state.Plots.AsNoTracking()
                .SingleAsync(candidate => candidate.Id == plotId);
            firstCareOpportunity = plot.CareOpportunityId!.Value;
            Assert.Equal(1, plot.CurrentHarvestCycle);
            Assert.Equal(3, plot.RemainingYield);
            Assert.Equal(protectedUntil, plot.ProtectedUntil);
        }

        await using (var missingCycleContext = new AppDbContext(options))
        {
            var missingCycleResult = await Controller(
                    missingCycleContext,
                    clock,
                    userId)
                .Harvest(plotId);
            var conflict = Assert.IsType<ObjectResult>(missingCycleResult);
            Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(conflict.Value);
            Assert.Equal(
                PlotErrorCodes.HarvestCycleMismatch,
                problem.Extensions["code"]);
        }

        clock.Advance(TimeSpan.FromHours(2));
        var firstHarvest = await HarvestAsync(
            options,
            clock,
            userId,
            plotId,
            expectedCycle: 1);
        Assert.Equal(2, firstHarvest.CurrentHarvestCycle);
        Assert.Equal(plantedAt.AddHours(3), firstHarvest.ReadyAt);

        Guid secondCareOpportunity;
        await using (var state = new AppDbContext(options))
        {
            var plot = await state.Plots.AsNoTracking()
                .SingleAsync(candidate => candidate.Id == plotId);
            secondCareOpportunity = plot.CareOpportunityId!.Value;
            Assert.NotEqual(firstCareOpportunity, secondCareOpportunity);
            Assert.Equal(3, plot.RemainingYield);
            Assert.Equal(PestStatus.None, plot.PestStatus);
            Assert.Null(plot.PestOccurrenceId);
            Assert.Equal(protectedUntil, plot.ProtectedUntil);
        }

        clock.Advance(TimeSpan.FromHours(1));
        var secondHarvest = await HarvestAsync(
            options,
            clock,
            userId,
            plotId,
            expectedCycle: 2);
        Assert.Equal(3, secondHarvest.CurrentHarvestCycle);

        await using (var state = new AppDbContext(options))
        {
            var thirdCareOpportunity = await state.Plots
                .Where(candidate => candidate.Id == plotId)
                .Select(plot => plot.CareOpportunityId)
                .SingleAsync();
            Assert.NotNull(thirdCareOpportunity);
            Assert.NotEqual(secondCareOpportunity, thirdCareOpportunity);
        }

        clock.Advance(TimeSpan.FromHours(1));
        var finalHarvest = await HarvestAsync(
            options,
            clock,
            userId,
            plotId,
            expectedCycle: 3);
        Assert.Null(finalHarvest.CurrentHarvestCycle);
        Assert.Null(finalHarvest.ReadyAt);

        await using var assertion = new AppDbContext(options);
        var finalPlot = await assertion.Plots.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == plotId);
        var user = await assertion.Users.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == userId);
        var cropQuantity = await assertion.InventoryItems
            .Where(item =>
                item.InventoryId == inventoryId
                && item.ItemType == ItemType.Crop
                && item.ItemId == "apple_crop")
            .Select(item => item.Quantity)
            .SingleAsync();

        Assert.Null(finalPlot.SeedId);
        Assert.Null(finalPlot.PlantedAt);
        Assert.Null(finalPlot.CurrentHarvestCycle);
        Assert.Null(finalPlot.CurrentHarvestCycleStartedAt);
        Assert.Null(finalPlot.RemainingYield);
        Assert.Null(finalPlot.CareOpportunityId);
        Assert.Equal(protectedUntil, finalPlot.ProtectedUntil);
        Assert.False(await assertion.InventoryItems.AnyAsync(item =>
            item.InventoryId == inventoryId
            && item.ItemType == ItemType.Seed
            && item.ItemId == "apple_tree"));
        Assert.Equal(9, cropQuantity);
        Assert.Equal(5, user.Level);
        Assert.Equal(385, user.CurrentXp);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task Tomato_PlantAndTwoHarvests_ResetsCycleAndEmptiesLast()
    {
        var options = DatabaseOptions();
        var plantedAt = Utc(10);
        var clock = new MutableTimeProvider(plantedAt);
        var userId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var inventoryId = Guid.NewGuid();

        await using (var arrange = new AppDbContext(options))
        {
            arrange.Users.Add(CreateUser(userId));
            arrange.Farms.Add(CreateFarm(farmId, userId));
            arrange.Plots.Add(new Plot
            {
                Id = plotId,
                FarmId = farmId,
                X = 0,
                Y = 0,
                Unlocked = true
            });
            arrange.Inventories.Add(new Inventory
            {
                Id = inventoryId,
                UserId = userId,
                Coins = 100
            });
            arrange.InventoryItems.Add(new InventoryItem
            {
                Id = Guid.NewGuid(),
                InventoryId = inventoryId,
                ItemType = ItemType.Seed,
                ItemId = "tomato",
                Quantity = 1
            });
            await arrange.SaveChangesAsync();
        }

        Guid firstCareOpportunity;
        await using (var plantContext = new AppDbContext(options))
        {
            var result = await Controller(plantContext, clock, userId)
                .Plant(plotId, new PlantSeedRequest("tomato"));
            var response = Assert.IsType<PlantResponse>(
                Assert.IsType<OkObjectResult>(result).Value);

            Assert.Equal(1, response.CurrentHarvestCycle);
            Assert.Equal(plantedAt.AddMinutes(2), response.ReadyAt);
            Assert.Equal(10, response.XpGained);
        }

        await using (var state = new AppDbContext(options))
        {
            var plot = await state.Plots.AsNoTracking()
                .SingleAsync(candidate => candidate.Id == plotId);
            firstCareOpportunity = plot.CareOpportunityId!.Value;
            Assert.Equal("tomato", plot.SeedId);
            Assert.Equal(plantedAt, plot.PlantedAt);
            Assert.Equal(1, plot.CurrentHarvestCycle);
            Assert.Equal(4, plot.RemainingYield);
        }

        clock.Advance(TimeSpan.FromMinutes(2));
        var firstHarvest = await HarvestAsync(
            options,
            clock,
            userId,
            plotId,
            expectedCycle: 1);

        Assert.Equal("tomato_crop", firstHarvest.Crop);
        Assert.Equal(4, firstHarvest.Amount);
        Assert.Equal(4, firstHarvest.InventoryTotal);
        Assert.Equal(75, firstHarvest.XpGained);
        Assert.Equal(2, firstHarvest.CurrentHarvestCycle);
        Assert.Equal(plantedAt.AddMinutes(4), firstHarvest.ReadyAt);

        await using (var state = new AppDbContext(options))
        {
            var plot = await state.Plots.AsNoTracking()
                .SingleAsync(candidate => candidate.Id == plotId);
            Assert.Equal("tomato", plot.SeedId);
            Assert.Equal(plantedAt, plot.PlantedAt);
            Assert.Equal(2, plot.CurrentHarvestCycle);
            Assert.Equal(plantedAt.AddMinutes(2),
                plot.CurrentHarvestCycleStartedAt);
            Assert.Equal(plantedAt.AddMinutes(4), plot.ReadyAt);
            Assert.Equal(4, plot.RemainingYield);
            Assert.NotNull(plot.CareOpportunityId);
            Assert.NotEqual(firstCareOpportunity, plot.CareOpportunityId);
            Assert.Equal(PestStatus.None, plot.PestStatus);
            Assert.Null(plot.PestOccurrenceId);
        }

        clock.Advance(TimeSpan.FromMinutes(2));
        var finalHarvest = await HarvestAsync(
            options,
            clock,
            userId,
            plotId,
            expectedCycle: 2);

        Assert.Equal("tomato_crop", finalHarvest.Crop);
        Assert.Equal(4, finalHarvest.Amount);
        Assert.Equal(8, finalHarvest.InventoryTotal);
        Assert.Equal(75, finalHarvest.XpGained);
        Assert.Null(finalHarvest.CurrentHarvestCycle);
        Assert.Null(finalHarvest.ReadyAt);

        await using var assertion = new AppDbContext(options);
        var finalPlot = await assertion.Plots.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == plotId);
        var user = await assertion.Users.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == userId);
        var cropQuantity = await assertion.InventoryItems
            .Where(item =>
                item.InventoryId == inventoryId
                && item.ItemType == ItemType.Crop
                && item.ItemId == "tomato_crop")
            .Select(item => item.Quantity)
            .SingleAsync();

        Assert.Null(finalPlot.SeedId);
        Assert.Null(finalPlot.PlantedAt);
        Assert.Null(finalPlot.ReadyAt);
        Assert.Null(finalPlot.CurrentHarvestCycle);
        Assert.Null(finalPlot.CurrentHarvestCycleStartedAt);
        Assert.Null(finalPlot.RemainingYield);
        Assert.Null(finalPlot.CareOpportunityId);
        Assert.False(await assertion.InventoryItems.AnyAsync(item =>
            item.InventoryId == inventoryId
            && item.ItemType == ItemType.Seed
            && item.ItemId == "tomato"));
        Assert.Equal(8, cropQuantity);
        Assert.Equal(5, user.Level);
        Assert.Equal(160, user.CurrentXp);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task ConcurrentHarvests_AdvanceAndRewardOneTimeOnly()
    {
        var options = DatabaseOptions();
        var now = Utc(10);
        var userId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var inventoryId = Guid.NewGuid();
        var originalOpportunity = Guid.NewGuid();

        await ArrangeReadyAppleAsync(
            options,
            now,
            userId,
            farmId,
            plotId,
            inventoryId,
            originalOpportunity);

        var clock = new MutableTimeProvider(now);
        await using var firstContext = new AppDbContext(options);
        await using var secondContext = new AppDbContext(options);
        var firstController = Controller(firstContext, clock, userId);
        var secondController = Controller(secondContext, clock, userId);
        var gate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var first = HarvestAfterGate(
            gate.Task,
            firstController,
            plotId,
            expectedCycle: 1);
        var second = HarvestAfterGate(
            gate.Task,
            secondController,
            plotId,
            expectedCycle: 1);
        gate.SetResult();

        var results = await Task.WhenAll(first, second)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Single(results, result => result is OkObjectResult);
        var conflict = Assert.Single(
            results,
            result => result is ObjectResult { StatusCode: 409 });
        var problem = Assert.IsType<ProblemDetails>(
            Assert.IsType<ObjectResult>(conflict).Value);
        Assert.Equal(
            PlotErrorCodes.HarvestCycleMismatch,
            problem.Extensions["code"]);

        await using var assertion = new AppDbContext(options);
        var plot = await assertion.Plots.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == plotId);
        var user = await assertion.Users.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == userId);
        var quantity = await assertion.InventoryItems
            .Where(item =>
                item.InventoryId == inventoryId
                && item.ItemType == ItemType.Crop
                && item.ItemId == "apple_crop")
            .Select(item => item.Quantity)
            .SingleAsync();

        Assert.Equal(2, plot.CurrentHarvestCycle);
        Assert.Equal(3, plot.RemainingYield);
        Assert.NotEqual(originalOpportunity, plot.CareOpportunityId);
        Assert.Equal(now.AddHours(1), plot.ReadyAt);
        Assert.Equal(3, quantity);
        Assert.Equal(125, user.CurrentXp);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task HarvestAtInventoryLimit_RollsBackCycleYieldAndXp()
    {
        var options = DatabaseOptions();
        var now = Utc(10);
        var userId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var inventoryId = Guid.NewGuid();
        var opportunityId = Guid.NewGuid();

        await ArrangeReadyAppleAsync(
            options,
            now,
            userId,
            farmId,
            plotId,
            inventoryId,
            opportunityId,
            cropQuantity: int.MaxValue);

        await using (var harvestContext = new AppDbContext(options))
        {
            var result = await Controller(
                    harvestContext,
                    new MutableTimeProvider(now),
                    userId)
                .Harvest(plotId, new HarvestCropRequest(1));
            var conflict = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        }

        await using var assertion = new AppDbContext(options);
        var plot = await assertion.Plots.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == plotId);
        var user = await assertion.Users.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == userId);
        var quantity = await assertion.InventoryItems
            .Where(item =>
                item.InventoryId == inventoryId
                && item.ItemId == "apple_crop")
            .Select(item => item.Quantity)
            .SingleAsync();

        Assert.Equal(1, plot.CurrentHarvestCycle);
        Assert.Equal(3, plot.RemainingYield);
        Assert.Equal(opportunityId, plot.CareOpportunityId);
        Assert.Equal(int.MaxValue, quantity);
        Assert.Equal(0, user.CurrentXp);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task TheftAtInventoryLimit_RollsBackYieldLogAndXp()
    {
        var options = DatabaseOptions();
        var now = Utc(10);
        var ownerId = Guid.NewGuid();
        var visitorId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var ownerInventoryId = Guid.NewGuid();
        var visitorInventoryId = Guid.NewGuid();

        await ArrangeReadyAppleAsync(
            options,
            now,
            ownerId,
            farmId,
            plotId,
            ownerInventoryId,
            Guid.NewGuid());

        await using (var arrange = new AppDbContext(options))
        {
            arrange.Users.Add(CreateUser(visitorId));
            arrange.Inventories.Add(new Inventory
            {
                Id = visitorInventoryId,
                UserId = visitorId,
                Coins = 100
            });
            arrange.InventoryItems.Add(new InventoryItem
            {
                Id = Guid.NewGuid(),
                InventoryId = visitorInventoryId,
                ItemType = ItemType.Crop,
                ItemId = "apple_crop",
                Quantity = int.MaxValue
            });
            arrange.Friendships.Add(
                AcceptedFriendship(ownerId, visitorId, now));
            await arrange.SaveChangesAsync();
        }

        await using (var theftContext = new AppDbContext(options))
        {
            var result = await CreateTheftController(
                    theftContext,
                    new MutableTimeProvider(now),
                    visitorId,
                    new PestOptions { Enabled = false })
                .Steal(farmId, plotId);
            var conflict = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        }

        await using var assertion = new AppDbContext(options);
        var plot = await assertion.Plots.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == plotId);
        var visitor = await assertion.Users.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == visitorId);
        var quantity = await assertion.InventoryItems
            .Where(item =>
                item.InventoryId == visitorInventoryId
                && item.ItemId == "apple_crop")
            .Select(item => item.Quantity)
            .SingleAsync();

        Assert.Equal(3, plot.RemainingYield);
        Assert.Equal(int.MaxValue, quantity);
        Assert.Equal(0, visitor.CurrentXp);
        Assert.False(await assertion.TheftLogs.AnyAsync(log =>
            log.PlotId == plotId));
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task TheftInFirstCycle_DoesNotReduceSecondCycleYield()
    {
        var options = DatabaseOptions();
        var now = Utc(10);
        var ownerId = Guid.NewGuid();
        var visitorId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var ownerInventoryId = Guid.NewGuid();
        var visitorInventoryId = Guid.NewGuid();

        await ArrangeReadyAppleAsync(
            options,
            now,
            ownerId,
            farmId,
            plotId,
            ownerInventoryId,
            Guid.NewGuid());

        await using (var socialArrange = new AppDbContext(options))
        {
            socialArrange.Users.Add(CreateUser(visitorId));
            socialArrange.Inventories.Add(new Inventory
            {
                Id = visitorInventoryId,
                UserId = visitorId,
                Coins = 100
            });
            socialArrange.Friendships.Add(
                AcceptedFriendship(ownerId, visitorId, now));
            await socialArrange.SaveChangesAsync();
        }

        await using (var theftContext = new AppDbContext(options))
        {
            var experience = new ExperienceService();
            var pest = new PestService(
                theftContext,
                experience,
                Options.Create(new PestOptions { Enabled = false }),
                new MutableTimeProvider(now));
            var controller = new TheftController(
                theftContext,
                new TheftService(),
                experience,
                new FriendshipService(theftContext),
                pest)
            {
                ControllerContext = AuthenticatedController(visitorId)
            };

            Assert.IsType<OkObjectResult>(await controller.Steal(
                farmId,
                plotId));
        }

        int yieldAfterTheft;
        await using (var state = new AppDbContext(options))
        {
            yieldAfterTheft = await state.Plots
                .Where(plot => plot.Id == plotId)
                .Select(plot => plot.RemainingYield!.Value)
                .SingleAsync();
            Assert.InRange(yieldAfterTheft, 1, 2);
        }

        await HarvestAsync(
            options,
            new MutableTimeProvider(now),
            ownerId,
            plotId,
            expectedCycle: 1);

        await using var assertion = new AppDbContext(options);
        var secondCycle = await assertion.Plots.AsNoTracking()
            .SingleAsync(plot => plot.Id == plotId);
        Assert.Equal(2, secondCycle.CurrentHarvestCycle);
        Assert.Equal(3, secondCycle.RemainingYield);
        Assert.NotEqual(yieldAfterTheft, secondCycle.RemainingYield);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task RegrownCycle_CanCreateANewPestOccurrence()
    {
        var options = DatabaseOptions();
        var now = Utc(10);
        var ownerId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var firstOccurrenceId = Guid.NewGuid();
        var clock = new MutableTimeProvider(now);
        var pestOptions = new PestOptions
        {
            Enabled = true,
            SafetyPeriodMinutes = 0,
            MinimumInfestationIntervalMinutes = 0,
            ReactionWindowMinutes = 5,
            DamageAmount = 1,
            MaxActivePestsPerFarm = 9
        };

        await ArrangeReadyAppleAsync(
            options,
            now,
            ownerId,
            farmId,
            plotId,
            Guid.NewGuid(),
            Guid.NewGuid());
        await using (var pestArrange = new AppDbContext(options))
        {
            var plot = await pestArrange.Plots
                .SingleAsync(candidate => candidate.Id == plotId);
            plot.PestOccurrenceId = firstOccurrenceId;
            plot.PestType = PestType.Caterpillar;
            plot.PestStatus = PestStatus.Active;
            plot.PestScheduledAt = now.AddMinutes(-10);
            plot.PestAppearsAt = now.AddMinutes(-5);
            plot.PestAppearedAt = now.AddMinutes(-5);
            plot.PestConsumesAt = now.AddMinutes(10);
            await pestArrange.SaveChangesAsync();
        }

        await using (var harvestContext = new AppDbContext(options))
        {
            var result = await Controller(
                    harvestContext,
                    clock,
                    ownerId,
                    pestOptions)
                .Harvest(plotId, new HarvestCropRequest(1));
            Assert.IsType<OkObjectResult>(result);
        }

        clock.Advance(TimeSpan.FromHours(1));
        await using (var processContext = new AppDbContext(options))
        {
            var experience = new ExperienceService();
            var pestService = new PestService(
                processContext,
                experience,
                Options.Create(pestOptions),
                clock);
            Assert.True(await pestService.ProcessFarmAsync(farmId));
        }

        await using var assertion = new AppDbContext(options);
        var secondCycle = await assertion.Plots.AsNoTracking()
            .SingleAsync(plot => plot.Id == plotId);
        Assert.Equal(2, secondCycle.CurrentHarvestCycle);
        Assert.NotNull(secondCycle.PestOccurrenceId);
        Assert.NotEqual(firstOccurrenceId, secondCycle.PestOccurrenceId);
        Assert.Equal(PestStatus.Active, secondCycle.PestStatus);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task NewCareOpportunity_DoesNotRewardRetroactiveActiveCycle()
    {
        var options = DatabaseOptions();
        var now = Utc(10);
        var ownerId = Guid.NewGuid();
        var visitorId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var visitorInventoryId = Guid.NewGuid();
        var existingCareCycleId = Guid.NewGuid();

        await ArrangeReadyAppleAsync(
            options,
            now,
            ownerId,
            farmId,
            plotId,
            Guid.NewGuid(),
            Guid.NewGuid());
        await using (var careArrange = new AppDbContext(options))
        {
            careArrange.Users.Add(CreateUser(visitorId));
            careArrange.Inventories.Add(new Inventory
            {
                Id = visitorInventoryId,
                UserId = visitorId,
                Coins = 100
            });
            careArrange.Friendships.Add(
                AcceptedFriendship(ownerId, visitorId, now));
            careArrange.VisitorFarmCareCycles.Add(new VisitorFarmCareCycle
            {
                Id = existingCareCycleId,
                VisitorUserId = visitorId,
                OwnerUserId = ownerId,
                FarmId = farmId,
                StartedAt = now.AddMinutes(-30),
                EndsAt = now.AddHours(4),
                RewardGranted = true,
                CoinsReward = 2,
                XpReward = 5
            });
            await careArrange.SaveChangesAsync();
        }

        await HarvestAsync(
            options,
            new MutableTimeProvider(now),
            ownerId,
            plotId,
            expectedCycle: 1);

        Guid newOpportunityId;
        await using (var state = new AppDbContext(options))
        {
            newOpportunityId = await state.Plots
                .Where(plot => plot.Id == plotId)
                .Select(plot => plot.CareOpportunityId!.Value)
                .SingleAsync();
        }

        CropCareAttempt attempt;
        await using (var careContext = new AppDbContext(options))
        {
            var careService = new CropCareService(
                careContext,
                new ExperienceService(),
                Options.Create(new CropCareOptions()),
                NullLogger<CropCareService>.Instance,
                new MutableTimeProvider(now));
            attempt = await careService.CareAsync(
                visitorId,
                farmId,
                plotId,
                newOpportunityId,
                Guid.NewGuid());
        }

        Assert.True(attempt.Succeeded);
        Assert.Equal(0, attempt.Response!.CoinsGained);
        Assert.Equal(0, attempt.Response.XpGained);
        Assert.Equal(existingCareCycleId, attempt.Response.CareCycleId);

        await using var assertion = new AppDbContext(options);
        var visitor = await assertion.Users.AsNoTracking()
            .SingleAsync(user => user.Id == visitorId);
        var inventory = await assertion.Inventories.AsNoTracking()
            .SingleAsync(candidate => candidate.UserId == visitorId);
        var completion = await assertion.CropCareCompletions.AsNoTracking()
            .SingleAsync(candidate =>
                candidate.CareOpportunityId == newOpportunityId);
        Assert.Equal(0, visitor.CurrentXp);
        Assert.Equal(100, inventory.Coins);
        Assert.Equal(0, completion.CoinsGained);
        Assert.Equal(0, completion.XpGained);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task InvalidCycle_TheftAndPestGrantNothingAndApplyNoDamage()
    {
        var options = DatabaseOptions();
        var now = Utc(10);
        var ownerId = Guid.NewGuid();
        var visitorId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var visitorInventoryId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();
        var pestOptions = new PestOptions
        {
            Enabled = true,
            SafetyPeriodMinutes = 0,
            DamageAmount = 1
        };

        await ArrangeReadyAppleAsync(
            options,
            now,
            ownerId,
            farmId,
            plotId,
            Guid.NewGuid(),
            Guid.NewGuid());
        await using (var invalidArrange = new AppDbContext(options))
        {
            invalidArrange.Users.Add(CreateUser(visitorId));
            invalidArrange.Inventories.Add(new Inventory
            {
                Id = visitorInventoryId,
                UserId = visitorId,
                Coins = 100
            });
            invalidArrange.Friendships.Add(
                AcceptedFriendship(ownerId, visitorId, now));
            var plot = await invalidArrange.Plots
                .SingleAsync(candidate => candidate.Id == plotId);
            plot.CurrentHarvestCycle = 4;
            plot.PestOccurrenceId = occurrenceId;
            plot.PestType = PestType.Caterpillar;
            plot.PestStatus = PestStatus.Active;
            plot.PestScheduledAt = now.AddMinutes(-10);
            plot.PestAppearsAt = now.AddMinutes(-5);
            plot.PestAppearedAt = now.AddMinutes(-5);
            plot.PestConsumesAt = now.AddMinutes(-1);
            await invalidArrange.SaveChangesAsync();
        }

        await using (var theftContext = new AppDbContext(options))
        {
            var result = await CreateTheftController(
                    theftContext,
                    new MutableTimeProvider(now),
                    visitorId,
                    pestOptions)
                .Steal(farmId, plotId);
            var failure = Assert.IsType<ObjectResult>(result);
            Assert.Equal(
                StatusCodes.Status500InternalServerError,
                failure.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(failure.Value);
            Assert.Equal(
                PlotErrorCodes.CropCycleStateInvalid,
                problem.Extensions["code"]);
        }

        await using (var processContext = new AppDbContext(options))
        {
            var experience = new ExperienceService();
            var pestService = new PestService(
                processContext,
                experience,
                Options.Create(pestOptions),
                new MutableTimeProvider(now));
            Assert.True(await pestService.ProcessFarmAsync(farmId));
        }

        await using (var removalContext = new AppDbContext(options))
        {
            var experience = new ExperienceService();
            var pestService = new PestService(
                removalContext,
                experience,
                Options.Create(pestOptions),
                new MutableTimeProvider(now));
            var attempt = await pestService.RemoveAsync(
                ownerId,
                farmId,
                plotId,
                occurrenceId,
                Guid.NewGuid());
            Assert.Equal(
                PestActionFailure.CropCycleStateInvalid,
                attempt.Failure);
        }

        await using var assertion = new AppDbContext(options);
        var invalidPlot = await assertion.Plots.AsNoTracking()
            .SingleAsync(plot => plot.Id == plotId);
        var visitor = await assertion.Users.AsNoTracking()
            .SingleAsync(user => user.Id == visitorId);
        Assert.Equal(4, invalidPlot.CurrentHarvestCycle);
        Assert.Equal(3, invalidPlot.RemainingYield);
        Assert.Equal(PestStatus.Active, invalidPlot.PestStatus);
        Assert.Equal(0, visitor.CurrentXp);
        Assert.False(await assertion.TheftLogs.AnyAsync(log =>
            log.PlotId == plotId));
        Assert.False(await assertion.PestRemovalCompletions.AnyAsync(
            completion => completion.PlotId == plotId));
        Assert.False(await assertion.Notifications.AnyAsync(notification =>
            notification.PestPlotId == plotId));
        Assert.False(await assertion.InventoryItems.AnyAsync(item =>
            item.InventoryId == visitorInventoryId
            && item.ItemId == "apple_crop"));
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task ConcurrentAppleHarvestAndTheft_ConserveOldYieldAndAdvanceOnce()
    {
        var options = DatabaseOptions();
        var now = Utc(10);
        var ownerId = Guid.NewGuid();
        var visitorId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var ownerInventoryId = Guid.NewGuid();
        var visitorInventoryId = Guid.NewGuid();
        var clock = new MutableTimeProvider(now);

        await ArrangeReadyAppleAsync(
            options,
            now,
            ownerId,
            farmId,
            plotId,
            ownerInventoryId,
            Guid.NewGuid());
        await using (var socialArrange = new AppDbContext(options))
        {
            socialArrange.Users.Add(CreateUser(visitorId));
            socialArrange.Inventories.Add(new Inventory
            {
                Id = visitorInventoryId,
                UserId = visitorId,
                Coins = 100
            });
            socialArrange.Friendships.Add(
                AcceptedFriendship(ownerId, visitorId, now));
            await socialArrange.SaveChangesAsync();
        }

        await using var harvestContext = new AppDbContext(options);
        await using var theftContext = new AppDbContext(options);
        var harvestController = Controller(
            harvestContext,
            clock,
            ownerId);
        var theftController = CreateTheftController(
            theftContext,
            clock,
            visitorId,
            new PestOptions { Enabled = false });
        var gate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var harvestTask = HarvestAfterGate(
            gate.Task,
            harvestController,
            plotId,
            expectedCycle: 1);
        var theftTask = TheftAfterGate(
            gate.Task,
            theftController,
            farmId,
            plotId);
        gate.SetResult();

        var results = await Task.WhenAll(
                harvestTask,
                theftTask)
            .WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsType<OkObjectResult>(results[0]);
        Assert.True(results[1] is OkObjectResult or BadRequestObjectResult);

        await using var assertion = new AppDbContext(options);
        var plot = await assertion.Plots.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == plotId);
        var owner = await assertion.Users.AsNoTracking()
            .SingleAsync(user => user.Id == ownerId);
        var grantedOldYield = await assertion.InventoryItems
            .Where(item =>
                (item.InventoryId == ownerInventoryId
                    || item.InventoryId == visitorInventoryId)
                && item.ItemType == ItemType.Crop
                && item.ItemId == "apple_crop")
            .SumAsync(item => item.Quantity);

        Assert.Equal(2, plot.CurrentHarvestCycle);
        Assert.Equal(3, plot.RemainingYield);
        Assert.Equal(now.AddHours(1), plot.ReadyAt);
        Assert.Equal(3, grantedOldYield);
        Assert.Equal(125, owner.CurrentXp);
        Assert.InRange(
            await assertion.TheftLogs.CountAsync(log =>
                log.PlotId == plotId),
            0,
            1);
    }

    private static async Task<HarvestResponse> HarvestAsync(
        DbContextOptions<AppDbContext> options,
        MutableTimeProvider clock,
        Guid userId,
        Guid plotId,
        int expectedCycle)
    {
        await using var context = new AppDbContext(options);
        var result = await Controller(context, clock, userId)
            .Harvest(plotId, new HarvestCropRequest(expectedCycle));
        return Assert.IsType<HarvestResponse>(
            Assert.IsType<OkObjectResult>(result).Value);
    }

    private static async Task<IActionResult> HarvestAfterGate(
        Task gate,
        PlotController controller,
        Guid plotId,
        int expectedCycle)
    {
        await gate;
        return await controller.Harvest(
            plotId,
            new HarvestCropRequest(expectedCycle));
    }

    private static async Task<IActionResult> TheftAfterGate(
        Task gate,
        TheftController controller,
        Guid farmId,
        Guid plotId)
    {
        await gate;
        return await controller.Steal(farmId, plotId);
    }

    private static async Task ArrangeReadyAppleAsync(
        DbContextOptions<AppDbContext> options,
        DateTime now,
        Guid userId,
        Guid farmId,
        Guid plotId,
        Guid inventoryId,
        Guid opportunityId,
        int? cropQuantity = null)
    {
        await using var arrange = new AppDbContext(options);
        arrange.Users.Add(CreateUser(userId));
        arrange.Farms.Add(CreateFarm(farmId, userId));
        arrange.Plots.Add(new Plot
        {
            Id = plotId,
            FarmId = farmId,
            X = 0,
            Y = 0,
            Unlocked = true,
            SeedId = "apple_tree",
            PlantedAt = now.AddHours(-2),
            CurrentHarvestCycle = 1,
            CurrentHarvestCycleStartedAt = now.AddHours(-2),
            ReadyAt = now,
            RemainingYield = 3,
            CareOpportunityId = opportunityId
        });
        arrange.Inventories.Add(new Inventory
        {
            Id = inventoryId,
            UserId = userId,
            Coins = 100
        });
        if (cropQuantity.HasValue)
        {
            arrange.InventoryItems.Add(new InventoryItem
            {
                Id = Guid.NewGuid(),
                InventoryId = inventoryId,
                ItemType = ItemType.Crop,
                ItemId = "apple_crop",
                Quantity = cropQuantity.Value
            });
        }

        await arrange.SaveChangesAsync();
    }

    private static PlotController Controller(
        AppDbContext context,
        TimeProvider clock,
        Guid userId,
        PestOptions? pestOptions = null)
    {
        var experienceService = new ExperienceService();
        return new PlotController(
            context,
            experienceService,
            new PestService(
                context,
                experienceService,
                Options.Create(pestOptions
                    ?? new PestOptions { Enabled = false }),
                clock))
        {
            ControllerContext = AuthenticatedController(userId)
        };
    }

    private static TheftController CreateTheftController(
        AppDbContext context,
        TimeProvider clock,
        Guid userId,
        PestOptions pestOptions)
    {
        var experienceService = new ExperienceService();
        return new TheftController(
            context,
            new TheftService(),
            experienceService,
            new FriendshipService(context),
            new PestService(
                context,
                experienceService,
                Options.Create(pestOptions),
                clock))
        {
            ControllerContext = AuthenticatedController(userId)
        };
    }

    private static ControllerContext AuthenticatedController(Guid userId) =>
        new()
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(
                        new[]
                        {
                            new Claim(
                                ClaimTypes.NameIdentifier,
                                userId.ToString())
                        },
                        authenticationType: "integration-test"))
            }
        };

    private static Friendship AcceptedFriendship(
        Guid firstUserId,
        Guid secondUserId,
        DateTime now)
    {
        var (userAId, userBId) = FriendshipService.GetCanonicalPair(
            firstUserId,
            secondUserId);
        return new Friendship
        {
            Id = Guid.NewGuid(),
            UserAId = userAId,
            UserBId = userBId,
            RequestedByUserId = secondUserId,
            Status = FriendshipStatus.Accepted,
            CreatedAt = now.AddMinutes(-2),
            RespondedAt = now.AddMinutes(-1)
        };
    }

    private static DbContextOptions<AppDbContext> DatabaseOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable(
                "CROP_CARE_TEST_CONNECTION")!)
            .Options;

    private static User CreateUser(Guid id)
    {
        var username = $"multi_harvest_{id:N}";
        return new User
        {
            Id = id,
            Username = username,
            NormalizedUsername = username.ToUpperInvariant(),
            PasswordHash = "integration-test",
            Level = 5
        };
    }

    private static Farm CreateFarm(Guid id, Guid userId) => new()
    {
        Id = id,
        UserId = userId,
        Name = "Multi-harvest integration farm"
    };

    private static DateTime Utc(int hour) =>
        new(2026, 8, 23, hour, 0, 0, DateTimeKind.Utc);

    private sealed class MutableTimeProvider(DateTime now) : TimeProvider
    {
        private DateTime _now = now;

        public override DateTimeOffset GetUtcNow() =>
            new(DateTime.SpecifyKind(_now, DateTimeKind.Utc));

        public void Advance(TimeSpan duration) => _now = _now.Add(duration);
    }
}
