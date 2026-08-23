namespace FarmAndFriends.Api.Domain.Entities;

public sealed class PremiumCurrencyPurchaseItem
{
    public Guid Id { get; set; }
    public Guid PremiumCurrencyTransactionId { get; set; }
    public PremiumCurrencyTransaction PremiumCurrencyTransaction { get; set; } =
        null!;

    // These are historical snapshots, intentionally not foreign keys to the
    // mutable shop catalog.
    public string ItemIdSnapshot { get; set; } = null!;
    public string ItemNameSnapshot { get; set; } = null!;
    public int Quantity { get; set; }
    public int UnitPremiumPrice { get; set; }
}
