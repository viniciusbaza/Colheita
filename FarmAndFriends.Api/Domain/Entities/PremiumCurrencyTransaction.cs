using FarmAndFriends.Api.Domain.Enums;

namespace FarmAndFriends.Api.Domain.Entities;

public sealed class PremiumCurrencyTransaction
{
    public Guid Id { get; set; }
    public long LedgerSequence { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public int Amount { get; set; }
    public int BalanceBefore { get; set; }
    public int BalanceAfter { get; set; }
    public PremiumCurrencyEventType EventType { get; set; }
    public string EventReference { get; set; } = null!;
    public string? IdempotencyKey { get; set; }
    public string OperationFingerprint { get; set; } = null!;
    public string? ExternalSource { get; set; }
    public string? ExternalTransactionId { get; set; }

    public Guid? ReversesTransactionId { get; set; }
    public PremiumCurrencyTransaction? ReversesTransaction { get; set; }

    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<PremiumCurrencyPurchaseItem> PurchaseItems { get; set; } =
        new List<PremiumCurrencyPurchaseItem>();
}
