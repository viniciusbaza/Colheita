using FarmAndFriends.Api.Contracts.Farms;
using FarmAndFriends.Api.Contracts.Pests;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Rules;
using FarmAndFriends.Api.Domain.Services;

namespace FarmAndFriends.Api.Mappers;

public static class FarmMapper
{
    public static FarmResponse ToFarmResponse(
        Farm farm,
        DateTime now,
        IReadOnlyDictionary<Guid, PlotCropCareState> careStates,
        DateTime? nextPestCheckAt = null)
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
                .ToList(),
            nextPestCheckAt
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
            PestRules.IsProtected(p, now) ? p.ProtectedUntil : null,
            ToPlotPestResponse(p),
            ToPlotCareResponse(careState)
        );
    }

    private static PestStateResponse? ToPlotPestResponse(Plot plot)
    {
        if (plot.SeedId == null
            || plot.PestStatus == Domain.Enums.PestStatus.None
            || !plot.PestType.HasValue)
        {
            return null;
        }

        return PestService.ToContractPest(plot);
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
