using FarmAndFriends.Api.Contracts.Farms;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Services;

namespace FarmAndFriends.Api.Mappers;

public static class FarmMapper
{
    public static FarmResponse ToFarmResponse(
        Farm farm,
        DateTime now,
        IReadOnlyDictionary<Guid, PlotCropCareState> careStates)
    {
        return new FarmResponse(
            farm.Id,
            farm.Name,
            farm.UserId,
            farm.User.Username,
            farm.Plots
                .OrderBy(p => p.Y)
                .ThenBy(p => p.X)
                .Select(p => ToPlotResponse(
                    p,
                    now,
                    careStates.TryGetValue(p.Id, out var careState)
                        ? careState
                        : null))
                .ToList()
        );
    }

    private static PlotResponse ToPlotResponse(
        Plot p,
        DateTime now,
        PlotCropCareState? careState)
    {
        var isReady = p.ReadyAt != null && p.ReadyAt <= now;

        return new PlotResponse(
            p.Id,
            p.X,
            p.Y,
            p.Unlocked,
            p.SeedId,
            p.PlantedAt,
            isReady,
            p.ReadyAt,
            isReady ? p.RemainingYield : null,
            ToPlotCareResponse(careState)
        );
    }

    private static PlotCareResponse? ToPlotCareResponse(
        PlotCropCareState? careState)
    {
        if (careState == null)
            return null;

        var legacyCaregiver = careState.Caregivers.FirstOrDefault()
            ?? careState.ViewerCare;

        return new PlotCareResponse(
            careState.OpportunityId,
            careState.CanCare
                ? "available"
                : careState.NextCareAt.HasValue
                    ? "cooldown"
                    : "observed",
            legacyCaregiver?.CaredAt,
            legacyCaregiver?.UserId,
            legacyCaregiver?.Username,
            careState.CanCare,
            careState.RewardAvailable,
            careState.ViewerCare != null,
            careState.NextCareAt,
            careState.CareCycleEndsAt,
            careState.CaregiverCount,
            careState.Caregivers
                .Select(caregiver => new PlotCaregiverResponse(
                    caregiver.UserId,
                    caregiver.Username,
                    caregiver.CaredAt))
                .ToList());
    }
}
