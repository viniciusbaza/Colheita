using System.Security.Claims;
using FarmAndFriends.Api.Configuration;
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

public sealed class PestConcurrencyTests
{
    [PostgresFact]
    [Trait("Category", "Postgres")]
    [Trait("PestScenario", "12")]
    public async Task ConcurrentLazyProcessing_ConsumesExactlyOnce()
    {
        var options = DatabaseOptions();
        var now = DateTime.UtcNow;
        var ownerId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();

        await ArrangeFarmAsync(
            options,
            ownerId,
            farmId,
            ActivePlot(plotId, farmId, now, consumesAt: now.AddMinutes(-1)));

        await using var firstContext = new AppDbContext(options);
        await using var secondContext = new AppDbContext(options);
        var firstService = CreateService(firstContext, now);
        var secondService = CreateService(secondContext, now);
        var gate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var first = ProcessAfterGate(gate.Task, firstService, farmId);
        var second = ProcessAfterGate(gate.Task, secondService, farmId);
        gate.SetResult();

        await Task.WhenAll(first, second)
            .WaitAsync(TimeSpan.FromSeconds(10));

        await using var assertContext = new AppDbContext(options);
        var plot = await assertContext.Plots
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == plotId);

        Assert.Equal(2, plot.RemainingYield);
        Assert.Equal(PestStatus.Consumed, plot.PestStatus);
        Assert.Equal(1, plot.PestConsumedAmount);
        Assert.Equal(
            1,
            await assertContext.Notifications.CountAsync(notification =>
                notification.PestPlotId == plotId
                && notification.Type == NotificationType.PestConsumed));
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    [Trait("PestScenario", "20")]
    public async Task HarvestAndExpiredPestProcessing_PreserveConsistentYield()
    {
        var options = DatabaseOptions();
        var now = DateTime.UtcNow;
        var ownerId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var inventoryId = Guid.NewGuid();

        await ArrangeFarmAsync(
            options,
            ownerId,
            farmId,
            ActivePlot(plotId, farmId, now, consumesAt: now.AddMinutes(-1)),
            new Inventory
            {
                Id = inventoryId,
                UserId = ownerId,
                Coins = 100
            });

        await using var processContext = new AppDbContext(options);
        await using var harvestContext = new AppDbContext(options);
        var processService = CreateService(processContext, now);
        var harvestService = CreateService(harvestContext, now);
        var controller = new PlotController(
            harvestContext,
            new ExperienceService(),
            harvestService)
        {
            ControllerContext = AuthenticatedController(ownerId)
        };
        var gate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var processing = ProcessAfterGate(
            gate.Task,
            processService,
            farmId);
        var harvesting = HarvestAfterGate(gate.Task, controller, plotId);
        gate.SetResult();

        await Task.WhenAll(processing, harvesting)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.IsType<OkObjectResult>(await harvesting);
        await using var assertContext = new AppDbContext(options);
        var plot = await assertContext.Plots
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == plotId);
        var cropQuantity = await assertContext.InventoryItems
            .Where(item =>
                item.InventoryId == inventoryId
                && item.ItemType == ItemType.Crop
                && item.ItemId == "corn_crop")
            .Select(item => item.Quantity)
            .SingleAsync();

        Assert.Null(plot.SeedId);
        Assert.Null(plot.RemainingYield);
        Assert.InRange(cropQuantity, 2, 3);
        Assert.InRange(
            await assertContext.Notifications.CountAsync(notification =>
                notification.PestPlotId == plotId
                && notification.Type == NotificationType.PestConsumed),
            0,
            1);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    [Trait("PestScenario", "17")]
    public async Task VisitorProtection_ConsumesItemAndCancelsActivePestAtomically()
    {
        var options = DatabaseOptions();
        var now = DateTime.UtcNow;
        var ownerId = Guid.NewGuid();
        var visitorId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var inventoryId = Guid.NewGuid();
        var visitorInventory = new Inventory
        {
            Id = inventoryId,
            UserId = visitorId,
            Coins = 100
        };

        await ArrangeFarmAsync(
            options,
            ownerId,
            farmId,
            ActivePlot(plotId, farmId, now, consumesAt: now.AddMinutes(10)),
            visitorInventory,
            CreateUser(visitorId, "visitor"),
            CreateAcceptedFriendship(ownerId, visitorId),
            new InventoryItem
            {
                Id = Guid.NewGuid(),
                InventoryId = inventoryId,
                ItemType = ItemType.Item,
                ItemId = PestOptions.NaturalRepellentItemId,
                Quantity = 1
            });

        await using var context = new AppDbContext(options);
        var service = CreateService(context, now);
        var result = await service.ApplyProtectionAsync(
            visitorId,
            farmId,
            plotId);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.Response!.RemainingItemQuantity);
        Assert.Equal(
            PestStatus.CancelledByProtection.ToString(),
            result.Response.Pest!.Status,
            ignoreCase: true);

        await using var assertContext = new AppDbContext(options);
        var plot = await assertContext.Plots
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == plotId);

        Assert.Equal(PestStatus.CancelledByProtection, plot.PestStatus);
        Assert.NotNull(plot.ProtectedUntil);
        Assert.Equal(
            TruncateToPostgresMicroseconds(now.AddHours(4)),
            plot.ProtectedUntil.Value);
        Assert.False(await assertContext.InventoryItems.AnyAsync(item =>
            item.InventoryId == inventoryId
            && item.ItemId == PestOptions.NaturalRepellentItemId));
        Assert.Equal(
            1,
            await assertContext.Notifications.CountAsync(notification =>
                notification.PestPlotId == plotId
                && notification.Type
                    == NotificationType.PestProtectionApplied));
    }

    private static async Task ProcessAfterGate(
        Task gate,
        PestService service,
        Guid farmId)
    {
        await gate;
        await service.ProcessFarmAsync(farmId);
    }

    private static async Task<IActionResult> HarvestAfterGate(
        Task gate,
        PlotController controller,
        Guid plotId)
    {
        await gate;
        return await controller.Harvest(plotId);
    }

    private static DbContextOptions<AppDbContext> DatabaseOptions()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CROP_CARE_TEST_CONNECTION")!;
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
    }

    private static PestService CreateService(
        AppDbContext context,
        DateTime now)
    {
        return new PestService(
            context,
            new ExperienceService(),
            Options.Create(new PestOptions { Enabled = true }),
            new FixedTimeProvider(now));
    }

    private static async Task ArrangeFarmAsync(
        DbContextOptions<AppDbContext> options,
        Guid ownerId,
        Guid farmId,
        Plot plot,
        params object[] additionalEntities)
    {
        await using var context = new AppDbContext(options);
        context.Users.Add(CreateUser(ownerId, "owner"));
        context.Farms.Add(new Farm
        {
            Id = farmId,
            Name = "Pest integration farm",
            UserId = ownerId
        });
        context.Plots.Add(plot);

        foreach (var entity in additionalEntities)
            context.Add(entity);

        await context.SaveChangesAsync();
    }

    private static Plot ActivePlot(
        Guid plotId,
        Guid farmId,
        DateTime now,
        DateTime consumesAt)
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

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(DateTime.SpecifyKind(now, DateTimeKind.Utc));
    }

    private static DateTime TruncateToPostgresMicroseconds(
        DateTime value)
    {
        const long ticksPerMicrosecond = 10;
        return value.AddTicks(-(value.Ticks % ticksPerMicrosecond));
    }
}
