using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Domain.Services;
using Xunit;

namespace FarmAndFriends.Api.Tests.Domain.Services;

public sealed class PremiumCurrencyReconciliationTests
{
    [Fact]
    public void Analyze_ReportsEveryTypedAccountingAndLandIssue()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var originalId = Guid.NewGuid();
        var landWithoutCompletionId = Guid.NewGuid();
        var wrongLinkedTransactionId = Guid.NewGuid();
        var missingTransactionId = Guid.NewGuid();
        var missingCompletionId = Guid.NewGuid();
        var mismatchedCompletionId = Guid.NewGuid();
        var transactions = new[]
        {
            Transaction(userId, 1, 10, 5, 15),
            Transaction(userId, 2, -3, 14, 11),
            Transaction(userId, 3, 1, 11, 12,
                reversesTransactionId: originalId),
            Transaction(userId, 4, 1, 12, 13,
                reversesTransactionId: originalId),
            Transaction(userId, 5, -2, 13, 11,
                id: landWithoutCompletionId,
                eventType: PremiumCurrencyEventType.LandPurchase,
                eventReference: "unlinked"),
            Transaction(otherUserId, 6, -4, 10, 6,
                id: wrongLinkedTransactionId,
                eventType: PremiumCurrencyEventType.AdminGrant,
                eventReference: "wrong")
        };
        var completions = new[]
        {
            new PremiumCurrencyReconciliationLandCompletion(
                missingCompletionId,
                userId,
                LandExpansionService.PremiumCoinsCurrency,
                2,
                missingTransactionId),
            new PremiumCurrencyReconciliationLandCompletion(
                mismatchedCompletionId,
                userId,
                LandExpansionService.PremiumCoinsCurrency,
                4,
                wrongLinkedTransactionId)
        };

        var report = PremiumCurrencyReconciliationService.Analyze(
            new PremiumCurrencyReconciliationSnapshot(
                userId,
                9,
                transactions,
                completions));

        Assert.False(report.IsConsistent);
        AssertIssue(report, PremiumCurrencyReconciliationIssueType.BalanceSumMismatch);
        AssertIssue(report, PremiumCurrencyReconciliationIssueType.LatestBalanceMismatch);
        AssertIssue(report, PremiumCurrencyReconciliationIssueType.FirstBalanceNotZero);
        AssertIssue(report, PremiumCurrencyReconciliationIssueType.ContinuityBroken);
        AssertIssue(report, PremiumCurrencyReconciliationIssueType.MultipleReversals);
        AssertIssue(report,
            PremiumCurrencyReconciliationIssueType.PremiumCompletionMissingTransaction);
        AssertIssue(report,
            PremiumCurrencyReconciliationIssueType.LandTransactionMissingCompletion);
        AssertIssue(report,
            PremiumCurrencyReconciliationIssueType.LandLinkSemanticMismatch);
    }

    [Fact]
    public void Analyze_AcceptsContinuousReconciledHistoryAndSemanticLandLink()
    {
        var userId = Guid.NewGuid();
        var completionId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var report = PremiumCurrencyReconciliationService.Analyze(
            new PremiumCurrencyReconciliationSnapshot(
                userId,
                8,
                [
                    Transaction(userId, 1, 10, 0, 10),
                    Transaction(
                        userId,
                        2,
                        -2,
                        10,
                        8,
                        transactionId,
                        PremiumCurrencyEventType.LandPurchase,
                        completionId.ToString("D").ToLowerInvariant())
                ],
                [
                    new PremiumCurrencyReconciliationLandCompletion(
                        completionId,
                        userId,
                        LandExpansionService.PremiumCoinsCurrency,
                        2,
                        transactionId)
                ]));

        Assert.True(report.IsConsistent);
        Assert.Empty(report.Issues);
        Assert.Equal(8, report.LedgerAmountSum);
        Assert.Equal(8, report.LatestBalanceAfter);
    }

    private static PremiumCurrencyReconciliationTransaction Transaction(
        Guid userId,
        long sequence,
        int amount,
        int before,
        int after,
        Guid? id = null,
        PremiumCurrencyEventType eventType =
            PremiumCurrencyEventType.AdminGrant,
        string eventReference = "test",
        Guid? reversesTransactionId = null) =>
        new(
            id ?? Guid.NewGuid(),
            sequence,
            userId,
            amount,
            before,
            after,
            eventType,
            eventReference,
            reversesTransactionId);

    private static void AssertIssue(
        PremiumCurrencyReconciliationReport report,
        PremiumCurrencyReconciliationIssueType issueType) =>
        Assert.Contains(report.Issues, issue => issue.Type == issueType);
}
