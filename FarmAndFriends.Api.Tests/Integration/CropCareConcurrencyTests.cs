using System.Security.Claims;
using FarmAndFriends.Api.Configuration;
using FarmAndFriends.Api.Contracts.Care;
using FarmAndFriends.Api.Contracts.Shop;
using FarmAndFriends.Api.Controllers;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Domain.Services;
using FarmAndFriends.Api.Dtos.Shop;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FarmAndFriends.Api.Tests.Integration;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(
                    "CROP_CARE_TEST_CONNECTION")))
        {
            Skip =
                "Set CROP_CARE_TEST_CONNECTION to run PostgreSQL integration tests.";
        }
    }
}

public sealed class CropCareConcurrencyTests
{
    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task ConcurrentVisitors_CanBothCareTheSamePlotAndBeRewarded()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CROP_CARE_TEST_CONNECTION")!;

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var now = new DateTime(2026, 7, 26, 10, 0, 0, DateTimeKind.Utc);
        var clock = new MutableTimeProvider(now);
        var ownerId = Guid.NewGuid();
        var firstVisitorId = Guid.NewGuid();
        var secondVisitorId = Guid.NewGuid();
        var thirdVisitorId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var opportunityId = Guid.NewGuid();

        await using (var arrangeContext = new AppDbContext(dbOptions))
        {
            arrangeContext.Users.AddRange(
                CreateUser(ownerId, "owner"),
                CreateUser(firstVisitorId, "first"),
                CreateUser(secondVisitorId, "second"),
                CreateUser(thirdVisitorId, "third"));
            arrangeContext.Farms.Add(new Farm
            {
                Id = farmId,
                Name = "Concurrency farm",
                UserId = ownerId
            });
            arrangeContext.Plots.Add(
                CreateGrowingPlot(
                    plotId,
                    farmId,
                    opportunityId,
                    x: 0,
                    now));
            arrangeContext.Inventories.AddRange(
                CreateInventory(firstVisitorId),
                CreateInventory(secondVisitorId));
            arrangeContext.Friendships.AddRange(
                CreateAcceptedFriendship(ownerId, firstVisitorId),
                CreateAcceptedFriendship(ownerId, secondVisitorId),
                CreateAcceptedFriendship(ownerId, thirdVisitorId));

            await arrangeContext.SaveChangesAsync();
        }

        await using var firstContext = new AppDbContext(dbOptions);
        await using var secondContext = new AppDbContext(dbOptions);
        var firstService = CreateService(firstContext, clock);
        var secondService = CreateService(secondContext, clock);
        var startGate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var firstAttempt = AttemptAfterGate(
            startGate.Task,
            firstService,
            firstVisitorId,
            farmId,
            plotId,
            opportunityId);
        var secondAttempt = AttemptAfterGate(
            startGate.Task,
            secondService,
            secondVisitorId,
            farmId,
            plotId,
            opportunityId);

        startGate.SetResult();
        var attempts = await Task.WhenAll(firstAttempt, secondAttempt)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.All(attempts, attempt => Assert.True(attempt.Succeeded));
        Assert.Equal(
            new[] { firstVisitorId, secondVisitorId }.Order(),
            attempts
                .Select(attempt => attempt.Response!.CaredByUserId)
                .Order());
        Assert.All(
            attempts,
            attempt =>
            {
                Assert.Equal(2, attempt.Response!.CoinsGained);
                Assert.Equal(2, attempt.Response.XpGained);
                Assert.True(attempt.Response.CycleRewardGranted);
            });

        await using var assertContext = new AppDbContext(dbOptions);
        var savedPlot = await assertContext.Plots
            .AsNoTracking()
            .SingleAsync(plot => plot.Id == plotId);
        var completions = await assertContext.CropCareCompletions
            .AsNoTracking()
            .Where(completion =>
                completion.CareOpportunityId == opportunityId)
            .ToListAsync();
        var cycles = await assertContext.VisitorFarmCareCycles
            .AsNoTracking()
            .Where(cycle =>
                cycle.FarmId == farmId
                && (cycle.VisitorUserId == firstVisitorId
                    || cycle.VisitorUserId == secondVisitorId))
            .ToListAsync();
        var inventories = await assertContext.Inventories
            .AsNoTracking()
            .Where(inventory =>
                inventory.UserId == firstVisitorId
                || inventory.UserId == secondVisitorId)
            .ToListAsync();
        var visitors = await assertContext.Users
            .AsNoTracking()
            .Where(user =>
                user.Id == firstVisitorId || user.Id == secondVisitorId)
            .ToListAsync();
        var notificationCount = await assertContext.Notifications
            .AsNoTracking()
            .CountAsync(notification =>
                notification.Type == NotificationType.CropCaredFor
                && notification.RecipientUserId == ownerId
                && (notification.ActorUserId == firstVisitorId
                    || notification.ActorUserId == secondVisitorId));
        var stateService = CreateService(assertContext, clock);
        var stateReadAt = now;
        var ownerStates = await stateService.GetFarmCareStatesAsync(
            farmId,
            ownerId,
            ownerId,
            new[] { savedPlot },
            stateReadAt);
        var thirdVisitorStates = await stateService.GetFarmCareStatesAsync(
            farmId,
            thirdVisitorId,
            ownerId,
            new[] { savedPlot },
            stateReadAt);

        Assert.Equal(2, completions.Count);
        Assert.Equal(
            new[] { firstVisitorId, secondVisitorId }.Order(),
            completions
                .Select(completion => completion.VisitorUserId)
                .Order());
        Assert.All(completions, completion =>
        {
            Assert.Equal(2, completion.CoinsGained);
            Assert.Equal(2, completion.XpGained);
        });
        Assert.Equal(2, cycles.Count);
        Assert.All(cycles, cycle =>
        {
            Assert.True(cycle.RewardGranted);
            Assert.Equal(TimeSpan.FromHours(5), cycle.EndsAt - cycle.StartedAt);
        });
        Assert.All(inventories, inventory => Assert.Equal(102, inventory.Coins));
        Assert.All(visitors, visitor => Assert.Equal(2, visitor.CurrentXp));
        Assert.Equal(2, notificationCount);

        var ownerState = Assert.Contains(plotId, ownerStates);
        Assert.False(ownerState.CanCare);
        Assert.Equal(2, ownerState.CaregiverCount);
        Assert.Equal(
            completions
                .Select(completion => completion.CaredByUsername)
                .Order(),
            ownerState.Caregivers
                .Select(caregiver => caregiver.Username)
                .Order());

        var thirdVisitorState =
            Assert.Contains(plotId, thirdVisitorStates);
        Assert.True(thirdVisitorState.CanCare);
        Assert.Null(thirdVisitorState.ViewerCare);
        Assert.Equal(0, thirdVisitorState.CaregiverCount);
        Assert.Empty(thirdVisitorState.Caregivers);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task ConcurrentPlots_SameVisitorUsesOneCycleAndEarnsPerPlot()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CROP_CARE_TEST_CONNECTION")!;
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var now = DateTime.UtcNow;
        var ownerId = Guid.NewGuid();
        var visitorId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var firstPlotId = Guid.NewGuid();
        var secondPlotId = Guid.NewGuid();
        var firstOpportunityId = Guid.NewGuid();
        var secondOpportunityId = Guid.NewGuid();

