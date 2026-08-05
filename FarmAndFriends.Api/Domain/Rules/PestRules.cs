using FarmAndFriends.Api.Configuration;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;

namespace FarmAndFriends.Api.Domain.Rules;

public static class PestRules
{
    public static bool IsProtected(Plot plot, DateTime now) =>
        plot.ProtectedUntil.HasValue && plot.ProtectedUntil.Value > now;

    public static bool HasHadPestThisCycle(Plot plot) =>
        plot.PestStatus != PestStatus.None;

    public static bool IsEligible(
        Plot plot,
        int totalYield,
        DateTime now,
        PestOptions options)
    {
        return options.Enabled
            && plot.Unlocked
            && plot.SeedId != null
            && plot.ReadyAt.HasValue
            && plot.ReadyAt.Value <= now
            && now >= plot.ReadyAt.Value.AddMinutes(
                options.SafetyPeriodMinutes)
            && totalYield > options.DamageAmount
            && plot.RemainingYield.GetValueOrDefault() > 1
            && !IsProtected(plot, now)
            && !HasHadPestThisCycle(plot);
    }

    public static void Schedule(
        Plot plot,
        DateTime now,
        DateTime appearsAt)
    {
        plot.PestOccurrenceId = Guid.NewGuid();
        plot.PestType = PestType.Caterpillar;
        plot.PestStatus = PestStatus.Scheduled;
        plot.PestScheduledAt = now;
        plot.PestAppearsAt = appearsAt;
        plot.PestAppearedAt = null;
        plot.PestConsumesAt = null;
        plot.PestResolvedAt = null;
        plot.PestConsumedAmount = 0;
    }

    public static void Activate(
        Plot plot,
        DateTime now,
        PestOptions options)
    {
        if (plot.PestStatus != PestStatus.Scheduled)
            return;

        EnsureOccurrenceIdentity(plot);
        plot.PestStatus = PestStatus.Active;
        plot.PestAppearedAt = now;
        plot.PestConsumesAt = now.AddMinutes(options.ReactionWindowMinutes);
    }

    public static bool CanActivate(
        Plot plot,
        int activePestCount,
        DateTime? lastFarmInfestationAt,
        DateTime now,
        PestOptions options)
    {
        return plot.PestStatus == PestStatus.Scheduled
            && plot.PestAppearsAt.HasValue
            && plot.PestAppearsAt.Value <= now
            && activePestCount < options.MaxActivePestsPerFarm
            && (!lastFarmInfestationAt.HasValue
                || lastFarmInfestationAt.Value.AddMinutes(
                    options.MinimumInfestationIntervalMinutes) <= now);
    }

    public static int Consume(
        Plot plot,
        DateTime now,
        PestOptions options)
    {
        if (plot.PestStatus != PestStatus.Active)
            return 0;

        var availableDamage = Math.Max(
            0,
            plot.RemainingYield.GetValueOrDefault() - 1);
        var consumed = Math.Min(options.DamageAmount, availableDamage);

        if (consumed <= 0)
        {
            Resolve(plot, PestStatus.CancelledByTheft, now);
            return 0;
        }

        plot.RemainingYield -= consumed;
        plot.PestConsumedAmount = consumed;
        Resolve(plot, PestStatus.Consumed, now);
        return consumed;
    }

    public static bool CancelActiveByTheft(Plot plot, DateTime now)
    {
        if (plot.PestStatus != PestStatus.Active)
            return false;

        Resolve(plot, PestStatus.CancelledByTheft, now);
        return true;
    }

    public static bool CancelByHarvest(Plot plot, DateTime now)
    {
        if (plot.PestStatus is not (
                PestStatus.Scheduled or PestStatus.Active))
        {
            return false;
        }

        Resolve(plot, PestStatus.CancelledByHarvest, now);
        return true;
    }

    public static bool CancelByProtection(Plot plot, DateTime now)
    {
        if (plot.PestStatus is not (
                PestStatus.Scheduled or PestStatus.Active))
        {
            return false;
        }

        Resolve(plot, PestStatus.CancelledByProtection, now);
        return true;
    }

    public static void Remove(Plot plot, DateTime now)
    {
        if (plot.PestStatus != PestStatus.Active)
            throw new InvalidOperationException(
                "Não existe uma praga ativa neste lote.");

        Resolve(plot, PestStatus.Removed, now);
    }

    public static void ResetCycle(Plot plot)
    {
        plot.PestOccurrenceId = null;
        plot.PestType = null;
        plot.PestStatus = PestStatus.None;
        plot.PestScheduledAt = null;
        plot.PestAppearsAt = null;
        plot.PestAppearedAt = null;
        plot.PestConsumesAt = null;
        plot.PestResolvedAt = null;
        plot.PestConsumedAmount = 0;
    }

    public static Guid EnsureOccurrenceIdentity(Plot plot)
    {
        if (plot.PestStatus is not (
                PestStatus.Scheduled or PestStatus.Active))
        {
            throw new InvalidOperationException(
                "Somente pragas agendadas ou ativas recebem identidade.");
        }

        plot.PestOccurrenceId ??= Guid.NewGuid();
        return plot.PestOccurrenceId.Value;
    }

    private static void Resolve(
        Plot plot,
        PestStatus status,
        DateTime now)
    {
        plot.PestStatus = status;
        plot.PestResolvedAt = now;
    }
}
