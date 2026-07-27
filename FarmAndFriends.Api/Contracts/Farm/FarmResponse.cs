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
    DateTime? PlantedAt,
    bool IsReady,
    DateTime? ReadyAt,
    int? RemainingYield,
    PlotCareResponse? Care
);

public record PlotCareResponse(
    Guid OpportunityId,
    string Status,
    DateTime? CaredAt,
    Guid? CaredByUserId,
    string? CaredByUsername,
    bool CanCare,
    bool RewardAvailable,
    bool ViewerCared,
    DateTime? NextCareAt,
    DateTime? CareCycleEndsAt,
    int CaregiverCount,
    IReadOnlyList<PlotCaregiverResponse> Caregivers
);

public record PlotCaregiverResponse(
    Guid UserId,
    string Username,
    DateTime CaredAt
);