        await using (var arrangeContext = new AppDbContext(dbOptions))
        {
            arrangeContext.Users.AddRange(
                CreateUser(ownerId, "owner"),
                CreateUser(visitorId, "multi_plot"));
            arrangeContext.Farms.Add(new Farm
            {
                Id = farmId,
                Name = "Multi-plot farm",
                UserId = ownerId
            });
            arrangeContext.Plots.AddRange(
                CreateGrowingPlot(
                    firstPlotId,
                    farmId,
                    firstOpportunityId,
                    x: 0,
                    now),
                CreateGrowingPlot(
                    secondPlotId,
                    farmId,
                    secondOpportunityId,
                    x: 1,
                    now));
            arrangeContext.Inventories.Add(CreateInventory(visitorId));
            arrangeContext.Friendships.Add(
                CreateAcceptedFriendship(ownerId, visitorId));
            await arrangeContext.SaveChangesAsync();
        }

        await using var firstContext = new AppDbContext(dbOptions);
        await using var secondContext = new AppDbContext(dbOptions);
        var gate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstTask = AttemptAfterGate(
            gate.Task,
            CreateService(firstContext),
            visitorId,
            farmId,
            firstPlotId,
            firstOpportunityId);
        var secondTask = AttemptAfterGate(
            gate.Task,
            CreateService(secondContext),
            visitorId,
            farmId,
            secondPlotId,
            secondOpportunityId);

        gate.SetResult();
        var attempts = await Task.WhenAll(firstTask, secondTask)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.All(attempts, attempt => Assert.True(attempt.Succeeded));
        Assert.All(attempts, attempt =>
        {
            Assert.Equal(2, attempt.Response!.CoinsGained);
            Assert.Equal(2, attempt.Response.XpGained);
            Assert.True(attempt.Response.CycleRewardGranted);
        });
        Assert.Single(
            attempts
                .Select(attempt => attempt.Response!.CareCycleId)
                .Distinct());

        await using var assertContext = new AppDbContext(dbOptions);
        var completions = await assertContext.CropCareCompletions
            .AsNoTracking()
            .Where(completion =>
                completion.VisitorUserId == visitorId
                && (completion.CareOpportunityId == firstOpportunityId
                    || completion.CareOpportunityId == secondOpportunityId))
            .ToListAsync();
        var cycle = await assertContext.VisitorFarmCareCycles
            .AsNoTracking()
            .SingleAsync(candidate =>
                candidate.VisitorUserId == visitorId
                && candidate.FarmId == farmId);
        var plots = await assertContext.Plots
            .AsNoTracking()
            .Where(plot =>
                plot.Id == firstPlotId || plot.Id == secondPlotId)
            .ToListAsync();

        Assert.Equal(2, completions.Count);
        Assert.Equal(
            new[] { firstPlotId, secondPlotId }.Order(),
            completions.Select(completion => completion.PlotId).Order());
        Assert.All(completions, completion =>
        {
            Assert.Equal(cycle.Id, completion.VisitorFarmCareCycleId);
            Assert.Equal(2, completion.CoinsGained);
            Assert.Equal(2, completion.XpGained);
        });
        Assert.Equal(TimeSpan.FromHours(5), cycle.EndsAt - cycle.StartedAt);
        Assert.True(cycle.RewardGranted);
        Assert.All(
            plots,
            plot => Assert.NotNull(plot.CareOpportunityId));
        Assert.Equal(
            104,
            await assertContext.Inventories
                .Where(inventory => inventory.UserId == visitorId)
                .Select(inventory => inventory.Coins)
                .SingleAsync());
        Assert.Equal(
            4,
            await assertContext.Users
                .Where(user => user.Id == visitorId)
                .Select(user => user.CurrentXp)
                .SingleAsync());
        Assert.Equal(
            1,
            await assertContext.Notifications.CountAsync(notification =>
                notification.Type == NotificationType.CropCaredFor
                && notification.ActorUserId == visitorId
                && notification.RecipientUserId == ownerId));
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task WaitingRequest_CannotCareCropThatMaturesUnderLock()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CROP_CARE_TEST_CONNECTION")!;
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var now = new DateTime(2026, 7, 26, 12, 0, 0, DateTimeKind.Utc);
        var clock = new MutableTimeProvider(now);
        var ownerId = Guid.NewGuid();
        var visitorId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var opportunityId = Guid.NewGuid();

        await using (var arrangeContext = new AppDbContext(dbOptions))
        {
            arrangeContext.Users.AddRange(
                CreateUser(ownerId, "owner"),
                CreateUser(visitorId, "waiting"));
            arrangeContext.Farms.Add(new Farm
            {
                Id = farmId,
                Name = "Maturing farm",
                UserId = ownerId
            });
            arrangeContext.Plots.Add(new Plot
            {
                Id = plotId,
                FarmId = farmId,
                X = 0,
                Y = 0,
                Unlocked = true,
                SeedId = "corn",
                PlantedAt = now.AddMinutes(-1),
                ReadyAt = now.AddHours(1),
                RemainingYield = 3,
                CareOpportunityId = opportunityId
            });
            arrangeContext.Inventories.Add(CreateInventory(visitorId));
            arrangeContext.Friendships.Add(
                CreateAcceptedFriendship(ownerId, visitorId));
            await arrangeContext.SaveChangesAsync();
        }

        await using var lockContext = new AppDbContext(dbOptions);
        await using var lockTransaction =
            await lockContext.Database.BeginTransactionAsync();
        await lockContext.Plots
            .FromSqlInterpolated(
                $"""SELECT * FROM "Plots" WHERE "Id" = {plotId} FOR UPDATE""")
            .SingleAsync();

        await using var careContext = new AppDbContext(dbOptions);
        var careTask = CreateService(careContext, clock).CareAsync(
            visitorId,
            farmId,
            plotId,
            opportunityId,
            Guid.NewGuid());

        clock.SetUtcNow(now.AddHours(1));
        await lockTransaction.CommitAsync();

        var attempt = await careTask;

        Assert.Equal(
            CropCareFailure.NotGrowing,
            attempt.Failure);

        await using var assertContext = new AppDbContext(dbOptions);
        var savedPlot = await assertContext.Plots
            .AsNoTracking()
            .SingleAsync(plot => plot.Id == plotId);
        var inventory = await assertContext.Inventories
            .AsNoTracking()
            .SingleAsync(candidate => candidate.UserId == visitorId);
        var notificationExists = await assertContext.Notifications
            .AsNoTracking()
            .AnyAsync(notification =>
                notification.Type == NotificationType.CropCaredFor
                && notification.ActorUserId == visitorId
                && notification.RecipientUserId == ownerId);

