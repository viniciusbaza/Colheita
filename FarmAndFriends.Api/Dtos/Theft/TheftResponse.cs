namespace FarmAndFriends.Api.Dtos.Theft;

public sealed record TheftResponse(
    Guid PlotId,
    int Stolen,
    int OwnerWillReceive,
    int XpGained,
    bool PestCancelled);
