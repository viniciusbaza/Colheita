using FarmAndFriends.Api.Configuration;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Rules;
using FarmAndFriends.Api.Domain.Services;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FarmAndFriends.Api.Tests.Integration;

public sealed class LandExpansionIntegrationTests
{
    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task Purchase_WithEitherCurrency_DebitsOnlySelectedBalance()
    {
        var coinFarm = await ArrangeAsync(
            level: 2,
            unlockedThrough: 6,
            coins: 1_000,
            premiumCoins: 10);
        var premiumFarm = await ArrangeAsync(
            level: 2,
            unlockedThrough: 6,
            coins: 1_000,
            premiumCoins: 10);

        await using (var context = new AppDbContext(DatabaseOptions()))
        {
            var attempt = await Service(context).PurchaseAsync(
                coinFarm.UserId,
                coinFarm.OfferedPlotId,
                LandPaymentCurrency.Coins,
                Guid.NewGuid());

            Assert.True(attempt.Succeeded);
            Assert.Equal(500, attempt.Response!.AmountSpent);
            Assert.Equal("coins", attempt.Response.PaymentCurrency);
        }

        await using (var context = new AppDbContext(DatabaseOptions()))
        {
            var attempt = await Service(context).PurchaseAsync(
                premiumFarm.UserId,
                premiumFarm.OfferedPlotId,
                LandPaymentCurrency.PremiumCoins,
                Guid.NewGuid());

            Assert.True(attempt.Succeeded);
            Assert.Equal(2, attempt.Response!.AmountSpent);
            Assert.Equal("premiumCoins", attempt.Response.PaymentCurrency);
        }

        await using var assertContext = new AppDbContext(DatabaseOptions());
        var coinInventory = await assertContext.Inventories
            .AsNoTracking()
            .SingleAsync(inventory => inventory.UserId == coinFarm.UserId);
        var premiumInventory = await assertContext.Inventories
            .AsNoTracking()
            .SingleAsync(inventory => inventory.UserId == premiumFarm.UserId);

        Assert.Equal((500, 10),
            (coinInventory.Coins, coinInventory.PremiumCoins));
        Assert.Equal((1_000, 8),
            (premiumInventory.Coins, premiumInventory.PremiumCoins));
        Assert.True(await assertContext.Plots
            .Where(plot =>
                plot.Id == coinFarm.OfferedPlotId
                || plot.Id == premiumFarm.OfferedPlotId)
            .AllAsync(plot => plot.Unlocked));
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task Purchase_EnforcesLevelFundsOwnershipFlagAndLayout()
    {
        var lowLevel = await ArrangeAsync(1, 6, 1_000, 10);
        var poor = await ArrangeAsync(2, 6, 499, 1);
        var disabled = await ArrangeAsync(2, 6, 1_000, 10);
        var malformed = await ArrangeAsync(2, 6, 1_000, 10);

        await using (var context = new AppDbContext(DatabaseOptions()))
        {
            Assert.Equal(
                LandPurchaseFailure.LevelRequired,
                (await Service(context).PurchaseAsync(
                    lowLevel.UserId,
                    lowLevel.OfferedPlotId,
                    LandPaymentCurrency.Coins,
                    Guid.NewGuid())).Failure);
        }

        await using (var context = new AppDbContext(DatabaseOptions()))
        {
            Assert.Equal(
                LandPurchaseFailure.InsufficientCoins,
                (await Service(context).PurchaseAsync(
                    poor.UserId,
                    poor.OfferedPlotId,
                    LandPaymentCurrency.Coins,
                    Guid.NewGuid())).Failure);
        }

        await using (var context = new AppDbContext(DatabaseOptions()))
        {
            Assert.Equal(
                LandPurchaseFailure.InsufficientPremiumCoins,
                (await Service(context).PurchaseAsync(
                    poor.UserId,
                    poor.OfferedPlotId,
                    LandPaymentCurrency.PremiumCoins,
                    Guid.NewGuid())).Failure);
        }

        await using (var context = new AppDbContext(DatabaseOptions()))
        {
            Assert.Equal(
                LandPurchaseFailure.PlotNotFound,
                (await Service(context).PurchaseAsync(
                    lowLevel.UserId,
                    poor.OfferedPlotId,
                    LandPaymentCurrency.Coins,
                    Guid.NewGuid())).Failure);
        }

        await using (var context = new AppDbContext(DatabaseOptions()))
        {
            Assert.Equal(
                LandPurchaseFailure.FeatureDisabled,
                (await Service(context, enabled: false).PurchaseAsync(
                    disabled.UserId,
                    disabled.OfferedPlotId,
                    LandPaymentCurrency.Coins,
                    Guid.NewGuid())).Failure);
        }

        await using (var context = new AppDbContext(DatabaseOptions()))
        {
            var plot = await context.Plots.SingleAsync(candidate =>
                candidate.Id == malformed.OfferedPlotId);
            plot.X = 99;
            await context.SaveChangesAsync();
        }

        await using (var context = new AppDbContext(DatabaseOptions()))
        {
            Assert.Equal(
                LandPurchaseFailure.UnsupportedLayout,
                (await Service(context).PurchaseAsync(
                    malformed.UserId,
                    malformed.OfferedPlotId,
                    LandPaymentCurrency.Coins,
                    Guid.NewGuid())).Failure);
        }

        await using var assertContext = new AppDbContext(DatabaseOptions());
        foreach (var arranged in new[] { lowLevel, poor, disabled, malformed })
        {
            Assert.False(await assertContext.LandPurchaseCompletions
                .AnyAsync(completion => completion.FarmId == arranged.FarmId));
        }
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task Retry_ReplaysOriginalCompletion_AndReuseIsRejected()
    {
        var arranged = await ArrangeAsync(2, 6, 1_000, 10);
        var key = Guid.NewGuid();
        Guid completionId;

        await using (var context = new AppDbContext(DatabaseOptions()))
        {
            var first = await Service(context).PurchaseAsync(
                arranged.UserId,
                arranged.OfferedPlotId,
                LandPaymentCurrency.Coins,
                key);
            Assert.True(first.Succeeded);
            Assert.False(first.Response!.Replayed);
            completionId = first.Response.CompletionId;
        }

        await using (var context = new AppDbContext(DatabaseOptions()))
        {
            var nextDefinition = FarmLayoutRules.GetDefinition(8);
            var otherPlotId = await context.Plots
                .Where(plot =>
                    plot.FarmId == arranged.FarmId
                    && plot.X == nextDefinition.X
                    && plot.Y == nextDefinition.Y)
                .Select(plot => plot.Id)
                .SingleAsync();
            var replay = await Service(context).PurchaseAsync(
                arranged.UserId,
                arranged.OfferedPlotId,
                LandPaymentCurrency.Coins,
                key);
            var reused = await Service(context).PurchaseAsync(
                arranged.UserId,
                arranged.OfferedPlotId,
                LandPaymentCurrency.PremiumCoins,
                key);
            var reusedTarget = await Service(context).PurchaseAsync(
                arranged.UserId,
                otherPlotId,
                LandPaymentCurrency.Coins,
                key);

            Assert.True(replay.Succeeded);
            Assert.True(replay.Response!.Replayed);
            Assert.Equal(completionId, replay.Response.CompletionId);
            Assert.Equal(
                LandPurchaseFailure.IdempotencyKeyReused,
                reused.Failure);
            Assert.Equal(
                LandPurchaseFailure.IdempotencyKeyReused,
                reusedTarget.Failure);
        }

        await using var assertContext = new AppDbContext(DatabaseOptions());
        var inventory = await assertContext.Inventories
            .AsNoTracking()
            .SingleAsync(candidate => candidate.UserId == arranged.UserId);
        Assert.Equal(500, inventory.Coins);
        Assert.Equal(10, inventory.PremiumCoins);
        Assert.Equal(1, await assertContext.LandPurchaseCompletions
            .CountAsync(completion => completion.FarmId == arranged.FarmId));
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task NinthPurchase_AddsExactlyNineteenLockedEmptyPlots()
    {
        var arranged = await ArrangeAsync(4, 8, 5_000, 10);
        IReadOnlyList<Guid> originalIds;

        await using (var context = new AppDbContext(DatabaseOptions()))
        {
            originalIds = await context.Plots
                .Where(plot => plot.FarmId == arranged.FarmId)
                .Select(plot => plot.Id)
                .ToListAsync();
            var attempt = await Service(context).PurchaseAsync(
                arranged.UserId,
                arranged.OfferedPlotId,
                LandPaymentCurrency.Coins,
                Guid.NewGuid());

            Assert.True(attempt.Succeeded);
            Assert.Equal(19, attempt.Response!.AddedPlotCount);
            Assert.Equal(7, attempt.Response.ExpandedTo!.Columns);
            Assert.Equal(4, attempt.Response.ExpandedTo.Rows);
        }

        await using var assertContext = new AppDbContext(DatabaseOptions());
        var plots = await assertContext.Plots
            .AsNoTracking()
            .Where(plot => plot.FarmId == arranged.FarmId)
            .ToListAsync();
        var added = plots.Where(plot => !originalIds.Contains(plot.Id)).ToList();

        Assert.Equal(28, plots.Count);
        Assert.Equal(19, added.Count);
        Assert.All(originalIds, id => Assert.Contains(plots, plot => plot.Id == id));
        Assert.All(added, plot =>
        {
            Assert.False(plot.Unlocked);
            Assert.Null(plot.SeedId);
            Assert.Null(plot.PlantedAt);
            Assert.Null(plot.ReadyAt);
            Assert.Null(plot.RemainingYield);
        });
        Assert.Equal(28, plots.Select(plot => (plot.X, plot.Y)).Distinct().Count());
        Assert.Equal(
            Enumerable.Range(1, FarmLayoutRules.MaxPlotCount)
                .Select(number => FarmLayoutRules.GetDefinition(number))
                .Select(definition => (definition.X, definition.Y)),
            plots.OrderBy(plot =>
                    Enumerable.Range(1, FarmLayoutRules.MaxPlotCount)
                        .Single(number =>
                        {
                            var definition = FarmLayoutRules.GetDefinition(number);
                            return definition.X == plot.X && definition.Y == plot.Y;
                        }))
                .Select(plot => (plot.X, plot.Y)));
        Assert.True(plots.Single(plot => plot.Id == arranged.OfferedPlotId).Unlocked);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task FinalPurchase_CompletesLayoutWithoutAnotherOffer()
    {
        var arranged = await ArrangeAsync(8, 27, 200_000, 20);

        await using (var context = new AppDbContext(DatabaseOptions()))
        {
            var attempt = await Service(context).PurchaseAsync(
                arranged.UserId,
                arranged.OfferedPlotId,
                LandPaymentCurrency.Coins,
                Guid.NewGuid());
            Assert.True(attempt.Succeeded);
            Assert.Equal(28, attempt.Response!.PlotNumber);
        }

        await using var assertContext = new AppDbContext(DatabaseOptions());
        var plots = await assertContext.Plots
            .AsNoTracking()
            .Where(plot => plot.FarmId == arranged.FarmId)
            .ToListAsync();
        Assert.Equal(28, plots.Count(plot => plot.Unlocked));
        Assert.Null(Service(assertContext).GetOffer(arranged.FarmId, plots));
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task ConcurrentKeys_DebitAndUnlockExactlyOnce()
    {
        var arranged = await ArrangeAsync(2, 6, 1_000, 10);
        await using var firstContext = new AppDbContext(DatabaseOptions());
        await using var secondContext = new AppDbContext(DatabaseOptions());
        var gate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var first = PurchaseAfterGate(gate.Task, Service(firstContext), arranged);
        var second = PurchaseAfterGate(gate.Task, Service(secondContext), arranged);
        gate.SetResult();
        var attempts = await Task.WhenAll(first, second)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Single(attempts, attempt => attempt.Succeeded);
        Assert.Single(attempts, attempt =>
            attempt.Failure == LandPurchaseFailure.StaleOffer);

        await using var assertContext = new AppDbContext(DatabaseOptions());
        var inventory = await assertContext.Inventories
            .AsNoTracking()
            .SingleAsync(candidate => candidate.UserId == arranged.UserId);
        Assert.Equal(500, inventory.Coins);
        Assert.Equal(1, await assertContext.LandPurchaseCompletions
            .CountAsync(completion => completion.FarmId == arranged.FarmId));
        Assert.True((await assertContext.Plots.FindAsync(
            arranged.OfferedPlotId))!.Unlocked);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task ConcurrentSameKey_ReturnsOriginalAndReplay_WithOneDebitAndCompletion()
    {
        var arranged = await ArrangeAsync(2, 6, 1_000, 10);
        var key = Guid.NewGuid();
        await using var firstContext = new AppDbContext(DatabaseOptions());
        await using var secondContext = new AppDbContext(DatabaseOptions());
        var gate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var first = PurchaseAfterGate(
            gate.Task,
            Service(firstContext),
            arranged,
            key);
        var second = PurchaseAfterGate(
            gate.Task,
            Service(secondContext),
            arranged,
            key);
        gate.SetResult();
        var attempts = await Task.WhenAll(first, second)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.All(attempts, attempt => Assert.True(attempt.Succeeded));
        var original = Assert.Single(attempts, attempt =>
            !attempt.Response!.Replayed);
        var replay = Assert.Single(attempts, attempt =>
            attempt.Response!.Replayed);
        Assert.Equal(
            original.Response!.CompletionId,
            replay.Response!.CompletionId);

        await using var assertContext = new AppDbContext(DatabaseOptions());
        var inventory = await assertContext.Inventories
            .AsNoTracking()
            .SingleAsync(candidate => candidate.UserId == arranged.UserId);
        Assert.Equal(500, inventory.Coins);
        Assert.Equal(10, inventory.PremiumCoins);
        Assert.Equal(1, await assertContext.LandPurchaseCompletions
            .CountAsync(completion => completion.FarmId == arranged.FarmId));
        Assert.True((await assertContext.Plots.FindAsync(
            arranged.OfferedPlotId))!.Unlocked);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task DatabaseConstraints_RejectDuplicateCoordinatesAndNegativeValues()
    {
        var arranged = await ArrangeAsync(2, 6, 1_000, 10);

        await using (var context = new AppDbContext(DatabaseOptions()))
        {
            context.Plots.Add(new Plot
            {
                Id = Guid.NewGuid(),
                FarmId = arranged.FarmId,
                X = 0,
                Y = 0,
                Unlocked = false
            });
            await Assert.ThrowsAsync<DbUpdateException>(
                () => context.SaveChangesAsync());
        }

        await using (var context = new AppDbContext(DatabaseOptions()))
        {
            var userId = Guid.NewGuid();
            context.Users.Add(new User
            {
                Id = userId,
                Username = $"negative_{userId:N}",
                NormalizedUsername = $"NEGATIVE_{userId:N}",
                PasswordHash = "integration-test"
            });
            context.Inventories.Add(new Inventory
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Coins = -1,
                PremiumCoins = 0
            });
            await Assert.ThrowsAsync<DbUpdateException>(
                () => context.SaveChangesAsync());
        }

        await using (var context = new AppDbContext(DatabaseOptions()))
        {
            context.LandPurchaseCompletions.Add(new LandPurchaseCompletion
            {
                Id = Guid.NewGuid(),
                IdempotencyKey = Guid.NewGuid(),
                BuyerUserId = arranged.UserId,
                FarmId = arranged.FarmId,
                PlotId = arranged.OfferedPlotId,
                PlotNumber = 7,
                PaymentCurrency = "coins",
                AmountSpent = 0,
                CoinsAfter = 1_000,
                PremiumCoinsAfter = 10,
                AddedPlotCount = 0,
                PurchasedAt = DateTime.UtcNow
            });
            await Assert.ThrowsAsync<DbUpdateException>(
                () => context.SaveChangesAsync());
        }
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task LedgerConstraints_RejectInconsistentExpansionMetadataAndDuplicatePlotNumber()
    {
        var ninth = await ArrangeAsync(4, 8, 5_000, 10);
        await using (var context = new AppDbContext(DatabaseOptions()))
        {
            context.LandPurchaseCompletions.Add(Completion(
                ninth,
                plotNumber: 9,
                addedPlotCount: 0));
            await Assert.ThrowsAsync<DbUpdateException>(
                () => context.SaveChangesAsync());
        }

        var ordinary = await ArrangeAsync(2, 6, 1_000, 10);
        await using (var context = new AppDbContext(DatabaseOptions()))
        {
            context.LandPurchaseCompletions.Add(Completion(
                ordinary,
                plotNumber: 7,
                addedPlotCount: 19,
                expandedWidth: 7,
                expandedHeight: 4));
            await Assert.ThrowsAsync<DbUpdateException>(
                () => context.SaveChangesAsync());
        }

        var duplicateNumber = await ArrangeAsync(2, 6, 1_000, 10);
        await using (var context = new AppDbContext(DatabaseOptions()))
        {
            context.LandPurchaseCompletions.Add(Completion(
                duplicateNumber,
                plotNumber: 7));
            await context.SaveChangesAsync();
        }

        await using (var context = new AppDbContext(DatabaseOptions()))
        {
            var nextDefinition = FarmLayoutRules.GetDefinition(8);
            var differentPlotId = await context.Plots
                .Where(plot =>
                    plot.FarmId == duplicateNumber.FarmId
                    && plot.X == nextDefinition.X
                    && plot.Y == nextDefinition.Y)
                .Select(plot => plot.Id)
                .SingleAsync();
            context.LandPurchaseCompletions.Add(Completion(
                duplicateNumber with { OfferedPlotId = differentPlotId },
                plotNumber: 7));
            await Assert.ThrowsAsync<DbUpdateException>(
                () => context.SaveChangesAsync());
        }
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task LedgerCompositeForeignKeys_RejectMismatchedBuyerFarmAndPlotFarm()
    {
        var first = await ArrangeAsync(2, 6, 1_000, 10);
        var second = await ArrangeAsync(2, 6, 1_000, 10);

        await using (var context = new AppDbContext(DatabaseOptions()))
        {
            context.LandPurchaseCompletions.Add(Completion(
                first with { UserId = second.UserId }));
            await Assert.ThrowsAsync<DbUpdateException>(
                () => context.SaveChangesAsync());
        }

        await using (var context = new AppDbContext(DatabaseOptions()))
        {
            context.LandPurchaseCompletions.Add(Completion(
                first with { OfferedPlotId = second.OfferedPlotId }));
            await Assert.ThrowsAsync<DbUpdateException>(
                () => context.SaveChangesAsync());
        }
    }

    private static async Task<LandPurchaseAttempt> PurchaseAfterGate(
        Task gate,
        LandExpansionService service,
        ArrangedFarm arranged,
        Guid? idempotencyKey = null)
    {
        await gate;
        return await service.PurchaseAsync(
            arranged.UserId,
            arranged.OfferedPlotId,
            LandPaymentCurrency.Coins,
            idempotencyKey ?? Guid.NewGuid());
    }

    private static LandPurchaseCompletion Completion(
        ArrangedFarm arranged,
        int plotNumber = 7,
        int addedPlotCount = 0,
        int? expandedWidth = null,
        int? expandedHeight = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = Guid.NewGuid(),
            BuyerUserId = arranged.UserId,
            FarmId = arranged.FarmId,
            PlotId = arranged.OfferedPlotId,
            PlotNumber = plotNumber,
            PaymentCurrency = "coins",
            AmountSpent = 500,
            CoinsAfter = 500,
            PremiumCoinsAfter = 10,
            AddedPlotCount = addedPlotCount,
            ExpandedWidth = expandedWidth,
            ExpandedHeight = expandedHeight,
            PurchasedAt = DateTime.UtcNow
        };

    private static async Task<ArrangedFarm> ArrangeAsync(
        int level,
        int unlockedThrough,
        int coins,
        int premiumCoins)
    {
        var userId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plots = CreateLayout(farmId, unlockedThrough);
        var evaluation = FarmLayoutRules.Evaluate(plots);
        Assert.Equal(FarmLayoutStatus.OfferAvailable, evaluation.Status);

        await using var context = new AppDbContext(DatabaseOptions());
        context.Users.Add(new User
        {
            Id = userId,
            Username = $"land_{userId:N}",
            NormalizedUsername = $"LAND_{userId:N}",
            PasswordHash = "integration-test",
            Level = level
        });
        context.Farms.Add(new Farm
        {
            Id = farmId,
            UserId = userId,
            Name = "Land expansion integration farm"
        });
        context.Plots.AddRange(plots);
        context.Inventories.Add(new Inventory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Coins = coins,
            PremiumCoins = premiumCoins
        });
        await context.SaveChangesAsync();

        return new ArrangedFarm(
            userId,
            farmId,
            evaluation.OfferedPlot!.Id);
    }

    private static List<Plot> CreateLayout(
        Guid farmId,
        int unlockedThrough)
    {
        var plots = FarmLayoutRules.CreateInitialPlots(farmId).ToList();
        if (unlockedThrough >= 9)
        {
            plots.AddRange(FarmLayoutRules.ExpansionPlots().Select(definition =>
                new Plot
                {
                    Id = Guid.NewGuid(),
                    FarmId = farmId,
                    X = definition.X,
                    Y = definition.Y
                }));
        }

        foreach (var plot in plots)
        {
            var plotNumber = Enumerable.Range(1, FarmLayoutRules.MaxPlotCount)
                .Single(number =>
                {
                    var definition = FarmLayoutRules.GetDefinition(number);
                    return definition.X == plot.X && definition.Y == plot.Y;
                });
            plot.Unlocked = plotNumber <= unlockedThrough;
        }

        return plots;
    }

    private static LandExpansionService Service(
        AppDbContext context,
        bool enabled = true) =>
        new(
            context,
            Options.Create(new LandExpansionOptions { Enabled = enabled }),
            TimeProvider.System,
            NullLogger<LandExpansionService>.Instance);

    private static DbContextOptions<AppDbContext> DatabaseOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable(
                "CROP_CARE_TEST_CONNECTION")!)
            .Options;

    private sealed record ArrangedFarm(
        Guid UserId,
        Guid FarmId,
        Guid OfferedPlotId);
}