        Assert.Equal(opportunityId, savedPlot.CareOpportunityId);
        Assert.Equal(100, inventory.Coins);
        Assert.False(notificationExists);
        Assert.False(
            await assertContext.CropCareCompletions.AnyAsync(completion =>
                completion.VisitorUserId == visitorId
                && completion.CareOpportunityId == opportunityId));
        Assert.False(
            await assertContext.VisitorFarmCareCycles.AnyAsync(cycle =>
                cycle.VisitorUserId == visitorId
                && cycle.FarmId == farmId));
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task SameVisitor_LongCropCooldownBlocksBeforeFiveHoursAndAllowsAtBoundary()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CROP_CARE_TEST_CONNECTION")!;
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var now = new DateTime(2026, 7, 26, 13, 0, 0, DateTimeKind.Utc);
        var clock = new MutableTimeProvider(now);
        var ownerId = Guid.NewGuid();
        var visitorId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var opportunityId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid();

        await using (var arrangeContext = new AppDbContext(dbOptions))
        {
            arrangeContext.Users.AddRange(
                CreateUser(ownerId, "long_crop_owner"),
                CreateUser(visitorId, "long_crop_visitor"));
            arrangeContext.Farms.Add(new Farm
            {
                Id = farmId,
                Name = "Long crop farm",
                UserId = ownerId
            });
            arrangeContext.Plots.Add(
                CreateGrowingPlot(
                    plotId,
                    farmId,
                    opportunityId,
                    x: 0,
                    now,
                    TimeSpan.FromDays(7)));
            arrangeContext.Inventories.Add(CreateInventory(visitorId));
            arrangeContext.Friendships.Add(
                CreateAcceptedFriendship(ownerId, visitorId));
            await arrangeContext.SaveChangesAsync();
        }

        CareForPlotResponse firstResponse;
        await using (var firstContext = new AppDbContext(dbOptions))
        {
            var first = await CreateService(firstContext, clock).CareAsync(
                visitorId,
                farmId,
                plotId,
                opportunityId,
                idempotencyKey);

            Assert.True(first.Succeeded);
            firstResponse = first.Response!;
            Assert.Equal(now, firstResponse.CaredAt);
            Assert.Equal(now.AddHours(5), firstResponse.NextCareAt);
            Assert.Equal(2, firstResponse.CoinsGained);
            Assert.Equal(2, firstResponse.XpGained);
        }

        var beforeBoundary = now.AddHours(5).AddSeconds(-1);
        clock.SetUtcNow(beforeBoundary);

        await using (var cooldownContext = new AppDbContext(dbOptions))
        {
            var service = CreateService(cooldownContext, clock);
            var replay = await service.CareAsync(
                visitorId,
                farmId,
                plotId,
                opportunityId,
                idempotencyKey);
            var newRequest = await service.CareAsync(
                visitorId,
                farmId,
                plotId,
                opportunityId,
                Guid.NewGuid());

            Assert.True(replay.Succeeded);
            Assert.Equal(firstResponse, replay.Response);
            Assert.Equal(
                CropCareFailure.CooldownActive,
                newRequest.Failure);
            Assert.Equal(now.AddHours(5), newRequest.NextCareAt);

            var plot = await cooldownContext.Plots
                .AsNoTracking()
                .SingleAsync(candidate => candidate.Id == plotId);
            var states = await service.GetFarmCareStatesAsync(
                farmId,
                visitorId,
                ownerId,
                new[] { plot },
                beforeBoundary);
            var state = Assert.Contains(plotId, states);

            Assert.False(state.CanCare);
            Assert.Equal(now.AddHours(5), state.NextCareAt);
            Assert.NotNull(state.ViewerCare);
        }

        await using (var unchangedContext = new AppDbContext(dbOptions))
        {
            Assert.Equal(
                1,
                await unchangedContext.CropCareCompletions.CountAsync(
                    completion =>
                        completion.VisitorUserId == visitorId
                        && completion.CareOpportunityId
                            == opportunityId));
            Assert.Equal(
                102,
                await unchangedContext.Inventories
                    .Where(inventory =>
                        inventory.UserId == visitorId)
                    .Select(inventory => inventory.Coins)
                    .SingleAsync());
            Assert.Equal(
                2,
                await unchangedContext.Users
                    .Where(user => user.Id == visitorId)
                    .Select(user => user.CurrentXp)
                    .SingleAsync());
        }

        var boundary = now.AddHours(5);
        clock.SetUtcNow(boundary);

        CareForPlotResponse secondResponse;
        await using (var boundaryContext = new AppDbContext(dbOptions))
        {
            var second = await CreateService(boundaryContext, clock).CareAsync(
                visitorId,
                farmId,
                plotId,
                opportunityId,
                Guid.NewGuid());

            Assert.True(second.Succeeded);
            secondResponse = second.Response!;
            Assert.Equal(boundary, secondResponse.CaredAt);
            Assert.Equal(boundary.AddHours(5), secondResponse.NextCareAt);
            Assert.Equal(opportunityId, secondResponse.CareOpportunityId);
            Assert.Equal(2, secondResponse.CoinsGained);
            Assert.Equal(2, secondResponse.XpGained);
        }

        Assert.NotEqual(
            firstResponse.CompletionId,
            secondResponse.CompletionId);
        Assert.NotEqual(
            firstResponse.CareCycleId,
            secondResponse.CareCycleId);

        await using var assertContext = new AppDbContext(dbOptions);
        var completions = await assertContext.CropCareCompletions
            .AsNoTracking()
            .Where(completion =>
                completion.VisitorUserId == visitorId
                && completion.CareOpportunityId == opportunityId)
            .OrderBy(completion => completion.CaredAt)
            .ToListAsync();

        Assert.Equal(2, completions.Count);
        Assert.Equal(now, completions[0].CaredAt);
        Assert.Equal(boundary, completions[1].CaredAt);
        Assert.Equal(
            104,
            await assertContext.Inventories
                .Where(inventory => inventory.UserId == visitorId)
                .Select(inventory => inventory.Coins)
                .SingleAsync());
        Assert.Equal(
            4,
            await assertContext.Users
                .Where(user => user.Id == visitorId)
                .Select(user => user.CurrentXp)
                .SingleAsync());
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task SameVisitor_OtherPlotAndOtherFarmAreImmediatelyIndependent()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CROP_CARE_TEST_CONNECTION")!;
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var now = new DateTime(2026, 7, 26, 14, 0, 0, DateTimeKind.Utc);
        var clock = new MutableTimeProvider(now);
        var firstOwnerId = Guid.NewGuid();
        var secondOwnerId = Guid.NewGuid();
        var visitorId = Guid.NewGuid();
        var firstFarmId = Guid.NewGuid();
        var secondFarmId = Guid.NewGuid();
        var firstPlotId = Guid.NewGuid();
        var secondPlotId = Guid.NewGuid();
        var otherFarmPlotId = Guid.NewGuid();
        var firstOpportunityId = Guid.NewGuid();
        var secondOpportunityId = Guid.NewGuid();
        var otherFarmOpportunityId = Guid.NewGuid();

