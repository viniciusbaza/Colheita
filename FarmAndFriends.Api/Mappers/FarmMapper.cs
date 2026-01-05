using FarmAndFriends.Api.Contracts.Farms;
using FarmAndFriends.Api.Domain.Entities;

namespace FarmAndFriends.Api.Mappers;

public static class FarmMapper
{
    public static FarmResponse ToFarmResponse(Farm farm, DateTime now)
    {
        return new FarmResponse(
            farm.Id,
            farm.Plots
                .OrderBy(p => p.Y)
                .ThenBy(p => p.X)
                .Select(p => ToPlotResponse(p, now))
                .ToList()
        );
    }

    private static PlotResponse ToPlotResponse(Plot p, DateTime now)
    {
        var isReady = p.ReadyAt != null && p.ReadyAt <= now;

        return new PlotResponse(
            p.Id,
            p.X,
            p.Y,
            p.Unlocked,
            p.SeedId,
            isReady,
            p.ReadyAt,
            isReady ? p.RemainingYield : null
        );
    }
}
