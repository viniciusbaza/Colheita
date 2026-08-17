namespace FarmAndFriends.Api.Domain.Entities;

public sealed class LandPurchaseCompletion
{
    public Guid Id { get; set; }
    public Guid IdempotencyKey { get; set; }

    public Guid BuyerUserId { get; set; }
    public User BuyerUser { get; set; } = null!;

    public Guid FarmId { get; set; }
    public Farm Farm { get; set; } = null!;

    public Guid PlotId { get; set; }
    public Plot Plot { get; set; } = null!;

    public int PlotNumber { get; set; }
    public string PaymentCurrency { get; set; } = null!;
    public int AmountSpent { get; set; }
    public int CoinsAfter { get; set; }
    public int PremiumCoinsAfter { get; set; }
    public int AddedPlotCount { get; set; }
    public int? ExpandedWidth { get; set; }
    public int? ExpandedHeight { get; set; }
    public DateTime PurchasedAt { get; set; }
}
