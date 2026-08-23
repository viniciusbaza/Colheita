using System.Data.Common;
using System.Globalization;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Domain.Services;
using FarmAndFriends.Api.Controllers;
using FarmAndFriends.Api.Infrastructure.Auth;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Xunit;

namespace FarmAndFriends.Api.Tests.Integration;

public sealed class PremiumCurrencyLedgerIntegrationTests
{
    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task Registration_CreatesNamespacedInitialGrantOnlyForPremium()
    {
        var userName = $"r_{Guid.NewGuid():N}"[..22];
        await using (var context = CreateContext())
        {
            var controller = new AuthController(
                context,
                new TokenService(Options.Create(new JwtSettings
                {
                    Key = new string('k', 64),
                    Issuer = "tests",
                    Audience = "tests",
                    ExpiresMinutes = 5
                })),
                Service(context));
            var response = await controller.Register(
                new AuthController.RegisterRequest(
                    userName,
                    "password",
                    "Ledger farm"));
            Assert.IsType<CreatedResult>(response);
        }

        await using var assertContext = CreateContext();
        var inventory = await assertContext.Inventories
            .Include(candidate => candidate.User)
            .SingleAsync(candidate => candidate.User.Username == userName);
        var ledger = await assertContext.PremiumCurrencyTransactions
            .SingleAsync(transaction => transaction.UserId == inventory.UserId);
        Assert.Equal((100, 10), (inventory.Coins, inventory.PremiumCoins));
        Assert.Equal(10, ledger.Amount);
        Assert.Equal(PremiumCurrencyEventType.AccountInitialGrant,
            ledger.EventType);
        Assert.Equal($"account-initial-grant:{inventory.UserId:D}",
            ledger.IdempotencyKey);
        var reconciliation = await new PremiumCurrencyReconciliationService(
                assertContext)
            .ReconcileAsync(inventory.UserId);
        Assert.True(reconciliation.IsConsistent);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task CreditAndDebit_PersistBalancesAndContinuousLedger()
    {
        var userId = await ArrangeWalletAsync(100);

        await using (var context = CreateContext())
        await using (var transaction =
            await context.Database.BeginTransactionAsync())
        {
            Assert.NotNull(await context.LockAsync(userId));
            var result = await Service(context).DebitAsync(
                userId,
                30,
                PremiumCurrencyEventType.LandPurchase,
                "land-completion-1",
                $"land-purchase:{Guid.NewGuid():D}");

            Assert.True(result.Succeeded);
            Assert.Equal(PremiumCurrencyOperationDisposition.Applied,
                result.Disposition);
            Assert.Equal((100, -30, 70),
                (result.Transaction!.BalanceBefore,
                    result.Transaction.Amount,
                    result.Transaction.BalanceAfter));
            await transaction.CommitAsync();
        }

        await AssertReconciledAsync(userId, expectedBalance: 70,
            expectedTransactions: 2);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task InsufficientBalance_DoesNotChangeBalanceOrLedger()
    {
        var userId = await ArrangeWalletAsync(10);

        await using (var context = CreateContext())
        await using (var transaction =
            await context.Database.BeginTransactionAsync())
        {
            Assert.NotNull(await context.LockAsync(userId));
            var result = await Service(context).DebitAsync(
                userId,
                11,
                PremiumCurrencyEventType.LandPurchase,
                "insufficient",
                $"land-purchase:{Guid.NewGuid():D}");

            Assert.Equal(PremiumCurrencyOperationFailure.InsufficientBalance,
                result.Failure);
            await transaction.CommitAsync();
        }

        await AssertReconciledAsync(userId, 10, 1);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task Idempotency_DistinguishesAppliedReplayAndConflict()
    {
        var userId = await ArrangeWalletAsync(50);
        var key = $"land-purchase:{Guid.NewGuid():D}";

        var applied = await ApplyDebitAsync(userId, 20, key, "completion-a");
        var replayed = await ApplyDebitAsync(userId, 20, key, "completion-a");
        var conflict = await ApplyDebitAsync(userId, 21, key, "completion-a");

        Assert.Equal(PremiumCurrencyOperationDisposition.Applied,
            applied.Disposition);
        Assert.Equal(PremiumCurrencyOperationDisposition.Replayed,
            replayed.Disposition);
        Assert.Equal(applied.Transaction!.Id, replayed.Transaction!.Id);
        Assert.Equal(PremiumCurrencyOperationFailure.IdempotencyConflict,
            conflict.Failure);
        await AssertReconciledAsync(userId, 30, 2);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task ExternalReference_SequentialRetryReplaysSameFingerprint()
    {
        var userId = await ArrangeWalletAsync(0);
        var externalId = $"payment-{Guid.NewGuid():N}";

        var applied = await ApplyExternalCreditAsync(userId, externalId);
        var replayed = await ApplyExternalCreditAsync(userId, externalId);

        Assert.Equal(PremiumCurrencyOperationDisposition.Applied,
            applied.Disposition);
        Assert.Equal(PremiumCurrencyOperationDisposition.Replayed,
            replayed.Disposition);
        Assert.Equal(applied.Transaction!.Id, replayed.Transaction!.Id);
        await AssertReconciledAsync(userId, 50, 1);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task EqualCreatedAt_IsOrderedDeterministicallyByLedgerSequence()
    {
        var userId = await ArrangeWalletAsync(0);
        var fixedNow = new DateTimeOffset(
            2026,
            8,
            18,
            12,
            0,
            0,
            TimeSpan.Zero);

        await ApplyFixedCreditAsync(userId, 1, fixedNow);
        await ApplyFixedCreditAsync(userId, 2, fixedNow);

        await using var context = CreateContext();
        var ledger = await context.PremiumCurrencyTransactions
            .Where(transaction => transaction.UserId == userId)
            .OrderBy(transaction => transaction.LedgerSequence)
            .ToArrayAsync();
        Assert.Equal(2, ledger.Length);
        Assert.All(ledger, transaction =>
            Assert.Equal(fixedNow.UtcDateTime, transaction.CreatedAt));
        Assert.True(ledger[0].LedgerSequence < ledger[1].LedgerSequence);
        Assert.Equal((0, 1),
            (ledger[0].BalanceBefore, ledger[0].BalanceAfter));
        Assert.Equal((1, 3),
            (ledger[1].BalanceBefore, ledger[1].BalanceAfter));
        var reconciliation = await new PremiumCurrencyReconciliationService(
                context)
            .ReconcileAsync(userId);
        Assert.True(reconciliation.IsConsistent);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task ConcurrentDebits_CannotSpendSameBalanceTwice()
    {
        var userId = await ArrangeWalletAsync(100);
        var gate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var attempts = new[]
        {
            ConcurrentDebitAsync(userId, gate.Task),
            ConcurrentDebitAsync(userId, gate.Task)
        };
        gate.SetResult();
        var results = await Task.WhenAll(attempts);

        Assert.Single(results, result => result.Succeeded);
        Assert.Single(results, result =>
            result.Failure
                == PremiumCurrencyOperationFailure.InsufficientBalance);
        await AssertReconciledAsync(userId, 20, 2);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task ConcurrentExternalReferenceCollision_IsRequeriedAfterSavepointRollback()
    {
        var firstUser = await ArrangeWalletAsync(0);
        var secondUser = await ArrangeWalletAsync(0);
        var externalId = $"payment-{Guid.NewGuid():N}";
        var gate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var attempts = new[]
        {
            ConcurrentExternalCreditAsync(firstUser, externalId, gate.Task),
            ConcurrentExternalCreditAsync(secondUser, externalId, gate.Task)
        };
        gate.SetResult();
        var results = await Task.WhenAll(attempts);

        Assert.Single(results, result =>
            result.Disposition == PremiumCurrencyOperationDisposition.Applied);
        Assert.Single(results, result =>
            result.Failure
                == PremiumCurrencyOperationFailure.ExternalTransactionConflict);
        await using var assertContext = CreateContext();
        Assert.Equal(1, await assertContext.PremiumCurrencyTransactions
            .CountAsync(transaction =>
                transaction.ExternalTransactionId == externalId));
        Assert.Equal(50, await assertContext.Inventories
            .Where(inventory =>
                inventory.UserId == firstUser
                || inventory.UserId == secondUser)
            .SumAsync(inventory => inventory.PremiumCoins));
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task Reversal_IsIntegralUniqueAndIdempotent()
    {
        var userId = await ArrangeWalletAsync(100);
        var debit = await ApplyDebitAsync(
            userId,
            25,
            $"land-purchase:{Guid.NewGuid():D}",
            "reversal-source");
        var reversalKey = $"refund:{Guid.NewGuid():D}";

        var first = await ReverseAsync(userId, debit.Transaction!.Id,
            reversalKey);
        var replay = await ReverseAsync(userId, debit.Transaction.Id,
            reversalKey);
        var duplicate = await ReverseAsync(userId, debit.Transaction.Id,
            $"refund:{Guid.NewGuid():D}");

        Assert.Equal(PremiumCurrencyOperationDisposition.Applied,
            first.Disposition);
        Assert.Equal(25, first.Transaction!.Amount);
        Assert.Equal(PremiumCurrencyOperationDisposition.Replayed,
            replay.Disposition);
        Assert.Equal(PremiumCurrencyOperationFailure.AlreadyReversed,
            duplicate.Failure);
        await AssertReconciledAsync(userId, 100, 3);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task PurchaseItems_AreImmutableHistoricalSnapshots()
    {
        var userId = await ArrangeWalletAsync(20);
        Guid transactionId;

        await using (var context = CreateContext())
        await using (var transaction =
            await context.Database.BeginTransactionAsync())
        {
            Assert.NotNull(await context.LockAsync(userId));
            var result = await Service(context).DebitAsync(
                userId,
                6,
                PremiumCurrencyEventType.PremiumItemPurchase,
                "order-1",
                $"premium-item-purchase:{Guid.NewGuid():D}",
                [new("fertilizer", "Fertilizante", 2, 3)]);
            transactionId = result.Transaction!.Id;
            await transaction.CommitAsync();
        }

        await using var assertContext = CreateContext();
        var item = await assertContext.PremiumCurrencyPurchaseItems
            .SingleAsync(candidate =>
                candidate.PremiumCurrencyTransactionId == transactionId);
        Assert.Equal(("fertilizer", "Fertilizante", 2, 3),
            (item.ItemIdSnapshot, item.ItemNameSnapshot,
                item.Quantity, item.UnitPremiumPrice));
        item.ItemNameSnapshot = "Alterado";
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => assertContext.SaveChangesAsync());
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task LedgerInsertFailure_RollsBackWalletUpdate()
    {
        var userId = await ArrangeWalletAsync(20);
        var interceptor = new FailCommandInterceptor(
            "INSERT INTO \"PremiumCurrencyTransactions\"");

        var exception = await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            await using var context = CreateContext(interceptor);
            await using var transaction =
                await context.Database.BeginTransactionAsync();
            Assert.NotNull(await context.LockAsync(userId));
            await Service(context).DebitAsync(
                userId,
                5,
                PremiumCurrencyEventType.LandPurchase,
                "failing-ledger",
                $"land-purchase:{Guid.NewGuid():D}");
        });
        Assert.IsType<InjectedCommandException>(exception.InnerException);

        await AssertReconciledAsync(userId, 20, 1);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task WalletUpdateFailure_DoesNotPersistLedger()
    {
        var userId = await ArrangeWalletAsync(20);
        var interceptor = new FailCommandInterceptor("UPDATE \"Inventories\"");

        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            await using var context = CreateContext(interceptor);
            await using var transaction =
                await context.Database.BeginTransactionAsync();
            Assert.NotNull(await context.LockAsync(userId));
            await Service(context).DebitAsync(
                userId,
                5,
                PremiumCurrencyEventType.LandPurchase,
                "failing-wallet",
                $"land-purchase:{Guid.NewGuid():D}");
        });

        await AssertReconciledAsync(userId, 20, 1);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task TransactionUpdateAndDelete_AreRejectedByApplication()
    {
        var userId = await ArrangeWalletAsync(10);
        await using var context = CreateContext();
        var ledger = await context.PremiumCurrencyTransactions
            .SingleAsync(transaction => transaction.UserId == userId);
        ledger.EventReference = "tampered";
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.SaveChangesAsync());

        context.Entry(ledger).State = EntityState.Unchanged;
        context.Remove(ledger);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.SaveChangesAsync());
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task GameplayFailureAfterDebit_RollsBackWalletLedgerAndConsequence()
    {
        var userId = await ArrangeWalletAsync(20);
        var consequenceId = Guid.NewGuid();

        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            await using var context = CreateContext();
            await using var transaction =
                await context.Database.BeginTransactionAsync();
            Assert.NotNull(await context.LockAsync(userId));
            var debit = await Service(context).DebitAsync(
                userId,
                5,
                PremiumCurrencyEventType.LandPurchase,
                consequenceId.ToString("D"),
                $"land-purchase:{Guid.NewGuid():D}");
            Assert.Equal(PremiumCurrencyOperationDisposition.Applied,
                debit.Disposition);

            // Simulates an invalid gameplay consequence after the debit was
            // flushed. The missing farm FK makes the second save fail.
            context.Plots.Add(new Plot
            {
                Id = consequenceId,
                FarmId = Guid.NewGuid(),
                X = 0,
                Y = 0,
                Unlocked = true
            });
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        });

        await AssertReconciledAsync(userId, 20, 1);
        await using var assertContext = CreateContext();
        Assert.False(await assertContext.Plots.AnyAsync(plot =>
            plot.Id == consequenceId));
    }

    [Fact]
    public void Fingerprint_IsCultureIndependentAndItemOrderCanonical()
    {
        var userId = Guid.NewGuid();
        var first = Operation(userId,
            [new("b", "B", 2, 3), new("a", "A", 1, 4)]);
        var second = Operation(userId,
            [new("a", "Renamed", 1, 4), new("b", "B", 2, 3)]);
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            var ptBr = PremiumCurrencyOperationFingerprint.Create(first);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            var arabic = PremiumCurrencyOperationFingerprint.Create(second);
            Assert.Equal(ptBr, arabic);
            Assert.StartsWith("v1:", ptBr);
            Assert.Equal(67, ptBr.Length);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    private static PremiumCurrencyOperation Operation(
        Guid userId,
        IReadOnlyCollection<PremiumCurrencyPurchaseItemSnapshot> items) =>
        new(userId, -10, PremiumCurrencyEventType.PremiumItemPurchase,
            "order", "premium-item-purchase:key", null, null, null, items);

    private static async Task<Guid> ArrangeWalletAsync(int premiumCoins)
    {
        var userId = Guid.NewGuid();
        await using var context = CreateContext();
        await using var transaction =
            await context.Database.BeginTransactionAsync();
        context.Users.Add(User(userId));
        context.Inventories.Add(new Inventory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Coins = 0,
            PremiumCoins = 0
        });
        await context.SaveChangesAsync();
        Assert.NotNull(await context.LockAsync(userId));

        if (premiumCoins > 0)
        {
            var grant = await Service(context).CreditAsync(
                userId,
                premiumCoins,
                PremiumCurrencyEventType.AdminGrant,
                $"test-grant:{userId:D}",
                $"admin-grant:{userId:D}");
            Assert.Equal(PremiumCurrencyOperationDisposition.Applied,
                grant.Disposition);
        }

        await transaction.CommitAsync();
        return userId;
    }

    private static async Task<PremiumCurrencyOperationResult> ApplyDebitAsync(
        Guid userId,
        int amount,
        string key,
        string eventReference)
    {
        await using var context = CreateContext();
        await using var transaction =
            await context.Database.BeginTransactionAsync();
        Assert.NotNull(await context.LockAsync(userId));
        var result = await Service(context).DebitAsync(
            userId,
            amount,
            PremiumCurrencyEventType.LandPurchase,
            eventReference,
            key);
        await transaction.CommitAsync();
        return result;
    }

    private static async Task<PremiumCurrencyOperationResult>
        ApplyExternalCreditAsync(Guid userId, string externalId)
    {
        await using var context = CreateContext();
        await using var transaction =
            await context.Database.BeginTransactionAsync();
        Assert.NotNull(await context.LockAsync(userId));
        var result = await Service(context).CreditAsync(
            userId,
            50,
            PremiumCurrencyEventType.PremiumCurrencyPurchase,
            externalId,
            null,
            "test-gateway:live",
            externalId);
        await transaction.CommitAsync();
        return result;
    }

    private static async Task ApplyFixedCreditAsync(
        Guid userId,
        int amount,
        DateTimeOffset fixedNow)
    {
        await using var context = CreateContext();
        await using var transaction =
            await context.Database.BeginTransactionAsync();
        Assert.NotNull(await context.LockAsync(userId));
        var result = await new PremiumCurrencyService(
                context,
                new FixedTimeProvider(fixedNow))
            .CreditAsync(
                userId,
                amount,
                PremiumCurrencyEventType.AdminGrant,
                $"fixed-grant:{Guid.NewGuid():D}",
                $"admin-grant:{Guid.NewGuid():D}");
        Assert.Equal(PremiumCurrencyOperationDisposition.Applied,
            result.Disposition);
        await transaction.CommitAsync();
    }

    private static async Task<PremiumCurrencyOperationResult> ReverseAsync(
        Guid userId,
        Guid originalId,
        string key)
    {
        await using var context = CreateContext();
        await using var transaction =
            await context.Database.BeginTransactionAsync();
        Assert.NotNull(await context.LockAsync(userId));
        var result = await Service(context).ReverseAsync(
            userId,
            originalId,
            PremiumCurrencyEventType.Refund,
            $"refund:{originalId:D}",
            key);
        await transaction.CommitAsync();
        return result;
    }

    private static async Task<PremiumCurrencyOperationResult> ConcurrentDebitAsync(
        Guid userId,
        Task gate)
    {
        await gate;
        await using var context = CreateContext();
        await using var transaction =
            await context.Database.BeginTransactionAsync();
        Assert.NotNull(await context.LockAsync(userId));
        var result = await Service(context).DebitAsync(
            userId,
            80,
            PremiumCurrencyEventType.LandPurchase,
            Guid.NewGuid().ToString("D"),
            $"land-purchase:{Guid.NewGuid():D}");
        if (result.Succeeded)
            await transaction.CommitAsync();
        return result;
    }

    private static async Task<PremiumCurrencyOperationResult>
        ConcurrentExternalCreditAsync(Guid userId, string externalId, Task gate)
    {
        await gate;
        await using var context = CreateContext();
        await using var transaction =
            await context.Database.BeginTransactionAsync();
        Assert.NotNull(await context.LockAsync(userId));
        var result = await Service(context).CreditAsync(
            userId,
            50,
            PremiumCurrencyEventType.PremiumCurrencyPurchase,
            externalId,
            null,
            "test-gateway:live",
            externalId);
        if (result.Succeeded)
            await transaction.CommitAsync();
        return result;
    }

    private static async Task AssertReconciledAsync(
        Guid userId,
        int expectedBalance,
        int expectedTransactions)
    {
        await using var context = CreateContext();
        var balance = await context.Inventories
            .Where(inventory => inventory.UserId == userId)
            .Select(inventory => inventory.PremiumCoins)
            .SingleAsync();
        var transactions = await context.PremiumCurrencyTransactions
            .AsNoTracking()
            .Where(transaction => transaction.UserId == userId)
            .OrderBy(transaction => transaction.LedgerSequence)
            .ToArrayAsync();

        Assert.Equal(expectedBalance, balance);
        Assert.Equal(expectedTransactions, transactions.Length);
        Assert.Equal(balance, transactions.Sum(transaction => transaction.Amount));
        if (transactions.Length == 0)
            return;
        Assert.Equal(0, transactions[0].BalanceBefore);
        Assert.Equal(balance, transactions[^1].BalanceAfter);
        for (var index = 1; index < transactions.Length; index++)
        {
            Assert.Equal(transactions[index - 1].BalanceAfter,
                transactions[index].BalanceBefore);
        }
    }

    private static User User(Guid userId) => new()
    {
        Id = userId,
        Username = $"premium_{userId:N}",
        NormalizedUsername = $"PREMIUM_{userId:N}",
        PasswordHash = "integration-test"
    };

    private static PremiumCurrencyService Service(AppDbContext context) =>
        new(context, TimeProvider.System);

    private static AppDbContext CreateContext(
        params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable(
                "CROP_CARE_TEST_CONNECTION")!);
        if (interceptors.Length != 0)
            builder.AddInterceptors(interceptors);
        return new AppDbContext(builder.Options);
    }

    private sealed class FailCommandInterceptor(string commandFragment)
        : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>>
            ReaderExecutingAsync(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<DbDataReader> result,
                CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains(commandFragment,
                StringComparison.Ordinal))
            {
                throw new InjectedCommandException();
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class InjectedCommandException : Exception;

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