        await using (var arrangeContext = new AppDbContext(dbOptions))
        {
            arrangeContext.Users.AddRange(
                CreateUser(firstOwnerId, "first_owner"),
                CreateUser(secondOwnerId, "second_owner"),
                CreateUser(visitorId, "independent_visitor"));
            arrangeContext.Farms.AddRange(
                new Farm
                {
                    Id = firstFarmId,
                    Name = "First independent farm",
                    UserId = firstOwnerId
                },
                new Farm
                {
                    Id = secondFarmId,
                    Name = "Second independent farm",
                    UserId = secondOwnerId
                });
            arrangeContext.Plots.AddRange(
                CreateGrowingPlot(
                    firstPlotId,
                    firstFarmId,
                    firstOpportunityId,
                    x: 0,
                    now,
                    TimeSpan.FromDays(1)),
                CreateGrowingPlot(
                    secondPlotId,
                    firstFarmId,
                    secondOpportunityId,
                    x: 1,
                    now,
                    TimeSpan.FromDays(1)),
                CreateGrowingPlot(
                    otherFarmPlotId,
                    secondFarmId,
                    otherFarmOpportunityId,
                    x: 0,
                    now,
                    TimeSpan.FromDays(1)));
            arrangeContext.Inventories.Add(CreateInventory(visitorId));
            arrangeContext.Friendships.AddRange(
                CreateAcceptedFriendship(firstOwnerId, visitorId),
                CreateAcceptedFriendship(secondOwnerId, visitorId));
            await arrangeContext.SaveChangesAsync();
        }

        CareForPlotResponse firstResponse;
        await using (var firstContext = new AppDbContext(dbOptions))
        {
            var attempt = await CreateService(firstContext, clock).CareAsync(
                visitorId,
                firstFarmId,
                firstPlotId,
                firstOpportunityId,
                Guid.NewGuid());

            Assert.True(attempt.Succeeded);
            firstResponse = attempt.Response!;
        }

        CareForPlotResponse secondResponse;
        await using (var secondContext = new AppDbContext(dbOptions))
        {
            var attempt = await CreateService(secondContext, clock).CareAsync(
                visitorId,
                firstFarmId,
                secondPlotId,
                secondOpportunityId,
                Guid.NewGuid());

            Assert.True(attempt.Succeeded);
            secondResponse = attempt.Response!;
        }

        CareForPlotResponse otherFarmResponse;
        await using (var otherFarmContext = new AppDbContext(dbOptions))
        {
            var attempt = await CreateService(
                    otherFarmContext,
                    clock)
                .CareAsync(
                    visitorId,
                    secondFarmId,
                    otherFarmPlotId,
                    otherFarmOpportunityId,
                    Guid.NewGuid());

            Assert.True(attempt.Succeeded);
            otherFarmResponse = attempt.Response!;
        }

        Assert.Equal(firstResponse.CareCycleId, secondResponse.CareCycleId);
        Assert.NotEqual(
            firstResponse.CareCycleId,
            otherFarmResponse.CareCycleId);
        Assert.All(
            new[] { firstResponse, secondResponse, otherFarmResponse },
            response =>
            {
                Assert.Equal(now, response.CaredAt);
                Assert.Equal(2, response.CoinsGained);
                Assert.Equal(2, response.XpGained);
            });

        await using var assertContext = new AppDbContext(dbOptions);
        Assert.Equal(
            3,
            await assertContext.CropCareCompletions.CountAsync(
                completion => completion.VisitorUserId == visitorId));
        Assert.Equal(
            2,
            await assertContext.VisitorFarmCareCycles.CountAsync(
                cycle => cycle.VisitorUserId == visitorId));
        Assert.Equal(
            106,
            await assertContext.Inventories
                .Where(inventory => inventory.UserId == visitorId)
                .Select(inventory => inventory.Coins)
                .SingleAsync());
        Assert.Equal(
            6,
            await assertContext.Users
                .Where(user => user.Id == visitorId)
                .Select(user => user.CurrentXp)
                .SingleAsync());
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task Retry_ReplaysOriginalCompletionWithoutAnotherReward()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CROP_CARE_TEST_CONNECTION")!;
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var now = DateTime.UtcNow;
        var ownerId = Guid.NewGuid();
        var visitorId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var opportunityId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid();

        await using (var arrangeContext = new AppDbContext(dbOptions))
        {
            arrangeContext.Users.AddRange(
                CreateUser(ownerId, "owner"),
                CreateUser(visitorId, "replay"));
            arrangeContext.Farms.Add(new Farm
            {
                Id = farmId,
                Name = "Replay farm",
                UserId = ownerId
            });
            arrangeContext.Plots.Add(
                CreateGrowingPlot(
                    plotId,
                    farmId,
                    opportunityId,
                    x: 0,
                    now));
            arrangeContext.Inventories.Add(CreateInventory(visitorId));
            arrangeContext.Friendships.Add(
                CreateAcceptedFriendship(ownerId, visitorId));
            await arrangeContext.SaveChangesAsync();
        }

        CareForPlotResponse firstResponse;
        await using (var firstContext = new AppDbContext(dbOptions))
        {
            var first = await CreateService(firstContext).CareAsync(
                visitorId,
                farmId,
                plotId,
                opportunityId,
                idempotencyKey);

            Assert.True(first.Succeeded);
            firstResponse = first.Response!;
        }

        await using (var mutateContext = new AppDbContext(dbOptions))
        {
            var visitor = await mutateContext.Users
                .SingleAsync(user => user.Id == visitorId);
            var inventory = await mutateContext.Inventories
                .SingleAsync(candidate => candidate.UserId == visitorId);
            var plot = await mutateContext.Plots
                .SingleAsync(candidate => candidate.Id == plotId);

            visitor.Username = $"renamed_{visitorId:N}";
            visitor.NormalizedUsername = visitor.Username.ToUpperInvariant();
            inventory.Coins += 50;
            plot.SeedId = null;
            plot.PlantedAt = null;
            plot.ReadyAt = null;
            plot.CareOpportunityId = null;

            await mutateContext.SaveChangesAsync();
        }

        await using (var retryContext = new AppDbContext(dbOptions))
        {
            var retry = await CreateService(retryContext).CareAsync(
                visitorId,
                farmId,
                plotId,
                opportunityId,
                idempotencyKey);
            var retryWithNewKey = await CreateService(retryContext).CareAsync(
                visitorId,
                farmId,
                plotId,
                opportunityId,
                Guid.NewGuid());

            Assert.True(retry.Succeeded);
            Assert.Equal(firstResponse, retry.Response);
            Assert.Equal(
                CropCareFailure.NotGrowing,
                retryWithNewKey.Failure);
        }

