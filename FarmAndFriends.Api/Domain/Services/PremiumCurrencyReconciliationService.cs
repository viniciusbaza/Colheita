using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FarmAndFriends.Api.Domain.Services;

public sealed class PremiumCurrencyReconciliationService
{
    private readonly AppDbContext _context;

    public PremiumCurrencyReconciliationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PremiumCurrencyReconciliationReport> ReconcileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var balance = await _context.Inventories
            .AsNoTracking()
            .Where(inventory => inventory.UserId == userId)
            .Select(inventory => (int?)inventory.PremiumCoins)
            .SingleOrDefaultAsync(cancellationToken);

        var completions = await _context.LandPurchaseCompletions
            .AsNoTracking()
            .Where(completion => completion.BuyerUserId == userId)
            .Select(completion => new PremiumCurrencyReconciliationLandCompletion(
                completion.Id,
                completion.BuyerUserId,
                completion.PaymentCurrency,
                completion.AmountSpent,
                completion.PremiumCurrencyTransactionId))
            .ToArrayAsync(cancellationToken);

        var linkedTransactionIds = completions
            .Where(completion => completion.PremiumCurrencyTransactionId.HasValue)
            .Select(completion => completion.PremiumCurrencyTransactionId!.Value)
            .ToArray();
        var transactions = await _context.PremiumCurrencyTransactions
            .AsNoTracking()
            .Where(transaction => transaction.UserId == userId
                || linkedTransactionIds.Contains(transaction.Id))
            .Select(transaction => new PremiumCurrencyReconciliationTransaction(
                transaction.Id,
                transaction.LedgerSequence,
                transaction.UserId,
                transaction.Amount,
                transaction.BalanceBefore,
                transaction.BalanceAfter,
                transaction.EventType,
                transaction.EventReference,
                transaction.ReversesTransactionId))
            .ToArrayAsync(cancellationToken);

        return Analyze(new PremiumCurrencyReconciliationSnapshot(
            userId,
            balance,
            transactions,
            completions));
    }

    public static PremiumCurrencyReconciliationReport Analyze(
        PremiumCurrencyReconciliationSnapshot snapshot)
    {
        var issues = new List<PremiumCurrencyReconciliationIssue>();
        if (!snapshot.InventoryPremiumCoins.HasValue)
        {
            issues.Add(new(PremiumCurrencyReconciliationIssueType.InventoryMissing));
        }

        var ordered = snapshot.Transactions
            .Where(transaction => transaction.UserId == snapshot.UserId)
            .OrderBy(transaction => transaction.LedgerSequence)
            .ToArray();
        var ledgerSum = ordered.Sum(transaction => (long)transaction.Amount);
        if (snapshot.InventoryPremiumCoins.HasValue
            && ledgerSum != snapshot.InventoryPremiumCoins.Value)
        {
            issues.Add(new(
                PremiumCurrencyReconciliationIssueType.BalanceSumMismatch,
                ExpectedValue: snapshot.InventoryPremiumCoins.Value,
                ActualValue: ledgerSum));
        }

        if (ordered.Length != 0)
        {
            if (ordered[0].BalanceBefore != 0)
            {
                issues.Add(new(
                    PremiumCurrencyReconciliationIssueType.FirstBalanceNotZero,
                    TransactionId: ordered[0].Id,
                    ExpectedValue: 0,
                    ActualValue: ordered[0].BalanceBefore));
            }

            if (snapshot.InventoryPremiumCoins.HasValue
                && ordered[^1].BalanceAfter
                    != snapshot.InventoryPremiumCoins.Value)
            {
                issues.Add(new(
                    PremiumCurrencyReconciliationIssueType.LatestBalanceMismatch,
                    TransactionId: ordered[^1].Id,
                    ExpectedValue: snapshot.InventoryPremiumCoins.Value,
                    ActualValue: ordered[^1].BalanceAfter));
            }

            for (var index = 1; index < ordered.Length; index++)
            {
                if (ordered[index].BalanceBefore
                    == ordered[index - 1].BalanceAfter)
                {
                    continue;
                }

                issues.Add(new(
                    PremiumCurrencyReconciliationIssueType.ContinuityBroken,
                    TransactionId: ordered[index].Id,
                    RelatedTransactionId: ordered[index - 1].Id,
                    ExpectedValue: ordered[index - 1].BalanceAfter,
                    ActualValue: ordered[index].BalanceBefore));
            }
        }

        foreach (var reversalGroup in ordered
            .Where(transaction => transaction.ReversesTransactionId.HasValue)
            .GroupBy(transaction => transaction.ReversesTransactionId!.Value)
            .Where(group => group.Count() > 1))
        {
            issues.Add(new(
                PremiumCurrencyReconciliationIssueType.MultipleReversals,
                RelatedTransactionId: reversalGroup.Key));
        }

        var transactionsById = snapshot.Transactions
            .ToDictionary(transaction => transaction.Id);
        var completionsByTransaction = snapshot.LandCompletions
            .Where(completion => completion.PremiumCurrencyTransactionId.HasValue)
            .GroupBy(completion => completion.PremiumCurrencyTransactionId!.Value)
            .ToDictionary(group => group.Key, group => group.ToArray());

        foreach (var completion in snapshot.LandCompletions.Where(completion =>
            string.Equals(
                completion.PaymentCurrency,
                LandExpansionService.PremiumCoinsCurrency,
                StringComparison.Ordinal)))
        {
            if (!completion.PremiumCurrencyTransactionId.HasValue
                || !transactionsById.TryGetValue(
                    completion.PremiumCurrencyTransactionId.Value,
                    out var transaction))
            {
                issues.Add(new(
                    PremiumCurrencyReconciliationIssueType.PremiumCompletionMissingTransaction,
                    CompletionId: completion.Id,
                    TransactionId: completion.PremiumCurrencyTransactionId));
                continue;
            }

            var expectedReference = completion.Id.ToString("D").ToLowerInvariant();
            if (transaction.UserId != completion.BuyerUserId
                || transaction.EventType != PremiumCurrencyEventType.LandPurchase
                || !string.Equals(transaction.EventReference,
                    expectedReference, StringComparison.Ordinal)
                || transaction.Amount != -(long)completion.AmountSpent)
            {
                issues.Add(new(
                    PremiumCurrencyReconciliationIssueType.LandLinkSemanticMismatch,
                    TransactionId: transaction.Id,
                    CompletionId: completion.Id));
            }
        }

        foreach (var transaction in ordered.Where(transaction =>
            transaction.EventType == PremiumCurrencyEventType.LandPurchase))
        {
            if (!completionsByTransaction.TryGetValue(
                    transaction.Id,
                    out var linkedCompletions)
                || linkedCompletions.Length == 0)
            {
                issues.Add(new(
                    PremiumCurrencyReconciliationIssueType.LandTransactionMissingCompletion,
                    TransactionId: transaction.Id));
            }
        }

        return new PremiumCurrencyReconciliationReport(
            snapshot.UserId,
            snapshot.InventoryPremiumCoins,
            ledgerSum,
            ordered.LastOrDefault()?.BalanceAfter,
            issues);
    }
}

