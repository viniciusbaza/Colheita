namespace FarmAndFriends.Api.Contracts.Land;

public sealed record LandOfferResponse(
    Guid PlotId,
    int PlotNumber,
    int MaxPlots,
    int MinLevel,
    LandPricesResponse Prices,
    LandDimensionsResponse? ExpandsTo);

public sealed record LandPricesResponse(
    int Coins,
    int PremiumCoins);

public sealed record LandDimensionsResponse(
    int Columns,
    int Rows);

public sealed record PurchaseLandRequest(string PaymentCurrency);

public sealed record PurchaseLandResponse(
    Guid CompletionId,
    Guid PlotId,
    int PlotNumber,
    string PaymentCurrency,
    int AmountSpent,
    int AddedPlotCount,
    LandDimensionsResponse? ExpandedTo,
    bool Replayed);