        await using var assertContext = new AppDbContext(dbOptions);
        Assert.Equal(
            1,
            await assertContext.CropCareCompletions.CountAsync(completion =>
                completion.CareOpportunityId == opportunityId));
        Assert.Equal(
            1,
            await assertContext.Notifications.CountAsync(notification =>
                notification.Type == NotificationType.CropCaredFor
                && notification.ActorUserId == visitorId
                && notification.RecipientUserId == ownerId));
        Assert.Equal(
            1,
            await assertContext.VisitorFarmCareCycles.CountAsync(cycle =>
                cycle.VisitorUserId == visitorId
                && cycle.FarmId == farmId));
        Assert.Equal(
            152,
            await assertContext.Inventories
                .Where(inventory => inventory.UserId == visitorId)
                .Select(inventory => inventory.Coins)
                .SingleAsync());
        Assert.Equal(
            2,
            await assertContext.Users
                .Where(user => user.Id == visitorId)
                .Select(user => user.CurrentXp)
                .SingleAsync());
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task ConcurrentDuplicateRequests_ReturnTheSameSingleReward()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CROP_CARE_TEST_CONNECTION")!;
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var now = DateTime.UtcNow;
        var ownerId = Guid.NewGuid();
        var visitorId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var opportunityId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid();

        await using (var arrangeContext = new AppDbContext(dbOptions))
        {
            arrangeContext.Users.AddRange(
                CreateUser(ownerId, "owner"),
                CreateUser(visitorId, "duplicate"));
            arrangeContext.Farms.Add(new Farm
            {
                Id = farmId,
                Name = "Duplicate request farm",
                UserId = ownerId
            });
            arrangeContext.Plots.Add(
                CreateGrowingPlot(
                    plotId,
                    farmId,
                    opportunityId,
                    x: 0,
                    now));
            arrangeContext.Inventories.Add(CreateInventory(visitorId));
            arrangeContext.Friendships.Add(
                CreateAcceptedFriendship(ownerId, visitorId));
            await arrangeContext.SaveChangesAsync();
        }

        await using var firstContext = new AppDbContext(dbOptions);
        await using var secondContext = new AppDbContext(dbOptions);
        var gate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstTask = AttemptAfterGate(
            gate.Task,
            CreateService(firstContext),
            visitorId,
            farmId,
            plotId,
            opportunityId,
            idempotencyKey);
        var secondTask = AttemptAfterGate(
            gate.Task,
            CreateService(secondContext),
            visitorId,
            farmId,
            plotId,
            opportunityId,
            idempotencyKey);

        gate.SetResult();
        var attempts = await Task.WhenAll(firstTask, secondTask)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.All(attempts, attempt => Assert.True(attempt.Succeeded));
        Assert.Equal(attempts[0].Response, attempts[1].Response);

        await using var assertContext = new AppDbContext(dbOptions);
        Assert.Equal(
            1,
            await assertContext.CropCareCompletions.CountAsync(completion =>
                completion.CareOpportunityId == opportunityId));
        Assert.Equal(
            1,
            await assertContext.Notifications.CountAsync(notification =>
                notification.Type == NotificationType.CropCaredFor
                && notification.ActorUserId == visitorId
                && notification.RecipientUserId == ownerId));
        Assert.Equal(
            1,
            await assertContext.VisitorFarmCareCycles.CountAsync(cycle =>
                cycle.VisitorUserId == visitorId
                && cycle.FarmId == farmId));
        Assert.Equal(
            102,
            await assertContext.Inventories
                .Where(inventory => inventory.UserId == visitorId)
                .Select(inventory => inventory.Coins)
                .SingleAsync());
        Assert.Equal(
            2,
            await assertContext.Users
                .Where(user => user.Id == visitorId)
                .Select(user => user.CurrentXp)
                .SingleAsync());
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task ReusedIdempotencyKey_DoesNotTouchAnotherOpportunity()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CROP_CARE_TEST_CONNECTION")!;
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var now = DateTime.UtcNow;
        var ownerId = Guid.NewGuid();
        var visitorId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var firstPlotId = Guid.NewGuid();
        var firstOpportunityId = Guid.NewGuid();
        var secondPlotId = Guid.NewGuid();
        var secondOpportunityId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid();

        await using (var arrangeContext = new AppDbContext(dbOptions))
        {
            arrangeContext.Users.AddRange(
                CreateUser(ownerId, "owner"),
                CreateUser(visitorId, "key"));
            arrangeContext.Farms.Add(new Farm
            {
                Id = farmId,
                Name = "Key reuse farm",
                UserId = ownerId
            });
            arrangeContext.Plots.AddRange(
                CreateGrowingPlot(
                    firstPlotId,
                    farmId,
                    firstOpportunityId,
                    x: 0,
                    now),
                CreateGrowingPlot(
                    secondPlotId,
                    farmId,
                    secondOpportunityId,
                    x: 1,
                    now));
            arrangeContext.Inventories.Add(CreateInventory(visitorId));
            arrangeContext.Friendships.Add(
                CreateAcceptedFriendship(ownerId, visitorId));
            await arrangeContext.SaveChangesAsync();
        }

        await using var careContext = new AppDbContext(dbOptions);
        var service = CreateService(careContext);
        var first = await service.CareAsync(
            visitorId,
            farmId,
            firstPlotId,
            firstOpportunityId,
            idempotencyKey);
        var reused = await service.CareAsync(
            visitorId,
            farmId,
            secondPlotId,
            secondOpportunityId,
            idempotencyKey);

        Assert.True(first.Succeeded);
        Assert.Equal(
            CropCareFailure.IdempotencyKeyReused,
            reused.Failure);

        await using var assertContext = new AppDbContext(dbOptions);
        Assert.Equal(
            1,
            await assertContext.CropCareCompletions.CountAsync(completion =>
                completion.VisitorUserId == visitorId));
        Assert.False(
            await assertContext.CropCareCompletions.AnyAsync(completion =>
                completion.VisitorUserId == visitorId
                && completion.CareOpportunityId == secondOpportunityId));
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task RewardQuota_IsIndependentPerVisitorAndFarmWithinWindow()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CROP_CARE_TEST_CONNECTION")!;
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var now = new DateTime(2026, 7, 26, 12, 0, 0, DateTimeKind.Utc);
        var clock = new MutableTimeProvider(now);
        var anaOwnerId = Guid.NewGuid();
        var paulaOwnerId = Guid.NewGuid();
        var visitorId = Guid.NewGuid();
        var anaFarmId = Guid.NewGuid();
        var paulaFarmId = Guid.NewGuid();
        var anaCaredPlotId = Guid.NewGuid();
        var anaAvailablePlotId = Guid.NewGuid();
        var paulaCaredPlotId = Guid.NewGuid();
        var paulaAvailablePlotId = Guid.NewGuid();
        var anaCaredOpportunityId = Guid.NewGuid();
        var anaAvailableOpportunityId = Guid.NewGuid();
        var paulaCaredOpportunityId = Guid.NewGuid();
        var paulaAvailableOpportunityId = Guid.NewGuid();

