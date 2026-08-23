namespace FarmAndFriends.Api.Domain.Enums;

public enum PremiumCurrencyEventType
{
    AccountInitialGrant,
    LandPurchase,
    PremiumCurrencyPurchase,
    PremiumItemPurchase,
    Refund,
    AdminGrant,
    Compensation
}

public static class PremiumCurrencyEventTokens
{
    public static string ToToken(this PremiumCurrencyEventType eventType) =>
        eventType switch
        {
            PremiumCurrencyEventType.AccountInitialGrant =>
                "account_initial_grant",
            PremiumCurrencyEventType.LandPurchase => "land_purchase",
            PremiumCurrencyEventType.PremiumCurrencyPurchase =>
                "premium_currency_purchase",
            PremiumCurrencyEventType.PremiumItemPurchase =>
                "premium_item_purchase",
            PremiumCurrencyEventType.Refund => "refund",
            PremiumCurrencyEventType.AdminGrant => "admin_grant",
            PremiumCurrencyEventType.Compensation => "compensation",
            _ => throw new ArgumentOutOfRangeException(nameof(eventType))
        };

    public static PremiumCurrencyEventType Parse(string token) =>
        token switch
        {
            "account_initial_grant" =>
                PremiumCurrencyEventType.AccountInitialGrant,
            "land_purchase" => PremiumCurrencyEventType.LandPurchase,
            "premium_currency_purchase" =>
                PremiumCurrencyEventType.PremiumCurrencyPurchase,
            "premium_item_purchase" =>
                PremiumCurrencyEventType.PremiumItemPurchase,
            "refund" => PremiumCurrencyEventType.Refund,
            "admin_grant" => PremiumCurrencyEventType.AdminGrant,
            "compensation" => PremiumCurrencyEventType.Compensation,
            _ => throw new InvalidOperationException(
                $"Unknown premium currency event token '{token}'.")
        };
}
