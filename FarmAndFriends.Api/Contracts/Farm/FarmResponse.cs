namespace FarmAndFriends.Api.Contracts.Farms;

public record FarmResponse(
    Guid Id,
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