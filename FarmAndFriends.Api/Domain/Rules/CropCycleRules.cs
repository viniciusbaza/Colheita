using FarmAndFriends.Api.Domain.Entities;

namespace FarmAndFriends.Api.Domain.Rules;

public sealed record CropCycleTransition(
    bool HasNextCycle,
    int? CurrentHarvestCycle,
    DateTime? ReadyAt);

public static class CropCycleRules
{
    public static void ValidateSeedConfiguration(Seed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);

        if (string.IsNullOrWhiteSpace(seed.Id)
            || string.IsNullOrWhiteSpace(seed.Name)
            || string.IsNullOrWhiteSpace(seed.CropId)
            || string.IsNullOrWhiteSpace(seed.CropName))
        {
            throw new InvalidOperationException(
                "A configuração da cultura possui identificadores ou nomes inválidos.");
        }

        if (seed.GrowTime <= TimeSpan.Zero)
            throw new InvalidOperationException("GrowTime deve ser positivo.");

        if (seed.CropAmount < 1)
            throw new InvalidOperationException("CropAmount deve ser positivo.");

        if (seed.HarvestCycles < 1)
            throw new InvalidOperationException("HarvestCycles deve ser positivo.");

        if (seed.HarvestCycles == 1 && seed.RegrowTime != null)
        {
            throw new InvalidOperationException(
                "Culturas de ciclo único não devem possuir RegrowTime.");
        }

        if (seed.HarvestCycles > 1
            && (!seed.RegrowTime.HasValue
                || seed.RegrowTime.Value <= TimeSpan.Zero))
        {
            throw new InvalidOperationException(
                "Culturas de múltiplas colheitas exigem RegrowTime positivo.");
        }
    }

    public static void StartPlanting(Plot plot, Seed seed, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(plot);
        ValidateSeedConfiguration(seed);

        if (plot.SeedId != null)
            throw new InvalidOperationException("O lote já está ocupado.");

        plot.SeedId = seed.Id;
        plot.PlantedAt = now;
        plot.CurrentHarvestCycle = 1;
        plot.CurrentHarvestCycleStartedAt = now;
        plot.ReadyAt = now.Add(seed.GrowTime);
        plot.RemainingYield = seed.CropAmount;
    }

    public static void ValidateCurrentCycle(Plot plot, Seed seed)
    {
        ArgumentNullException.ThrowIfNull(plot);
        ValidateSeedConfiguration(seed);

        if (!string.Equals(plot.SeedId, seed.Id, StringComparison.Ordinal)
            || !plot.CurrentHarvestCycle.HasValue
            || plot.CurrentHarvestCycle.Value < 1
            || plot.CurrentHarvestCycle.Value > seed.HarvestCycles
            || !plot.CurrentHarvestCycleStartedAt.HasValue
            || !plot.ReadyAt.HasValue
            || plot.ReadyAt.Value <= plot.CurrentHarvestCycleStartedAt.Value)
        {
            throw new InvalidOperationException(
                "O estado persistido do ciclo produtivo está inválido.");
        }
    }

    public static CropCycleTransition CompleteHarvest(
        Plot plot,
        Seed seed,
        DateTime now)
    {
        ValidateCurrentCycle(plot, seed);

        if (plot.CurrentHarvestCycle!.Value < seed.HarvestCycles)
        {
            var nextCycle = checked(plot.CurrentHarvestCycle.Value + 1);
            var regrowTime = seed.RegrowTime!.Value;

            plot.CurrentHarvestCycle = nextCycle;
            plot.CurrentHarvestCycleStartedAt = now;
            plot.ReadyAt = now.Add(regrowTime);
            plot.RemainingYield = seed.CropAmount;

            return new CropCycleTransition(
                HasNextCycle: true,
                nextCycle,
                plot.ReadyAt);
        }

        plot.SeedId = null;
        plot.PlantedAt = null;
        plot.CurrentHarvestCycle = null;
        plot.CurrentHarvestCycleStartedAt = null;
        plot.ReadyAt = null;
        plot.RemainingYield = null;

        return new CropCycleTransition(
            HasNextCycle: false,
            CurrentHarvestCycle: null,
            ReadyAt: null);
    }
}
