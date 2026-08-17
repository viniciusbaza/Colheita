using System.Security.Claims;
using FarmAndFriends.Api.Configuration;
using FarmAndFriends.Api.Contracts.Plots;
using FarmAndFriends.Api.Contracts.Pests;
using FarmAndFriends.Api.Contracts.Shop;
using FarmAndFriends.Api.Controllers;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Domain.Services;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace FarmAndFriends.Api.Tests.Integration;

public sealed class PestScenarioIntegrationTests
{
    [PostgresFact]
    [Trait("Category", "Postgres")]
    [Trait("PestScenario", "8")]
    public async Task Scenario08_OwnerCanRemoveActivePestThroughEndpoint()
    {
        var database = DatabaseOptions();
        var now = UtcNowRounded();
        var ownerId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();

        await ArrangeFarmAsync(
            database,
            ownerId,
            farmId,
            ActivePlot(
                plotId,
                farmId,
                now,
                now.AddMinutes(10),
                occurrenceId),
            new Inventory
            {
                Id = Guid.NewGuid(),
                UserId = ownerId,
                Coins = 100
            });

        await using var context = new AppDbContext(database);
        var action = await CreatePestController(context, ownerId, now)
            .Remove(
                farmId,
                plotId,
                new RemovePestRequest(occurrenceId),
                Guid.NewGuid().ToString(),
                CancellationToken.None);

        var response = Assert.IsType<PestActionResponse>(
            Assert.IsType<OkObjectResult>(action).Value);
        Assert.Equal(2, response.CoinsGained);
        Assert.Equal(5, response.XpGained);
        Assert.Equal(102, response.Coins);
        Assert.True(response.RewardGranted);
        Assert.False(response.Replayed);
        Assert.NotEqual(Guid.Empty, response.CompletionId);
        Assert.NotEqual(Guid.Empty, response.PestOccurrenceId);
        await using var assertion = new AppDbContext(database);
        var plot = await assertion.Plots
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == plotId);
        Assert.Equal(PestStatus.Removed, plot.PestStatus);
        Assert.Equal(3, plot.RemainingYield);
        Assert.Equal(response.PestOccurrenceId, plot.PestOccurrenceId);
        Assert.Equal(
            102,
            await assertion.Inventories
                .Where(inventory => inventory.UserId == ownerId)
                .Select(inventory => inventory.Coins)
                .SingleAsync());
        Assert.Equal(
            5,
            await assertion.Users
                .Where(user => user.Id == ownerId)
                .Select(user => user.CurrentXp)
                .SingleAsync());
        Assert.Equal(
            1,
            await assertion.PestRemovalCompletions.CountAsync(
                completion => completion.Id == response.CompletionId));
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    [Trait("PestScenario", "9")]
    public async Task Scenario09_AcceptedFriendCanRemoveActivePestThroughEndpoint()
    {
        var database = DatabaseOptions();
        var now = UtcNowRounded();
        var ownerId = Guid.NewGuid();
        var visitorId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();

        await ArrangeFarmAsync(
            database,
            ownerId,
            farmId,
            ActivePlot(
                plotId,
                farmId,
                now,
                now.AddMinutes(10),
                occurrenceId),
            CreateUser(visitorId, "visitor"),
            CreateAcceptedFriendship(ownerId, visitorId),
            new Inventory
            {
                Id = Guid.NewGuid(),
                UserId = visitorId,
                Coins = 100
            });

        await using var context = new AppDbContext(database);
        var action = await CreatePestController(context, visitorId, now)
            .Remove(
                farmId,
                plotId,
                new RemovePestRequest(occurrenceId),
                Guid.NewGuid().ToString(),
                CancellationToken.None);

        var response = Assert.IsType<PestActionResponse>(
            Assert.IsType<OkObjectResult>(action).Value);
        Assert.Equal(2, response.CoinsGained);
        Assert.Equal(5, response.XpGained);
        Assert.Equal(102, response.Coins);
        Assert.True(response.RewardGranted);
        await using var assertion = new AppDbContext(database);
        var plot = await assertion.Plots
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == plotId);
        Assert.Equal(PestStatus.Removed, plot.PestStatus);
        Assert.Equal(
            102,
            await assertion.Inventories
                .Where(inventory => inventory.UserId == visitorId)
                .Select(inventory => inventory.Coins)
                .SingleAsync());
        Assert.Equal(
            5,
            await assertion.Users
                .Where(user => user.Id == visitorId)
                .Select(user => user.CurrentXp)
                .SingleAsync());
        Assert.Equal(
            1,
            await assertion.Notifications.CountAsync(notification =>
                notification.PestPlotId == plotId
                && notification.ActorUserId == visitorId
                && notification.Type == NotificationType.PestRemoved));
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    [Trait("PestScenario", "16")]
    public async Task Scenario16_BuyThenApplyRepellentConsumesPurchasedInventory()
    {
        var database = DatabaseOptions();
        var now = UtcNowRounded();
        var ownerId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var inventoryId = Guid.NewGuid();

        await ArrangeFarmAsync(
            database,
            ownerId,
            farmId,
            EmptyPlot(plotId, farmId),
            new Inventory
            {
                Id = inventoryId,
                UserId = ownerId,
                Coins = 100
            });

        await using (var buyContext = new AppDbContext(database))
        {
            var purchase = await CreateShopController(
                    buyContext,
                    ownerId)
                .BuyItem(new BuyItemRequest(
                    PestOptions.NaturalRepellentItemId,
                    1));
            Assert.IsType<OkObjectResult>(purchase);
        }

        await using (var protectionContext = new AppDbContext(database))
        {
            var protection = await CreatePestController(
                    protectionContext,
                    ownerId,
                    now)
                .ApplyProtection(
                    farmId,
                    plotId,
                    CancellationToken.None);
            Assert.IsType<OkObjectResult>(protection);
        }

        await using var assertion = new AppDbContext(database);
        var inventory = await assertion.Inventories
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == inventoryId);
        var plot = await assertion.Plots
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == plotId);

        Assert.Equal(70, inventory.Coins);
        Assert.False(await assertion.InventoryItems.AnyAsync(item =>
            item.InventoryId == inventoryId
            && item.ItemId == PestOptions.NaturalRepellentItemId));
        Assert.Equal(now.AddHours(4), plot.ProtectedUntil);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    [Trait("PestScenario", "18")]
    public async Task Scenario18_ProtectionSurvivesHarvestAndReplantUntilExpiration()
    {
        var database = DatabaseOptions();
        var now = UtcNowRounded();
        var ownerId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var inventoryId = Guid.NewGuid();

        await ArrangeFarmAsync(
            database,
            ownerId,
            farmId,
            ActivePlot(plotId, farmId, now, now.AddMinutes(10)),
            new Inventory
            {
                Id = inventoryId,
                UserId = ownerId,
                Coins = 100
            },
            new InventoryItem
            {
                Id = Guid.NewGuid(),
                InventoryId = inventoryId,
                ItemType = ItemType.Item,
                ItemId = PestOptions.NaturalRepellentItemId,
                Quantity = 1
            },
            new InventoryItem
            {
                Id = Guid.NewGuid(),
                InventoryId = inventoryId,
                ItemType = ItemType.Seed,
                ItemId = "corn",
                Quantity = 1
            });

        DateTime protectedUntil;
        await using (var protectionContext = new AppDbContext(database))
        {
            var protection = await CreateService(protectionContext, now)
                .ApplyProtectionAsync(ownerId, farmId, plotId);
            Assert.True(protection.Succeeded);
            protectedUntil = protection.Response!.ProtectedUntil;
        }

        await using (var harvestContext = new AppDbContext(database))
        {
            var harvest = CreatePlotController(
                harvestContext,
                ownerId,
                now);
            Assert.IsType<OkObjectResult>(
                await harvest.Harvest(plotId));
        }

        await using (var plantContext = new AppDbContext(database))
        {
            var plant = CreatePlotController(
                plantContext,
                ownerId,
                now);
            Assert.IsType<OkObjectResult>(
                await plant.Plant(
                    plotId,
                    new PlantSeedRequest("corn")));
        }

        await using var assertion = new AppDbContext(database);
        var plot = await assertion.Plots
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == plotId);
        Assert.Equal("corn", plot.SeedId);
        Assert.Equal(protectedUntil, plot.ProtectedUntil);
        Assert.Equal(PestStatus.None, plot.PestStatus);
        Assert.Null(plot.PestType);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    [Trait("PestScenario", "19")]
    public async Task Scenario19_ProtectionDoesNotPreventSuccessfulTheft()
    {
        var database = DatabaseOptions();
        var now = UtcNowRounded();
        var ownerId = Guid.NewGuid();
        var visitorId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var ownerInventoryId = Guid.NewGuid();

        await ArrangeFarmAsync(
            database,
            ownerId,
            farmId,
            ActivePlot(plotId, farmId, now, now.AddMinutes(10)),
            CreateUser(visitorId, "visitor"),
            CreateAcceptedFriendship(ownerId, visitorId),
            new Inventory
            {
                Id = ownerInventoryId,
                UserId = ownerId,
                Coins = 100
            },
            new InventoryItem
            {
                Id = Guid.NewGuid(),
                InventoryId = ownerInventoryId,
                ItemType = ItemType.Item,
                ItemId = PestOptions.NaturalRepellentItemId,
                Quantity = 1
            },
            new Inventory
            {
                Id = Guid.NewGuid(),
                UserId = visitorId,
                Coins = 100
            });

        await using (var protectionContext = new AppDbContext(database))
        {
            var protection = await CreateService(
                    protectionContext,
                    now)
                .ApplyProtectionAsync(ownerId, farmId, plotId);
            Assert.True(protection.Succeeded);
        }

        await using (var theftContext = new AppDbContext(database))
        {
            var theft = CreateTheftController(
                theftContext,
                visitorId,
                now);
            Assert.IsType<OkObjectResult>(
                await theft.Steal(farmId, plotId));
        }

        await using var assertion = new AppDbContext(database);
        var plot = await assertion.Plots
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == plotId);
        Assert.Equal(2, plot.RemainingYield);
        Assert.True(plot.ProtectedUntil > now);
        Assert.Equal(
            1,
            await assertion.TheftLogs.CountAsync(log =>
                log.PlotId == plotId
                && log.ThiefUserId == visitorId));
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    [Trait("PestScenario", "20")]
    public async Task Scenario20_TheftAfterExpiredPestAccumulatesLossWithFloorOne()
    {
        var database = DatabaseOptions();
        var now = UtcNowRounded();
        var ownerId = Guid.NewGuid();
        var visitorId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();

        await ArrangeFarmAsync(
            database,
            ownerId,
            farmId,
            ActivePlot(plotId, farmId, now, now.AddMinutes(-1)),
            CreateUser(visitorId, "visitor"),
            CreateAcceptedFriendship(ownerId, visitorId),
            new Inventory
            {
                Id = Guid.NewGuid(),
                UserId = visitorId,
                Coins = 100
            });

        await using (var context = new AppDbContext(database))
        {
            var theft = CreateTheftController(context, visitorId, now);
            Assert.IsType<OkObjectResult>(
                await theft.Steal(farmId, plotId));
        }

        await using var assertion = new AppDbContext(database);
        var plot = await assertion.Plots
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == plotId);
        Assert.Equal(1, plot.RemainingYield);
        Assert.Equal(PestStatus.Consumed, plot.PestStatus);
        Assert.Equal(1, plot.PestConsumedAmount);
        Assert.Equal(
            1,
            await assertion.TheftLogs
                .Where(log => log.PlotId == plotId)
                .SumAsync(log => log.Quantity));
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    [Trait("PestScenario", "14")]
    public async Task TheftResponse_ReportsWhenActivePestWasCancelled()
    {
        var database = DatabaseOptions();
        var now = UtcNowRounded();
        var ownerId = Guid.NewGuid();
        var visitorId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();

        await ArrangeFarmAsync(
            database,
            ownerId,
            farmId,
            ActivePlot(
                plotId,
                farmId,
                now,
                now.AddMinutes(10)),
            CreateUser(visitorId, "visitor"),
            CreateAcceptedFriendship(ownerId, visitorId),
            new Inventory
            {
                Id = Guid.NewGuid(),
                UserId = visitorId,
                Coins = 100
            });

        await using var context = new AppDbContext(database);
        var result = Assert.IsType<OkObjectResult>(
            await CreateTheftController(context, visitorId, now)
                .Steal(farmId, plotId));
        var response = Assert.IsType<
            FarmAndFriends.Api.Dtos.Theft.TheftResponse>(result.Value);

        Assert.True(response.PestCancelled);
        Assert.InRange(response.Stolen, 1, 2);
        Assert.Equal(
            3 - response.Stolen,
            response.OwnerWillReceive);
        Assert.True(response.OwnerWillReceive >= 1);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    [Trait("PestScenario", "20")]
    public async Task Scenario20_ConcurrentTheftHarvestAndConsumptionConserveTotalYield()
    {
        var database = DatabaseOptions();
        var now = UtcNowRounded();
        var ownerId = Guid.NewGuid();
        var visitorId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var ownerInventoryId = Guid.NewGuid();
        var visitorInventoryId = Guid.NewGuid();

        await ArrangeFarmAsync(
            database,
            ownerId,
            farmId,
            ActivePlot(plotId, farmId, now, now.AddMinutes(-1)),
            CreateUser(visitorId, "visitor"),
            CreateAcceptedFriendship(ownerId, visitorId),
            new Inventory
            {
                Id = ownerInventoryId,
                UserId = ownerId,
                Coins = 100
            },
            new Inventory
            {
                Id = visitorInventoryId,
                UserId = visitorId,
                Coins = 100
            });

        await using var processContext = new AppDbContext(database);
        await using var harvestContext = new AppDbContext(database);
        await using var theftContext = new AppDbContext(database);
        var processor = CreateService(processContext, now);
        var harvest = CreatePlotController(
            harvestContext,
            ownerId,
            now);
        var theft = CreateTheftController(
            theftContext,
            visitorId,
            now);
        var gate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var processTask = AfterGate(
            gate.Task,
            () => processor.ProcessFarmAsync(farmId));
        var harvestTask = AfterGate(
            gate.Task,
            () => harvest.Harvest(plotId));
        var theftTask = AfterGate(
            gate.Task,
            () => theft.Steal(farmId, plotId));
        gate.SetResult();

        await Task.WhenAll(
                processTask,
                harvestTask,
                theftTask)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(await processTask);
        Assert.IsType<OkObjectResult>(await harvestTask);
        Assert.True(
            await theftTask is OkObjectResult or BadRequestObjectResult);

        await using var assertion = new AppDbContext(database);
        var plot = await assertion.Plots
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == plotId);
        var ownerQuantity = await CropQuantityAsync(
            assertion,
            ownerInventoryId);
        var visitorQuantity = await CropQuantityAsync(
            assertion,
            visitorInventoryId);
        var stolen = await assertion.TheftLogs
            .Where(log => log.PlotId == plotId)
            .SumAsync(log => (int?)log.Quantity) ?? 0;

        Assert.Null(plot.SeedId);
        Assert.Null(plot.RemainingYield);
        Assert.Equal(stolen, visitorQuantity);
        Assert.Equal(
            3,
            ownerQuantity + visitorQuantity + plot.PestConsumedAmount);
        Assert.InRange(plot.PestConsumedAmount, 0, 1);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task ConcurrentProtections_DebitExactlyOneItem()
    {
        var database = DatabaseOptions();
        var now = UtcNowRounded();
        var ownerId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var inventoryId = Guid.NewGuid();

        await ArrangeFarmAsync(
            database,
            ownerId,
            farmId,
            EmptyPlot(plotId, farmId),
            new Inventory
            {
                Id = inventoryId,
                UserId = ownerId,
                Coins = 100
            },
            new InventoryItem
            {
                Id = Guid.NewGuid(),
                InventoryId = inventoryId,
                ItemType = ItemType.Item,
                ItemId = PestOptions.NaturalRepellentItemId,
                Quantity = 2
            });

        await using var firstContext = new AppDbContext(database);
        await using var secondContext = new AppDbContext(database);
        var first = CreateService(firstContext, now);
        var second = CreateService(secondContext, now);
        var gate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var firstTask = AfterGate(
            gate.Task,
            () => first.ApplyProtectionAsync(
                ownerId,
                farmId,
                plotId));
        var secondTask = AfterGate(
            gate.Task,
            () => second.ApplyProtectionAsync(
                ownerId,
                farmId,
                plotId));
        gate.SetResult();
        var attempts = await Task.WhenAll(firstTask, secondTask)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Single(attempts, attempt => attempt.Succeeded);
        Assert.Single(
            attempts,
            attempt =>
                attempt.Failure == PestActionFailure.AlreadyProtected);

        await using var assertion = new AppDbContext(database);
        Assert.Equal(
            1,
            await assertion.InventoryItems
                .Where(item =>
                    item.InventoryId == inventoryId
                    && item.ItemType == ItemType.Item
                    && item.ItemId
                        == PestOptions.NaturalRepellentItemId)
                .Select(item => item.Quantity)
                .SingleAsync());
        Assert.Equal(
            1,
            await assertion.Plots.CountAsync(plot =>
                plot.Id == plotId
                && plot.ProtectedUntil == now.AddHours(4)));
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task ReapplyingProtection_ReturnsConflictWithoutSecondDebit()
    {
        var database = DatabaseOptions();
        var now = UtcNowRounded();
        var ownerId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var inventoryId = Guid.NewGuid();

        await ArrangeFarmAsync(
            database,
            ownerId,
            farmId,
            EmptyPlot(plotId, farmId),
            new Inventory
            {
                Id = inventoryId,
                UserId = ownerId,
                Coins = 100
            },
            new InventoryItem
            {
                Id = Guid.NewGuid(),
                InventoryId = inventoryId,
                ItemType = ItemType.Item,
                ItemId = PestOptions.NaturalRepellentItemId,
                Quantity = 2
            });

        await using (var firstContext = new AppDbContext(database))
        {
            var first = await CreatePestController(
                    firstContext,
                    ownerId,
                    now)
                .ApplyProtection(
                    farmId,
                    plotId,
                    CancellationToken.None);
            Assert.IsType<OkObjectResult>(first);
        }

        await using (var secondContext = new AppDbContext(database))
        {
            var second = await CreatePestController(
                    secondContext,
                    ownerId,
                    now)
                .ApplyProtection(
                    farmId,
                    plotId,
                    CancellationToken.None);
            AssertProblem(
                second,
                409,
                PestErrorCodes.AlreadyProtected);
        }

        await using var assertion = new AppDbContext(database);
        Assert.Equal(
            1,
            await assertion.InventoryItems
                .Where(item =>
                    item.InventoryId == inventoryId
                    && item.ItemId
                        == PestOptions.NaturalRepellentItemId)
                .Select(item => item.Quantity)
                .SingleAsync());
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task PestEndpoints_ReturnStableErrorsWithoutMutatingState()
    {
        var database = DatabaseOptions();
        var now = UtcNowRounded();
        var ownerId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var activePlotId = Guid.NewGuid();
        var inactivePlotId = Guid.NewGuid();
        var activeOccurrenceId = Guid.NewGuid();
        var inactiveOccurrenceId = Guid.NewGuid();
        var inventoryId = Guid.NewGuid();
        var resolvedPlot = EmptyPlot(inactivePlotId, farmId);
        resolvedPlot.X = 1;
        resolvedPlot.PestOccurrenceId = inactiveOccurrenceId;
        resolvedPlot.PestType = PestType.Caterpillar;
        resolvedPlot.PestStatus = PestStatus.Removed;

        await ArrangeFarmAsync(
            database,
            ownerId,
            farmId,
            ActivePlot(
                activePlotId,
                farmId,
                now,
                now.AddMinutes(10),
                activeOccurrenceId),
            CreateUser(strangerId, "stranger"),
            resolvedPlot,
            new Inventory
            {
                Id = inventoryId,
                UserId = ownerId,
                Coins = 100
            });

        await using (var strangerContext = new AppDbContext(database))
        {
            var forbidden = await CreatePestController(
                    strangerContext,
                    strangerId,
                    now)
                .Remove(
                    farmId,
                    activePlotId,
                    new RemovePestRequest(activeOccurrenceId),
                    Guid.NewGuid().ToString(),
                    CancellationToken.None);
            AssertProblem(forbidden, 403, PestErrorCodes.Forbidden);
        }

        await using (var missingContext = new AppDbContext(database))
        {
            var missing = await CreatePestController(
                    missingContext,
                    ownerId,
                    now)
                .Remove(
                    farmId,
                    Guid.NewGuid(),
                    new RemovePestRequest(Guid.NewGuid()),
                    Guid.NewGuid().ToString(),
                    CancellationToken.None);
            AssertProblem(missing, 404, PestErrorCodes.PlotNotFound);
        }

        await using (var inactiveContext = new AppDbContext(database))
        {
            var inactive = await CreatePestController(
                    inactiveContext,
                    ownerId,
                    now)
                .Remove(
                    farmId,
                    inactivePlotId,
                    new RemovePestRequest(inactiveOccurrenceId),
                    Guid.NewGuid().ToString(),
                    CancellationToken.None);
            AssertProblem(inactive, 409, PestErrorCodes.NotActive);
        }

        await using (var itemContext = new AppDbContext(database))
        {
            var unavailable = await CreatePestController(
                    itemContext,
                    ownerId,
                    now)
                .ApplyProtection(
                    farmId,
                    inactivePlotId,
                    CancellationToken.None);
            AssertProblem(
                unavailable,
                409,
                PestErrorCodes.ItemUnavailable);
        }

        await using var assertion = new AppDbContext(database);
        var activePlot = await assertion.Plots
            .AsNoTracking()
            .SingleAsync(plot => plot.Id == activePlotId);
        var inactivePlot = await assertion.Plots
            .AsNoTracking()
            .SingleAsync(plot => plot.Id == inactivePlotId);
        Assert.Equal(PestStatus.Active, activePlot.PestStatus);
        Assert.Null(inactivePlot.ProtectedUntil);
        Assert.False(await assertion.InventoryItems.AnyAsync(item =>
            item.InventoryId == inventoryId));
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task DisabledFeature_AllowsActivePestRemovalWithoutReward()
    {
        var database = DatabaseOptions();
        var now = UtcNowRounded();
        var ownerId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var legacyPlot = ActivePlot(
            plotId,
            farmId,
            now,
            now.AddMinutes(-1));
        legacyPlot.PestOccurrenceId = null;

        await ArrangeFarmAsync(
            database,
            ownerId,
            farmId,
            legacyPlot,
            new Inventory
            {
                Id = Guid.NewGuid(),
                UserId = ownerId,
                Coins = 100
            });

        await using (var processingContext = new AppDbContext(database))
        {
            Assert.True(await CreateService(
                    processingContext,
                    now,
                    enabled: false)
                .ProcessFarmAsync(farmId));
        }

        Guid occurrenceId;
        await using (var occurrenceContext = new AppDbContext(database))
        {
            occurrenceId = (await occurrenceContext.Plots
                .AsNoTracking()
                .SingleAsync(candidate => candidate.Id == plotId))
                .PestOccurrenceId!.Value;
        }

        await using var removalContext = new AppDbContext(database);
        var controller = new PestController(
            CreateService(removalContext, now, enabled: false))
        {
            ControllerContext = AuthenticatedController(ownerId)
        };
        var response = Assert.IsType<PestActionResponse>(
            Assert.IsType<OkObjectResult>(
                await controller.Remove(
                    farmId,
                    plotId,
                    new RemovePestRequest(occurrenceId),
                    Guid.NewGuid().ToString(),
                    CancellationToken.None)).Value);
        Assert.Equal(0, response.CoinsGained);
        Assert.Equal(0, response.XpGained);
        Assert.Equal(100, response.Coins);
        Assert.False(response.RewardGranted);

        await using var assertion = new AppDbContext(database);
        var plot = await assertion.Plots
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == plotId);
        Assert.Equal(PestStatus.Removed, plot.PestStatus);
        Assert.Equal(3, plot.RemainingYield);
        Assert.Equal(
            1,
            await assertion.PestRemovalCompletions.CountAsync(completion =>
                completion.PlotId == plotId
                && completion.CoinsGained == 0
                && completion.XpGained == 0));
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task Harvest_WithMissingRemainingYield_GrantsNothing()
    {
        var database = DatabaseOptions();
        var now = UtcNowRounded();
        var ownerId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var inventoryId = Guid.NewGuid();

        await ArrangeFarmAsync(
            database,
            ownerId,
            farmId,
            ReadyPlotWithoutYield(plotId, farmId, now),
            new Inventory
            {
                Id = inventoryId,
                UserId = ownerId,
                Coins = 100
            });

        await using (var context = new AppDbContext(database))
        {
            var action = await CreatePlotController(
                    context,
                    ownerId,
                    now)
                .Harvest(plotId);
            AssertYieldUnavailable(action);
        }

        await using var assertion = new AppDbContext(database);
        var plot = await assertion.Plots
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == plotId);
        var owner = await assertion.Users
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == ownerId);

        Assert.Equal("corn", plot.SeedId);
        Assert.Null(plot.RemainingYield);
        Assert.Equal(0, owner.CurrentXp);
        Assert.False(await assertion.InventoryItems.AnyAsync(item =>
            item.InventoryId == inventoryId
            && item.ItemType == ItemType.Crop));
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task Theft_WithMissingRemainingYield_GrantsNothing()
    {
        var database = DatabaseOptions();
        var now = UtcNowRounded();
        var ownerId = Guid.NewGuid();
        var visitorId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var visitorInventoryId = Guid.NewGuid();

        await ArrangeFarmAsync(
            database,
            ownerId,
            farmId,
            ReadyPlotWithoutYield(plotId, farmId, now),
            CreateUser(visitorId, "visitor"),
            CreateAcceptedFriendship(ownerId, visitorId),
            new Inventory
            {
                Id = visitorInventoryId,
                UserId = visitorId,
                Coins = 100
            });

        await using (var context = new AppDbContext(database))
        {
            var action = await CreateTheftController(
                    context,
                    visitorId,
                    now)
                .Steal(farmId, plotId);
            AssertYieldUnavailable(action);
        }

        await using var assertion = new AppDbContext(database);
        var plot = await assertion.Plots
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == plotId);
        var visitor = await assertion.Users
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == visitorId);

        Assert.Null(plot.RemainingYield);
        Assert.Equal(0, visitor.CurrentXp);
        Assert.False(await assertion.InventoryItems.AnyAsync(item =>
            item.InventoryId == visitorInventoryId
            && item.ItemType == ItemType.Crop));
        Assert.False(await assertion.TheftLogs.AnyAsync(log =>
            log.PlotId == plotId));
        Assert.False(await assertion.Notifications.AnyAsync(notification =>
            notification.TheftLogId != null
            && notification.RecipientUserId == ownerId));
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task Theft_LockedPlotWithCropState_GrantsNothing()
    {
        var database = DatabaseOptions();
        var now = UtcNowRounded();
        var ownerId = Guid.NewGuid();
        var visitorId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var visitorInventoryId = Guid.NewGuid();
        var lockedPlot = ActivePlot(
            plotId,
            farmId,
            now,
            now.AddMinutes(10));
        lockedPlot.Unlocked = false;

        await ArrangeFarmAsync(
            database,
            ownerId,
            farmId,
            lockedPlot,
            CreateUser(visitorId, "visitor"),
            CreateAcceptedFriendship(ownerId, visitorId),
            new Inventory
            {
                Id = visitorInventoryId,
                UserId = visitorId,
                Coins = 100
            });

        await using (var context = new AppDbContext(database))
        {
            var action = await CreateTheftController(
                    context,
                    visitorId,
                    now)
                .Steal(farmId, plotId);
            Assert.Equal(
                "Plot não encontrado.",
                Assert.IsType<NotFoundObjectResult>(action).Value);
        }

        await using var assertion = new AppDbContext(database);
        var plot = await assertion.Plots
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == plotId);
        var visitor = await assertion.Users
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == visitorId);

        Assert.False(plot.Unlocked);
        Assert.Equal(3, plot.RemainingYield);
        Assert.Equal(PestStatus.Active, plot.PestStatus);
        Assert.Equal(0, visitor.CurrentXp);
        Assert.False(await assertion.InventoryItems.AnyAsync(item =>
            item.InventoryId == visitorInventoryId));
        Assert.False(await assertion.TheftLogs.AnyAsync(log =>
            log.PlotId == plotId));
        Assert.False(await assertion.Notifications.AnyAsync(notification =>
            notification.RecipientUserId == ownerId
            && notification.ActorUserId == visitorId));
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task PestRemoval_LockedPlotWithActivePest_GrantsNothing()
    {
        var database = DatabaseOptions();
        var now = UtcNowRounded();
        var ownerId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();
        var lockedPlot = ActivePlot(
            plotId,
            farmId,
            now,
            now.AddMinutes(10),
            occurrenceId);
        lockedPlot.Unlocked = false;

        await ArrangeFarmAsync(
            database,
            ownerId,
            farmId,
            lockedPlot,
            new Inventory
            {
                Id = Guid.NewGuid(),
                UserId = ownerId,
                Coins = 100
            });

        await using (var context = new AppDbContext(database))
        {
            var action = await CreatePestController(context, ownerId, now)
                .Remove(
                    farmId,
                    plotId,
                    new RemovePestRequest(occurrenceId),
                    Guid.NewGuid().ToString(),
                    CancellationToken.None);
            AssertProblem(
                action,
                StatusCodes.Status404NotFound,
                PestErrorCodes.PlotNotFound);
        }

        await using var assertion = new AppDbContext(database);
        var plot = await assertion.Plots
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == plotId);
        var owner = await assertion.Users
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == ownerId);

        Assert.False(plot.Unlocked);
        Assert.Equal(PestStatus.Active, plot.PestStatus);
        Assert.Equal(occurrenceId, plot.PestOccurrenceId);
        Assert.Equal(3, plot.RemainingYield);
        Assert.Equal(0, owner.CurrentXp);
        Assert.Equal(
            100,
            await assertion.Inventories
                .Where(inventory => inventory.UserId == ownerId)
                .Select(inventory => inventory.Coins)
                .SingleAsync());
        Assert.False(await assertion.PestRemovalCompletions.AnyAsync(
            completion => completion.PlotId == plotId));
        Assert.False(await assertion.Notifications.AnyAsync(notification =>
            notification.PestPlotId == plotId));
    }

    private static async Task<T> AfterGate<T>(
        Task gate,
        Func<Task<T>> action)
    {
        await gate;
        return await action();
    }

    private static PestController CreatePestController(
        AppDbContext context,
        Guid userId,
        DateTime now)
    {
        return new PestController(CreateService(context, now))
        {
            ControllerContext = AuthenticatedController(userId)
        };
    }

    private static PlotController CreatePlotController(
        AppDbContext context,
        Guid userId,
        DateTime now)
    {
        return new PlotController(
            context,
            new ExperienceService(),
            CreateService(context, now))
        {
            ControllerContext = AuthenticatedController(userId)
        };
    }

    private static ShopController CreateShopController(
        AppDbContext context,
        Guid userId)
    {
        return new ShopController(
            context,
            Options.Create(new PestOptions { Enabled = true }))
        {
            ControllerContext = AuthenticatedController(userId)
        };
    }

    private static TheftController CreateTheftController(
        AppDbContext context,
        Guid userId,
        DateTime now)
    {
        return new TheftController(
            context,
            new TheftService(),
            new ExperienceService(),
            new FriendshipService(context),
            CreateService(context, now))
        {
            ControllerContext = AuthenticatedController(userId)
        };
    }

    private static PestService CreateService(
        AppDbContext context,
        DateTime now,
        bool enabled = true)
    {
        return new PestService(
            context,
            new ExperienceService(),
            Options.Create(new PestOptions { Enabled = enabled }),
            new FixedTimeProvider(now));
    }

    private static async Task<int> CropQuantityAsync(
        AppDbContext context,
        Guid inventoryId)
    {
        return await context.InventoryItems
            .Where(item =>
                item.InventoryId == inventoryId
                && item.ItemType == ItemType.Crop
                && item.ItemId == "corn_crop")
            .SumAsync(item => (int?)item.Quantity) ?? 0;
    }

    private static void AssertProblem(
        IActionResult action,
        int expectedStatus,
        string expectedCode)
    {
        var result = Assert.IsType<ObjectResult>(action);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(expectedStatus, result.StatusCode);
        Assert.Equal(expectedCode, problem.Extensions["code"]);
    }

    private static void AssertYieldUnavailable(IActionResult action)
    {
        var result = Assert.IsType<ObjectResult>(action);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(500, result.StatusCode);
        Assert.Equal("Rendimento do lote indisponível", problem.Title);
    }

    private static DbContextOptions<AppDbContext> DatabaseOptions()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CROP_CARE_TEST_CONNECTION")!;
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
    }

    private static async Task ArrangeFarmAsync(
        DbContextOptions<AppDbContext> database,
        Guid ownerId,
        Guid farmId,
        Plot plot,
        params object[] additionalEntities)
    {
        await using var context = new AppDbContext(database);
        context.Users.Add(CreateUser(ownerId, "owner"));
        context.Farms.Add(new Farm
        {
            Id = farmId,
            Name = "Pest scenario farm",
            UserId = ownerId
        });
        context.Plots.Add(plot);
        foreach (var entity in additionalEntities)
            context.Add(entity);
        await context.SaveChangesAsync();
    }

    private static Plot EmptyPlot(Guid plotId, Guid farmId) => new()
    {
        Id = plotId,
        FarmId = farmId,
        X = 0,
        Y = 0,
        Unlocked = true
    };

    private static Plot ReadyPlotWithoutYield(
        Guid plotId,
        Guid farmId,
        DateTime now) => new()
        {
            Id = plotId,
            FarmId = farmId,
            X = 0,
            Y = 0,
            Unlocked = true,
            SeedId = "corn",
            PlantedAt = now.AddHours(-1),
            ReadyAt = now.AddMinutes(-30),
            RemainingYield = null
        };

    private static Plot ActivePlot(
        Guid plotId,
        Guid farmId,
        DateTime now,
        DateTime consumesAt,
        Guid? occurrenceId = null)
    {
        return new Plot
        {
            Id = plotId,
            FarmId = farmId,
            X = 0,
            Y = 0,
            Unlocked = true,
            SeedId = "corn",
            PlantedAt = now.AddHours(-1),
            ReadyAt = now.AddMinutes(-30),
            RemainingYield = 3,
            PestOccurrenceId = occurrenceId ?? Guid.NewGuid(),
            PestType = PestType.Caterpillar,
            PestStatus = PestStatus.Active,
            PestScheduledAt = now.AddMinutes(-20),
            PestAppearsAt = now.AddMinutes(-15),
            PestAppearedAt = now.AddMinutes(-15),
            PestConsumesAt = consumesAt
        };
    }

    private static ControllerContext AuthenticatedController(Guid userId)
    {
        return new ControllerContext
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
    }

    private static User CreateUser(Guid id, string prefix)
    {
        var username = $"{prefix}_{id:N}";
        return new User
        {
            Id = id,
            Username = username,
            NormalizedUsername = username.ToUpperInvariant(),
            PasswordHash = "integration-test"
        };
    }

    private static Friendship CreateAcceptedFriendship(
        Guid firstUserId,
        Guid secondUserId)
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
            CreatedAt = DateTime.UtcNow.AddMinutes(-2),
            RespondedAt = DateTime.UtcNow.AddMinutes(-1)
        };
    }

    private static DateTime UtcNowRounded()
    {
        var now = DateTime.UtcNow;
        return now.AddTicks(
            -(now.Ticks % TimeSpan.TicksPerMillisecond));
    }

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(DateTime.SpecifyKind(now, DateTimeKind.Utc));
    }
}