        await using (var arrangeContext = new AppDbContext(dbOptions))
        {
            arrangeContext.Users.AddRange(
                CreateUser(anaOwnerId, "ana"),
                CreateUser(paulaOwnerId, "paula"),
                CreateUser(visitorId, "per_farm_quota"));
            arrangeContext.Farms.AddRange(
                new Farm
                {
                    Id = anaFarmId,
                    Name = "Ana farm",
                    UserId = anaOwnerId
                },
                new Farm
                {
                    Id = paulaFarmId,
                    Name = "Paula farm",
                    UserId = paulaOwnerId
                });
            arrangeContext.Plots.AddRange(
                CreateGrowingPlot(
                    anaCaredPlotId,
                    anaFarmId,
                    anaCaredOpportunityId,
                    x: 0,
                    now,
                    TimeSpan.FromDays(7)),
                CreateGrowingPlot(
                    anaAvailablePlotId,
                    anaFarmId,
                    anaAvailableOpportunityId,
                    x: 1,
                    now,
                    TimeSpan.FromDays(7)),
                CreateGrowingPlot(
                    paulaCaredPlotId,
                    paulaFarmId,
                    paulaCaredOpportunityId,
                    x: 0,
                    now,
                    TimeSpan.FromDays(7)),
                CreateGrowingPlot(
                    paulaAvailablePlotId,
                    paulaFarmId,
                    paulaAvailableOpportunityId,
                    x: 1,
                    now,
                    TimeSpan.FromDays(7)));
            arrangeContext.Inventories.Add(CreateInventory(visitorId));
            arrangeContext.Friendships.AddRange(
                CreateAcceptedFriendship(anaOwnerId, visitorId),
                CreateAcceptedFriendship(paulaOwnerId, visitorId));

            // The quota is a safety guard. Pack completed five-hour cycles
            // inside the rolling window so the next cycle exercises the cap.
            arrangeContext.VisitorFarmCareCycles.AddRange(
                Enumerable.Range(0, 5).Select(index =>
                {
                    var startedAt = now.AddHours(-23 + index * 4);
                    return new VisitorFarmCareCycle
                    {
                        Id = Guid.NewGuid(),
                        VisitorUserId = visitorId,
                        OwnerUserId = anaOwnerId,
                        FarmId = anaFarmId,
                        StartedAt = startedAt,
                        EndsAt = startedAt.AddHours(5),
                        RewardGranted = true,
                        CoinsReward = 2,
                        XpReward = 2
                    };
                }).Concat(
                    Enumerable.Range(0, 4).Select(index =>
                    {
                        var startedAt = now.AddHours(-23 + index * 4);
                        return new VisitorFarmCareCycle
                        {
                            Id = Guid.NewGuid(),
                            VisitorUserId = visitorId,
                            OwnerUserId = paulaOwnerId,
                            FarmId = paulaFarmId,
                            StartedAt = startedAt,
                            EndsAt = startedAt.AddHours(5),
                            RewardGranted = true,
                            CoinsReward = 2,
                            XpReward = 2
                        };
                    })));

            await arrangeContext.SaveChangesAsync();
        }

        await using (var stateContext = new AppDbContext(dbOptions))
        {
            var plots = await stateContext.Plots
                .AsNoTracking()
                .Where(plot =>
                    plot.Id == anaCaredPlotId
                    || plot.Id == paulaCaredPlotId)
                .ToDictionaryAsync(plot => plot.Id);
            var service = CreateService(stateContext, clock);
            var anaStates = await service.GetFarmCareStatesAsync(
                anaFarmId,
                visitorId,
                anaOwnerId,
                new[] { plots[anaCaredPlotId] },
                now);
            var paulaStates = await service.GetFarmCareStatesAsync(
                paulaFarmId,
                visitorId,
                paulaOwnerId,
                new[] { plots[paulaCaredPlotId] },
                now);

            var anaState = Assert.Contains(anaCaredPlotId, anaStates);
            var paulaState = Assert.Contains(paulaCaredPlotId, paulaStates);

            Assert.True(anaState.CanCare);
            Assert.False(anaState.RewardAvailable);
            Assert.Null(anaState.CareCycleEndsAt);
            Assert.True(paulaState.CanCare);
            Assert.True(paulaState.RewardAvailable);
            Assert.Null(paulaState.CareCycleEndsAt);
        }

        CareForPlotResponse anaSixthResponse;
        await using (var anaCareContext = new AppDbContext(dbOptions))
        {
            var attempt = await CreateService(anaCareContext, clock).CareAsync(
                visitorId,
                anaFarmId,
                anaCaredPlotId,
                anaCaredOpportunityId,
                Guid.NewGuid());

            Assert.True(attempt.Succeeded);
            anaSixthResponse = attempt.Response!;
            Assert.Equal(0, anaSixthResponse.CoinsGained);
            Assert.Equal(0, anaSixthResponse.XpGained);
            Assert.Equal(100, anaSixthResponse.Coins);
            Assert.False(anaSixthResponse.CycleRewardGranted);
        }

        CareForPlotResponse paulaFifthResponse;
        await using (var paulaCareContext = new AppDbContext(dbOptions))
        {
            var attempt = await CreateService(paulaCareContext, clock)
                .CareAsync(
                    visitorId,
                    paulaFarmId,
                    paulaCaredPlotId,
                    paulaCaredOpportunityId,
                    Guid.NewGuid());

            Assert.True(attempt.Succeeded);
            paulaFifthResponse = attempt.Response!;
            Assert.Equal(2, paulaFifthResponse.CoinsGained);
            Assert.Equal(2, paulaFifthResponse.XpGained);
            Assert.Equal(102, paulaFifthResponse.Coins);
            Assert.True(paulaFifthResponse.CycleRewardGranted);
        }

        await using var assertContext = new AppDbContext(dbOptions);
        var completions = await assertContext.CropCareCompletions
            .AsNoTracking()
            .Where(completion =>
                completion.VisitorUserId == visitorId
                && (completion.FarmId == anaFarmId
                    || completion.FarmId == paulaFarmId))
            .ToListAsync();
        var cycles = await assertContext.VisitorFarmCareCycles
            .AsNoTracking()
            .Where(cycle =>
                cycle.VisitorUserId == visitorId
                && (cycle.FarmId == anaFarmId
                    || cycle.FarmId == paulaFarmId))
            .ToListAsync();

        var anaCompletion = Assert.Single(
            completions,
            completion => completion.FarmId == anaFarmId);
        var paulaCompletion = Assert.Single(
            completions,
            completion => completion.FarmId == paulaFarmId);
        var anaCurrentCycle = Assert.Single(
            cycles,
            cycle => cycle.Id == anaCompletion.VisitorFarmCareCycleId);
        var paulaCurrentCycle = Assert.Single(
            cycles,
            cycle => cycle.Id == paulaCompletion.VisitorFarmCareCycleId);

