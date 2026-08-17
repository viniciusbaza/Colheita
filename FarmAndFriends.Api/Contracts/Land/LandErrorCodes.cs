namespace FarmAndFriends.Api.Contracts.Land;

public static class LandErrorCodes
{
    public const string IdempotencyKeyRequired =
        "LAND_IDEMPOTENCY_KEY_REQUIRED";
    public const string IdempotencyKeyReused =
        "LAND_IDEMPOTENCY_KEY_REUSED";
    public const string FeatureDisabled = "LAND_FEATURE_DISABLED";
    public const string PlotNotFound = "LAND_PLOT_NOT_FOUND";
    public const string StaleOffer = "LAND_STALE_OFFER";
    public const string LevelRequired = "LAND_LEVEL_REQUIRED";
    public const string InsufficientCoins = "LAND_INSUFFICIENT_COINS";
    public const string InsufficientPremiumCoins =
        "LAND_INSUFFICIENT_PREMIUM_COINS";
    public const string UnsupportedLayout = "LAND_UNSUPPORTED_LAYOUT";
    public const string InvalidPaymentCurrency =
        "LAND_INVALID_PAYMENT_CURRENCY";
    public const string InventoryNotFound = "LAND_INVENTORY_NOT_FOUND";
}