public sealed record PremiumCurrencyReconciliationSnapshot(
    Guid UserId,
    int? InventoryPremiumCoins,
    IReadOnlyCollection<PremiumCurrencyReconciliationTransaction> Transactions,
    IReadOnlyCollection<PremiumCurrencyReconciliationLandCompletion>
        LandCompletions);

public sealed record PremiumCurrencyReconciliationTransaction(
    Guid Id,
    long LedgerSequence,
    Guid UserId,
    int Amount,
    int BalanceBefore,
    int BalanceAfter,
    PremiumCurrencyEventType EventType,
    string EventReference,
    Guid? ReversesTransactionId);

public sealed record PremiumCurrencyReconciliationLandCompletion(
    Guid Id,
    Guid BuyerUserId,
    string PaymentCurrency,
    int AmountSpent,
    Guid? PremiumCurrencyTransactionId);

public sealed record PremiumCurrencyReconciliationReport(
    Guid UserId,
    int? InventoryPremiumCoins,
    long LedgerAmountSum,
    int? LatestBalanceAfter,
    IReadOnlyList<PremiumCurrencyReconciliationIssue> Issues)
{
    public bool IsConsistent => Issues.Count == 0;
}

public sealed record PremiumCurrencyReconciliationIssue(
    PremiumCurrencyReconciliationIssueType Type,
    Guid? TransactionId = null,
    Guid? RelatedTransactionId = null,
    Guid? CompletionId = null,
    long? ExpectedValue = null,
    long? ActualValue = null);

public enum PremiumCurrencyReconciliationIssueType
{
    InventoryMissing,
    BalanceSumMismatch,
    LatestBalanceMismatch,
    FirstBalanceNotZero,
    ContinuityBroken,
    MultipleReversals,
    PremiumCompletionMissingTransaction,
    LandTransactionMissingCompletion,
    LandLinkSemanticMismatch
}