        Assert.Equal(0, anaCompletion.CoinsGained);
        Assert.Equal(0, anaCompletion.XpGained);
        Assert.False(anaCurrentCycle.RewardGranted);
        Assert.Equal(0, anaCurrentCycle.CoinsReward);
        Assert.Equal(0, anaCurrentCycle.XpReward);
        Assert.Equal(now, anaCurrentCycle.StartedAt);
        Assert.Equal(now.AddHours(5), anaCurrentCycle.EndsAt);
        Assert.Equal(2, paulaCompletion.CoinsGained);
        Assert.Equal(2, paulaCompletion.XpGained);
        Assert.True(paulaCurrentCycle.RewardGranted);
        Assert.Equal(2, paulaCurrentCycle.CoinsReward);
        Assert.Equal(2, paulaCurrentCycle.XpReward);
        Assert.Equal(now, paulaCurrentCycle.StartedAt);
        Assert.Equal(now.AddHours(5), paulaCurrentCycle.EndsAt);
        Assert.All(
            cycles,
            cycle => Assert.Equal(
                TimeSpan.FromHours(5),
                cycle.EndsAt - cycle.StartedAt));
        Assert.Equal(
            5,
            cycles.Count(cycle =>
                cycle.FarmId == anaFarmId && cycle.RewardGranted));
        Assert.Equal(
            6,
            cycles.Count(cycle => cycle.FarmId == anaFarmId));
        Assert.Equal(
            5,
            cycles.Count(cycle =>
                cycle.FarmId == paulaFarmId && cycle.RewardGranted));
        Assert.Equal(
            5,
            cycles.Count(cycle => cycle.FarmId == paulaFarmId));
        Assert.All(
            cycles,
            cycle => Assert.True(
                cycle.StartedAt > now.AddHours(-24)));
        Assert.Equal(
            102,
            await assertContext.Inventories
                .Where(inventory => inventory.UserId == visitorId)
                .Select(inventory => inventory.Coins)
                .SingleAsync());
        Assert.Equal(
            2,
            await assertContext.Users
                .Where(user => user.Id == visitorId)
                .Select(user => user.CurrentXp)
                .SingleAsync());

        var availablePlots = await assertContext.Plots
            .AsNoTracking()
            .Where(plot =>
                plot.Id == anaAvailablePlotId
                || plot.Id == paulaAvailablePlotId)
            .ToDictionaryAsync(plot => plot.Id);
        var stateService = CreateService(assertContext, clock);
        var anaStatesAfterCare = await stateService.GetFarmCareStatesAsync(
            anaFarmId,
            visitorId,
            anaOwnerId,
            new[] { availablePlots[anaAvailablePlotId] },
            now);
        var paulaStatesAfterCare = await stateService.GetFarmCareStatesAsync(
            paulaFarmId,
            visitorId,
            paulaOwnerId,
            new[] { availablePlots[paulaAvailablePlotId] },
            now);
        var anaStateAfterCare = Assert.Contains(
            anaAvailablePlotId,
            anaStatesAfterCare);
        var paulaStateAfterCare = Assert.Contains(
            paulaAvailablePlotId,
            paulaStatesAfterCare);

        Assert.True(anaStateAfterCare.CanCare);
        Assert.False(anaStateAfterCare.RewardAvailable);
        Assert.Equal(now.AddHours(5), anaStateAfterCare.CareCycleEndsAt);
        Assert.True(paulaStateAfterCare.CanCare);
        Assert.True(paulaStateAfterCare.RewardAvailable);
        Assert.Equal(now.AddHours(5), paulaStateAfterCare.CareCycleEndsAt);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task ReciprocalCare_CompletesBothWithoutDeadlock()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CROP_CARE_TEST_CONNECTION")!;
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var now = DateTime.UtcNow;
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();
        var firstFarmId = Guid.NewGuid();
        var secondFarmId = Guid.NewGuid();
        var firstPlotId = Guid.NewGuid();
        var secondPlotId = Guid.NewGuid();
        var firstOpportunityId = Guid.NewGuid();
        var secondOpportunityId = Guid.NewGuid();

        await using (var arrangeContext = new AppDbContext(dbOptions))
        {
            arrangeContext.Users.AddRange(
                CreateUser(firstUserId, "reciprocal_a"),
                CreateUser(secondUserId, "reciprocal_b"));
            arrangeContext.Farms.AddRange(
                new Farm
                {
                    Id = firstFarmId,
                    Name = "First reciprocal farm",
                    UserId = firstUserId
                },
                new Farm
                {
                    Id = secondFarmId,
                    Name = "Second reciprocal farm",
                    UserId = secondUserId
                });
            arrangeContext.Plots.AddRange(
                CreateGrowingPlot(
                    firstPlotId,
                    firstFarmId,
                    firstOpportunityId,
                    x: 0,
                    now),
                CreateGrowingPlot(
                    secondPlotId,
                    secondFarmId,
                    secondOpportunityId,
                    x: 0,
                    now));
            arrangeContext.Inventories.AddRange(
                CreateInventory(firstUserId),
                CreateInventory(secondUserId));
            arrangeContext.Friendships.Add(
                CreateAcceptedFriendship(firstUserId, secondUserId));
            await arrangeContext.SaveChangesAsync();
        }

        await using var firstContext = new AppDbContext(dbOptions);
        await using var secondContext = new AppDbContext(dbOptions);
        var firstCare = CreateService(firstContext).CareAsync(
            firstUserId,
            secondFarmId,
            secondPlotId,
            secondOpportunityId,
            Guid.NewGuid());
        var secondCare = CreateService(secondContext).CareAsync(
            secondUserId,
            firstFarmId,
            firstPlotId,
            firstOpportunityId,
            Guid.NewGuid());

        var attempts = await Task.WhenAll(firstCare, secondCare)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.All(attempts, attempt => Assert.True(attempt.Succeeded));

        await using var assertContext = new AppDbContext(dbOptions);
        var users = await assertContext.Users
            .AsNoTracking()
            .Where(user =>
                user.Id == firstUserId || user.Id == secondUserId)
            .ToListAsync();
        var inventories = await assertContext.Inventories
            .AsNoTracking()
            .Where(inventory =>
                inventory.UserId == firstUserId
                || inventory.UserId == secondUserId)
            .ToListAsync();
        var completions = await assertContext.CropCareCompletions
            .AsNoTracking()
            .Where(completion =>
                completion.CareOpportunityId == firstOpportunityId
                || completion.CareOpportunityId == secondOpportunityId)
            .ToListAsync();
        var plots = await assertContext.Plots
            .AsNoTracking()
            .Where(plot =>
                plot.Id == firstPlotId || plot.Id == secondPlotId)
            .ToListAsync();

