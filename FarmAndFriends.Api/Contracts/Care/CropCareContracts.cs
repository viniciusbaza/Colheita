namespace FarmAndFriends.Api.Contracts.Care;

public record CareForPlotRequest(Guid CareOpportunityId);

public record CareForPlotResponse(
    Guid CompletionId,
    Guid PlotId,
    Guid CareOpportunityId,
    DateTime CaredAt,
    DateTime NextCareAt,
    Guid CaredByUserId,
    string CaredByUsername,
    int CoinsGained,
    int XpGained,
    int Coins,
    Guid CareCycleId,
    DateTime CareCycleEndsAt,
    bool CycleRewardGranted);
