using FarmAndFriends.Api.Domain.Entities;

namespace FarmAndFriends.Api.Domain.Rules;

public static class CropCareRules
{
    public static void StartCurrentCropCycle(Plot plot)
    {
        ClearCurrentOpportunity(plot);
        plot.CareOpportunityId = Guid.NewGuid();
    }

    public static bool IsGrowing(
        Plot plot,
        Guid opportunityId,
        DateTime now)
    {
        return plot.CareOpportunityId == opportunityId
            && plot.SeedId != null
            && plot.ReadyAt > now;
    }

    public static bool WasGrowingAt(
        Plot plot,
        Guid opportunityId,
        DateTime instant)
    {
        return plot.CareOpportunityId == opportunityId
            && plot.SeedId != null
            && plot.PlantedAt <= instant
            && plot.ReadyAt > instant;
    }

    public static void ClearCurrentOpportunity(Plot plot)
    {
        plot.CareOpportunityId = null;
    }
}
