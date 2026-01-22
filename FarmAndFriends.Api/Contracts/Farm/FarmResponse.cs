namespace FarmAndFriends.Api.Contracts.Farms;

public record FarmResponse(
    Guid Id,
    string Name,
    Guid OwnerUserId,
    string OwnerUsername,
    List<PlotResponse> Plots
);

public record PlotResponse(
    Guid Id,
    int X,
    int Y,
    bool Unlocked,
    string? SeedId,
    bool IsReady,
    DateTime? ReadyAt,
    int? RemainingYield
);