        Assert.All(users, user => Assert.Equal(2, user.CurrentXp));
        Assert.All(inventories, inventory => Assert.Equal(102, inventory.Coins));
        Assert.Equal(2, completions.Count);
        Assert.Contains(
            completions,
            completion =>
                completion.VisitorUserId == firstUserId
                && completion.OwnerUserId == secondUserId
                && completion.FarmId == secondFarmId
                && completion.PlotId == secondPlotId);
        Assert.Contains(
            completions,
            completion =>
                completion.VisitorUserId == secondUserId
                && completion.OwnerUserId == firstUserId
                && completion.FarmId == firstFarmId
                && completion.PlotId == firstPlotId);
        Assert.Equal(
            2,
            await assertContext.VisitorFarmCareCycles.CountAsync(cycle =>
                (cycle.VisitorUserId == firstUserId
                    && cycle.FarmId == secondFarmId)
                || (cycle.VisitorUserId == secondUserId
                    && cycle.FarmId == firstFarmId)));
        Assert.All(
            plots,
            plot => Assert.NotNull(plot.CareOpportunityId));
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task CareAndSellCrop_PreserveCoinsAndBothXpAwards()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CROP_CARE_TEST_CONNECTION")!;
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var now = DateTime.UtcNow;
        var ownerId = Guid.NewGuid();
        var visitorId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var opportunityId = Guid.NewGuid();
        var inventoryId = Guid.NewGuid();

        await using (var arrangeContext = new AppDbContext(dbOptions))
        {
            var visitor = CreateUser(visitorId, "economy");
            visitor.Level = 1;
            visitor.CurrentXp = 99;

            arrangeContext.Users.AddRange(
                CreateUser(ownerId, "owner"),
                visitor);
            arrangeContext.Farms.Add(new Farm
            {
                Id = farmId,
                Name = "Economy farm",
                UserId = ownerId
            });
            arrangeContext.Plots.Add(
                CreateGrowingPlot(
                    plotId,
                    farmId,
                    opportunityId,
                    x: 0,
                    now));
            arrangeContext.Inventories.Add(new Inventory
            {
                Id = inventoryId,
                UserId = visitorId,
                Coins = 100,
                PremiumCoins = 0
            });
            arrangeContext.InventoryItems.Add(new InventoryItem
            {
                Id = Guid.NewGuid(),
                InventoryId = inventoryId,
                ItemType = ItemType.Crop,
                ItemId = "carrot_crop",
                Quantity = 1
            });
            arrangeContext.Friendships.Add(
                CreateAcceptedFriendship(ownerId, visitorId));
            await arrangeContext.SaveChangesAsync();
        }

        await using var lockContext = new AppDbContext(dbOptions);
        await using var lockTransaction =
            await lockContext.Database.BeginTransactionAsync();
        await lockContext.LockAsync(visitorId);

        await using var careContext = new AppDbContext(dbOptions);
        await using var shopContext = new AppDbContext(dbOptions);
        var careTask = CreateService(careContext).CareAsync(
            visitorId,
            farmId,
            plotId,
            opportunityId,
            Guid.NewGuid());
        var shopController = CreateShopController(shopContext, visitorId);
        var sellTask = shopController.SellCrop(
            new SellCropRequest("carrot_crop", 1));

        await Task.Delay(TimeSpan.FromMilliseconds(250));
        await lockTransaction.CommitAsync();

        await Task.WhenAll((Task)careTask, sellTask)
            .WaitAsync(TimeSpan.FromSeconds(10));
        var care = await careTask;
        var sale = await sellTask;

        Assert.True(care.Succeeded);
        Assert.IsType<OkObjectResult>(sale);

        await using var assertContext = new AppDbContext(dbOptions);
        var inventory = await assertContext.Inventories
            .AsNoTracking()
            .SingleAsync(candidate => candidate.UserId == visitorId);
        var user = await assertContext.Users
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == visitorId);
        var completion = await assertContext.CropCareCompletions
            .AsNoTracking()
            .SingleAsync(candidate =>
                candidate.VisitorUserId == visitorId
                && candidate.CareOpportunityId == opportunityId);
        var cycle = await assertContext.VisitorFarmCareCycles
            .AsNoTracking()
            .SingleAsync(candidate =>
                candidate.Id == completion.VisitorFarmCareCycleId);

        Assert.Equal(122, inventory.Coins);
        Assert.Equal(2, user.Level);
        Assert.Equal(6, user.CurrentXp);
        Assert.Equal(2, completion.CoinsGained);
        Assert.Equal(2, completion.XpGained);
        Assert.True(cycle.RewardGranted);
        Assert.False(
            await assertContext.InventoryItems.AnyAsync(item =>
                item.InventoryId == inventoryId
                && item.ItemId == "carrot_crop"));
    }

    private static async Task<CropCareAttempt> AttemptAfterGate(
        Task gate,
        CropCareService service,
        Guid visitorUserId,
        Guid farmId,
        Guid plotId,
        Guid opportunityId,
        Guid? idempotencyKey = null)
    {
        await gate;
        return await service.CareAsync(
            visitorUserId,
            farmId,
            plotId,
            opportunityId,
            idempotencyKey ?? Guid.NewGuid());
    }

    private static CropCareService CreateService(
        AppDbContext context,
        TimeProvider? timeProvider = null)
    {
        var options = Options.Create(new CropCareOptions());

        return new CropCareService(
            context,
            new ExperienceService(),
            options,
            NullLogger<CropCareService>.Instance,
            timeProvider);
    }

    private static ShopController CreateShopController(
        AppDbContext context,
        Guid userId)
    {
        return new ShopController(context, new ExperienceService())
        {
            ControllerContext = new ControllerContext
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
            }
        };
    }

    private static Plot CreateGrowingPlot(
        Guid plotId,
        Guid farmId,
        Guid opportunityId,
        int x,
        DateTime now,
        TimeSpan? growingFor = null)
    {
        return new Plot
        {
            Id = plotId,
            FarmId = farmId,
            X = x,
            Y = 0,
            Unlocked = true,
            SeedId = "corn",
            PlantedAt = now.AddMinutes(-1),
            ReadyAt = now.Add(growingFor ?? TimeSpan.FromMinutes(1)),
            RemainingYield = 3,
            CareOpportunityId = opportunityId
        };
    }

    private sealed class MutableTimeProvider(DateTime utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = new(
            DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void SetUtcNow(DateTime value)
        {
            _utcNow = new DateTimeOffset(
                DateTime.SpecifyKind(value, DateTimeKind.Utc));
        }
    }

    private static User CreateUser(Guid id, string prefix)
    {
        var uniqueName = $"{prefix}_{id:N}";

        return new User
        {
            Id = id,
            Username = uniqueName,
            NormalizedUsername = uniqueName.ToUpperInvariant(),
            PasswordHash = "integration-test"
        };
    }

    private static Inventory CreateInventory(Guid userId)
    {
        return new Inventory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Coins = 100,
            PremiumCoins = 0
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
}
