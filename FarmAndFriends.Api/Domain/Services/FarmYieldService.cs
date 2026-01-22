using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class FarmYieldService
{
    private readonly AppDbContext _context;

    public FarmYieldService(AppDbContext context)
    {
        _context = context;
    }

    public async Task PopulateRemainingYieldAsync(Farm farm, DateTime now)
    {
        foreach (var plot in farm.Plots)
        {
            // Não está pronto ou não tem seed → não exibe remaining
            if (plot.SeedId == null || plot.ReadyAt == null || plot.ReadyAt > now)
            {
                plot.RemainingYield = null;
                continue;
            }
            // Busca seed
            var seed = await _context.Seeds.FindAsync(plot.SeedId);
            if (seed == null)
            {
                plot.RemainingYield = null;
                continue;
            }
            // Soma roubos do plot
            var stolenAmount = await _context.TheftLogs
                .Where(t => t.PlotId == plot.Id &&
                       t.CreatedAt >= plot.PlantedAt
                )
                .SumAsync(t => t.Quantity);
            // Calcula yield restante
            plot.RemainingYield = Math.Max(1, seed.CropAmount - stolenAmount);
        }
    }
}
