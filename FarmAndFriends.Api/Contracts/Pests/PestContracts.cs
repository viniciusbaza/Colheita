namespace FarmAndFriends.Api.Contracts.Pests;

public sealed record RemovePestRequest(Guid PestOccurrenceId);

public sealed record PestActionResponse(
    Guid PlotId,
    string Status,
    int? RemainingYield,
    PestStateResponse? Pest,
    Guid CompletionId,
    Guid PestOccurrenceId,
    int CoinsGained,
    int XpGained,
    int Coins,
    bool RewardGranted,
    bool Replayed);

public sealed record ApplyPestProtectionResponse(
    Guid PlotId,
    string Status,
    int? RemainingYield,
    PestStateResponse? Pest,
    string ItemId,
    int RemainingItemQuantity,
    DateTime ProtectedUntil);

public sealed record PestStateResponse(
    string Type,
    string Status,
    DateTime? ScheduledAt,
    DateTime? AppearsAt,
    DateTime? AppearedAt,
    DateTime? ConsumesAt,
    DateTime? ResolvedAt,
    int ConsumedAmount,
    bool CanRemove,
    Guid? OccurrenceId);
