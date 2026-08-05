using System.Security.Claims;
using FarmAndFriends.Api.Configuration;
using FarmAndFriends.Api.Contracts.Pests;
using FarmAndFriends.Api.Controllers;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Domain.Rules;
using FarmAndFriends.Api.Domain.Services;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Xunit;

namespace FarmAndFriends.Api.Tests.Integration;

public sealed class PestRemovalRewardIntegrationTests
{
    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task FifteenthRemovalPaysAndSixteenthStillRemovesWithoutReward()
    {
        var database = DatabaseOptions();
        var now = RoundedUtcNow();
        var actorId = Guid.NewGuid();
        var firstFarmId = Guid.NewGuid();
        var secondFarmId = Guid.NewGuid();
        var firstPlotId = Guid.NewGuid();
        var secondPlotId = Guid.NewGuid();
        var firstOccurrenceId = Guid.NewGuid();
        var secondOccurrenceId = Guid.NewGuid();

        await ArrangeActorAsync(
            database,
            actorId,
            (firstFarmId, ActivePlot(
                firstPlotId,
                firstFarmId,
                now,
                firstOccurrenceId)),
            (secondFarmId, ActivePlot(
                secondPlotId,
                secondFarmId,
                now,
                secondOccurrenceId)));
        await SeedRewardedCompletionsAsync(
            database,
            actorId,
            now.AddHours(-1),
            count: 14);

        await using var context = new AppDbContext(database);
        var service = CreateService(context, now);

        var fifteenth = await service.RemoveAsync(
            actorId,
            firstFarmId,
            firstPlotId,
            firstOccurrenceId,
            Guid.NewGuid());
        var sixteenth = await service.RemoveAsync(
            actorId,
            secondFarmId,
            secondPlotId,
            secondOccurrenceId,
            Guid.NewGuid());

        Assert.True(fifteenth.Succeeded);
        Assert.True(sixteenth.Succeeded);
        Assert.True(fifteenth.Response!.RewardGranted);
        Assert.Equal(2, fifteenth.Response.CoinsGained);
        Assert.Equal(5, fifteenth.Response.XpGained);
        Assert.False(sixteenth.Response!.RewardGranted);
        Assert.Equal(0, sixteenth.Response.CoinsGained);
        Assert.Equal(0, sixteenth.Response.XpGained);
        Assert.Equal(102, sixteenth.Response.Coins);

        await using var assertion = new AppDbContext(database);
        Assert.Equal(
            102,
            await assertion.Inventories
                .Where(inventory => inventory.UserId == actorId)
                .Select(inventory => inventory.Coins)
                .SingleAsync());
        Assert.Equal(
            5,
            await assertion.Users
                .Where(user => user.Id == actorId)
                .Select(user => user.CurrentXp)
                .SingleAsync());
        Assert.Equal(
            16,
            await assertion.PestRemovalCompletions.CountAsync(
                completion => completion.ActorUserId == actorId));
        Assert.Equal(
            2,
            await assertion.Plots.CountAsync(plot =>
                (plot.Id == firstPlotId || plot.Id == secondPlotId)
                && plot.PestStatus == PestStatus.Removed));
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task ExpiredRollingWindowCompletionsDoNotConsumeQuota()
    {
        var database = DatabaseOptions();
        var now = RoundedUtcNow();
        var actorId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();

        await ArrangeActorAsync(
            database,
            actorId,
            (farmId, ActivePlot(plotId, farmId, now, occurrenceId)));
        await SeedRewardedCompletionsAsync(
            database,
            actorId,
            now.AddHours(-24).AddSeconds(-1),
            count: 15);

        await using var context = new AppDbContext(database);
        var attempt = await CreateService(context, now).RemoveAsync(
            actorId,
            farmId,
            plotId,
            occurrenceId,
            Guid.NewGuid());

        Assert.True(attempt.Succeeded);
        Assert.True(attempt.Response!.RewardGranted);
        Assert.Equal(2, attempt.Response.CoinsGained);
        Assert.Equal(5, attempt.Response.XpGained);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task StaleOccurrenceCannotRemoveOrRewardCurrentPest()
    {
        var database = DatabaseOptions();
        var now = RoundedUtcNow();
        var actorId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var staleOccurrenceId = Guid.NewGuid();
        var currentOccurrenceId = Guid.NewGuid();
        var currentPlot = ActivePlot(
            plotId,
            farmId,
            now,
            currentOccurrenceId);
        currentPlot.PestConsumesAt = now.AddSeconds(-1);

        await ArrangeActorAsync(
            database,
            actorId,
            (farmId, currentPlot));

        await using var context = new AppDbContext(database);
        var controller = new PestController(CreateService(context, now))
        {
            ControllerContext = AuthenticatedController(actorId)
        };
        var action = await controller.Remove(
            farmId,
            plotId,
            new RemovePestRequest(staleOccurrenceId),
            Guid.NewGuid().ToString(),
            CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(action);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(
            PestErrorCodes.OccurrenceMismatch,
            problem.Extensions["code"]);

        await using var assertion = new AppDbContext(database);
        var plot = await assertion.Plots
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == plotId);
        Assert.Equal(currentOccurrenceId, plot.PestOccurrenceId);
        Assert.Equal(PestStatus.Active, plot.PestStatus);
        Assert.Equal(3, plot.RemainingYield);
        Assert.Equal(
            100,
            await assertion.Inventories
                .Where(inventory => inventory.UserId == actorId)
                .Select(inventory => inventory.Coins)
                .SingleAsync());
        Assert.Equal(
            0,
            await assertion.Users
                .Where(user => user.Id == actorId)
                .Select(user => user.CurrentXp)
                .SingleAsync());
        Assert.False(await assertion.PestRemovalCompletions.AnyAsync(
            completion => completion.PlotId == plotId));
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task RetryReturnsPersistedCompletionAndKeyReuseConflicts()
    {
        var database = DatabaseOptions();
        var now = RoundedUtcNow();
        var actorId = Guid.NewGuid();
        var firstFarmId = Guid.NewGuid();
        var secondFarmId = Guid.NewGuid();
        var firstPlotId = Guid.NewGuid();
        var secondPlotId = Guid.NewGuid();
        var key = Guid.NewGuid();
        var firstOccurrenceId = Guid.NewGuid();
        var secondOccurrenceId = Guid.NewGuid();

        await ArrangeActorAsync(
            database,
            actorId,
            (firstFarmId, ActivePlot(
                firstPlotId,
                firstFarmId,
                now,
                firstOccurrenceId)),
            (secondFarmId, ActivePlot(
                secondPlotId,
                secondFarmId,
                now,
                secondOccurrenceId)));

        PestActionAttempt first;
        await using (var firstContext = new AppDbContext(database))
        {
            first = await CreateService(firstContext, now).RemoveAsync(
                actorId,
                firstFarmId,
                firstPlotId,
                firstOccurrenceId,
                key);
        }

        var replacementOccurrenceId = Guid.NewGuid();
        await using (var replacementContext = new AppDbContext(database))
        {
            var plot = await replacementContext.Plots
                .SingleAsync(candidate => candidate.Id == firstPlotId);
            PestRules.ResetCycle(plot);
            plot.SeedId = "corn";
            plot.PlantedAt = now.AddMinutes(-10);
            plot.ReadyAt = now.AddMinutes(-5);
            plot.RemainingYield = 3;
            plot.PestOccurrenceId = replacementOccurrenceId;
            plot.PestType = PestType.Caterpillar;
            plot.PestStatus = PestStatus.Active;
            plot.PestScheduledAt = now.AddMinutes(1);
            plot.PestAppearsAt = now.AddMinutes(2);
            plot.PestAppearedAt = now.AddMinutes(2);
            plot.PestConsumesAt = now.AddMinutes(20);
            plot.PestResolvedAt = null;
            await replacementContext.SaveChangesAsync();
        }

        PestActionAttempt replay;
        await using (var retryContext = new AppDbContext(database))
        {
            replay = await CreateService(retryContext, now).RemoveAsync(
                actorId,
                firstFarmId,
                firstPlotId,
                firstOccurrenceId,
                key);
        }

        IActionResult conflict;
        await using (var conflictContext = new AppDbContext(database))
        {
            var controller = new PestController(
                CreateService(conflictContext, now))
            {
                ControllerContext = AuthenticatedController(actorId)
            };
            conflict = await controller.Remove(
                secondFarmId,
                secondPlotId,
                new RemovePestRequest(secondOccurrenceId),
                key.ToString(),
                CancellationToken.None);
        }

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.False(first.Response!.Replayed);
        Assert.True(replay.Response!.Replayed);
        Assert.Equal(
            first.Response.CompletionId,
            replay.Response.CompletionId);
        Assert.Equal(first.Response.CoinsGained, replay.Response.CoinsGained);
        Assert.Equal(first.Response.XpGained, replay.Response.XpGained);
        Assert.Equal("removed", replay.Response.Status);
        Assert.Equal(firstOccurrenceId, replay.Response.PestOccurrenceId);
        Assert.Equal(3, replay.Response.RemainingYield);
        Assert.Null(replay.Response.Pest);
        var conflictResult = Assert.IsType<ObjectResult>(conflict);
        var conflictProblem = Assert.IsType<ProblemDetails>(
            conflictResult.Value);
        Assert.Equal(409, conflictResult.StatusCode);
        Assert.Equal(
            PestErrorCodes.IdempotencyKeyReused,
            conflictProblem.Extensions["code"]);

        await using var assertion = new AppDbContext(database);
        Assert.Equal(
            1,
            await assertion.PestRemovalCompletions.CountAsync(
                completion => completion.ActorUserId == actorId));
        Assert.Equal(
            102,
            await assertion.Inventories
                .Where(inventory => inventory.UserId == actorId)
                .Select(inventory => inventory.Coins)
                .SingleAsync());
        Assert.Equal(
            replacementOccurrenceId,
            await assertion.Plots
                .Where(plot => plot.Id == firstPlotId)
                .Select(plot => plot.PestOccurrenceId)
                .SingleAsync());
        Assert.Equal(
            PestStatus.Active,
            await assertion.Plots
                .Where(plot => plot.Id == firstPlotId)
                .Select(plot => plot.PestStatus)
                .SingleAsync());
        Assert.Equal(
            secondOccurrenceId,
            await assertion.Plots
                .Where(plot => plot.Id == secondPlotId)
                .Select(plot => plot.PestOccurrenceId)
                .SingleAsync());
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task ConcurrentRemovalCreatesOneCompletionAndOneReward()
    {
        var database = DatabaseOptions();
        var now = RoundedUtcNow();
        var actorId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();

        await ArrangeActorAsync(
            database,
            actorId,
            (farmId, ActivePlot(plotId, farmId, now, occurrenceId)));

        await using var firstContext = new AppDbContext(database);
        await using var secondContext = new AppDbContext(database);
        var firstService = CreateService(firstContext, now);
        var secondService = CreateService(secondContext, now);
        var gate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var first = RemoveAfterGate(
            gate.Task,
            firstService,
            actorId,
            farmId,
            plotId,
            occurrenceId,
            Guid.NewGuid());
        var second = RemoveAfterGate(
            gate.Task,
            secondService,
            actorId,
            farmId,
            plotId,
            occurrenceId,
            Guid.NewGuid());
        gate.SetResult();
        var attempts = await Task.WhenAll(first, second)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Single(attempts, attempt => attempt.Succeeded);
        Assert.Single(
            attempts,
            attempt => attempt.Failure == PestActionFailure.PestNotActive);

        await using var assertion = new AppDbContext(database);
        Assert.Equal(
            1,
            await assertion.PestRemovalCompletions.CountAsync(
                completion => completion.PlotId == plotId));
        Assert.Equal(
            102,
            await assertion.Inventories
                .Where(inventory => inventory.UserId == actorId)
                .Select(inventory => inventory.Coins)
                .SingleAsync());
        Assert.Equal(
            5,
            await assertion.Users
                .Where(user => user.Id == actorId)
                .Select(user => user.CurrentXp)
                .SingleAsync());
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task ConcurrentFarmsRespectActorGlobalRewardCap()
    {
        var database = DatabaseOptions();
        var now = RoundedUtcNow();
        var actorId = Guid.NewGuid();
        var firstFarmId = Guid.NewGuid();
        var secondFarmId = Guid.NewGuid();
        var firstPlotId = Guid.NewGuid();
        var secondPlotId = Guid.NewGuid();
        var firstOccurrenceId = Guid.NewGuid();
        var secondOccurrenceId = Guid.NewGuid();

        await ArrangeActorAsync(
            database,
            actorId,
            (firstFarmId, ActivePlot(
                firstPlotId,
                firstFarmId,
                now,
                firstOccurrenceId)),
            (secondFarmId, ActivePlot(
                secondPlotId,
                secondFarmId,
                now,
                secondOccurrenceId)));
        await SeedRewardedCompletionsAsync(
            database,
            actorId,
            now.AddHours(-1),
            count: 14);

        await using var firstContext = new AppDbContext(database);
        await using var secondContext = new AppDbContext(database);
        var gate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var first = RemoveAfterGate(
            gate.Task,
            CreateService(firstContext, now),
            actorId,
            firstFarmId,
            firstPlotId,
            firstOccurrenceId,
            Guid.NewGuid());
        var second = RemoveAfterGate(
            gate.Task,
            CreateService(secondContext, now),
            actorId,
            secondFarmId,
            secondPlotId,
            secondOccurrenceId,
            Guid.NewGuid());
        gate.SetResult();
        var attempts = await Task.WhenAll(first, second)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.All(attempts, attempt => Assert.True(attempt.Succeeded));
        Assert.Single(
            attempts,
            attempt => attempt.Response!.RewardGranted);
        Assert.Single(
            attempts,
            attempt => !attempt.Response!.RewardGranted);

        await using var assertion = new AppDbContext(database);
        Assert.Equal(
            102,
            await assertion.Inventories
                .Where(inventory => inventory.UserId == actorId)
                .Select(inventory => inventory.Coins)
                .SingleAsync());
        Assert.Equal(
            16,
            await assertion.PestRemovalCompletions.CountAsync(
                completion => completion.ActorUserId == actorId));
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task MissingInventoryRollsBackOccurrenceStateXpAndCompletion()
    {
        var database = DatabaseOptions();
        var now = RoundedUtcNow();
        var actorId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();

        await ArrangeActorAsync(
            database,
            actorId,
            includeInventory: false,
            (farmId, ActivePlot(plotId, farmId, now, occurrenceId)));

        await using var context = new AppDbContext(database);
        var attempt = await CreateService(context, now).RemoveAsync(
            actorId,
            farmId,
            plotId,
            occurrenceId,
            Guid.NewGuid());

        Assert.Equal(PestActionFailure.InventoryNotFound, attempt.Failure);
        await using var assertion = new AppDbContext(database);
        var plot = await assertion.Plots
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == plotId);
        Assert.Equal(PestStatus.Active, plot.PestStatus);
        Assert.Equal(occurrenceId, plot.PestOccurrenceId);
        Assert.Equal(
            0,
            await assertion.Users
                .Where(user => user.Id == actorId)
                .Select(user => user.CurrentXp)
                .SingleAsync());
        Assert.False(await assertion.PestRemovalCompletions.AnyAsync(
            completion => completion.PlotId == plotId));
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task AutomaticConsumptionNeverCreatesRemovalReward()
    {
        var database = DatabaseOptions();
        var now = RoundedUtcNow();
        var actorId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var plot = ActivePlot(plotId, farmId, now);
        plot.PestConsumesAt = now.AddSeconds(-1);

        await ArrangeActorAsync(database, actorId, (farmId, plot));

        await using var context = new AppDbContext(database);
        Assert.True(await CreateService(context, now).ProcessFarmAsync(farmId));

        await using var assertion = new AppDbContext(database);
        Assert.Equal(
            PestStatus.Consumed,
            await assertion.Plots
                .Where(candidate => candidate.Id == plotId)
                .Select(candidate => candidate.PestStatus)
                .SingleAsync());
        Assert.False(await assertion.PestRemovalCompletions.AnyAsync(
            completion => completion.PlotId == plotId));
        Assert.Equal(
            100,
            await assertion.Inventories
                .Where(inventory => inventory.UserId == actorId)
                .Select(inventory => inventory.Coins)
                .SingleAsync());
        Assert.Equal(
            0,
            await assertion.Users
                .Where(user => user.Id == actorId)
                .Select(user => user.CurrentXp)
                .SingleAsync());
    }

    private static async Task<PestActionAttempt> RemoveAfterGate(
        Task gate,
        PestService service,
        Guid actorId,
        Guid farmId,
        Guid plotId,
        Guid expectedOccurrenceId,
        Guid key)
    {
        await gate;
        return await service.RemoveAsync(
            actorId,
            farmId,
            plotId,
            expectedOccurrenceId,
            key);
    }

    private static async Task ArrangeActorAsync(
        DbContextOptions<AppDbContext> database,
        Guid actorId,
        params (Guid FarmId, Plot Plot)[] farms)
    {
        await ArrangeActorAsync(
            database,
            actorId,
            includeInventory: true,
            farms);
    }

    private static async Task ArrangeActorAsync(
        DbContextOptions<AppDbContext> database,
        Guid actorId,
        bool includeInventory,
        params (Guid FarmId, Plot Plot)[] farms)
    {
        await using var context = new AppDbContext(database);
        context.Users.Add(CreateUser(actorId));

        if (includeInventory)
        {
            context.Inventories.Add(new Inventory
            {
                Id = Guid.NewGuid(),
                UserId = actorId,
                Coins = 100
            });
        }

        foreach (var (farmId, plot) in farms)
        {
            context.Farms.Add(new Farm
            {
                Id = farmId,
                Name = $"Reward farm {farmId:N}",
                UserId = actorId
            });
            context.Plots.Add(plot);
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedRewardedCompletionsAsync(
        DbContextOptions<AppDbContext> database,
        Guid actorId,
        DateTime removedAt,
        int count)
    {
        await using var context = new AppDbContext(database);

        for (var index = 0; index < count; index++)
        {
            context.PestRemovalCompletions.Add(
                new PestRemovalCompletion
                {
                    Id = Guid.NewGuid(),
                    PestOccurrenceId = Guid.NewGuid(),
                    IdempotencyKey = Guid.NewGuid(),
                    ActorUserId = actorId,
                    OwnerUserId = actorId,
                    FarmId = Guid.NewGuid(),
                    PlotId = Guid.NewGuid(),
                    RemovedAt = removedAt.AddMilliseconds(index),
                    RemainingYieldAfter = 3,
                    CoinsGained = 2,
                    XpGained = 5,
                    CoinsAfter = 100
                });
        }

        await context.SaveChangesAsync();
    }

    private static PestService CreateService(
        AppDbContext context,
        DateTime now)
    {
        return new PestService(
            context,
            new ExperienceService(),
            Options.Create(new PestOptions
            {
                Enabled = true,
                RemovalCoinsReward = 2,
                RemovalXpReward = 5,
                MaxRewardedRemovalsPerWindow = 15,
                RemovalRewardRollingWindowHours = 24
            }),
            new FixedTimeProvider(now));
    }

    private static Plot ActivePlot(
        Guid plotId,
        Guid farmId,
        DateTime now,
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
            PestConsumesAt = now.AddMinutes(10)
        };
    }

    private static User CreateUser(Guid id)
    {
        var username = $"reward_{id:N}";
        return new User
        {
            Id = id,
            Username = username,
            NormalizedUsername = username.ToUpperInvariant(),
            PasswordHash = "integration-test"
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

    private static DbContextOptions<AppDbContext> DatabaseOptions()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CROP_CARE_TEST_CONNECTION")!;
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
    }

    private static DateTime RoundedUtcNow()
    {
        var now = DateTime.UtcNow;
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMillisecond));
    }

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(DateTime.SpecifyKind(now, DateTimeKind.Utc));
    }
}
