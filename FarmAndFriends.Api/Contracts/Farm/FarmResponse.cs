using FarmAndFriends.Api.Contracts.Pests;
using FarmAndFriends.Api.Contracts.Land;

namespace FarmAndFriends.Api.Contracts.Farms;

public record FarmResponse(
    Guid Id,
    string Name,
    Guid OwnerUserId,
    string OwnerUsername,
    List<PlotResponse> Plots,
    DateTime? NextPestCheckAt,
    LandOfferResponse? LandOffer
);

public record PlotResponse(
    Guid Id,
    int X,
    int Y,
    bool Unlocked,
    string? SeedId,
    DateTime? PlantedAt,
    int? CurrentHarvestCycle,
    bool IsReady,
    DateTime? ReadyAt,
    int? RemainingYield,
    DateTime? ProtectedUntil,
    PestStateResponse? Pest,
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
