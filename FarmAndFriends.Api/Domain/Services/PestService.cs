using FarmAndFriends.Api.Configuration;
using FarmAndFriends.Api.Contracts.Pests;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Domain.Rules;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FarmAndFriends.Api.Domain.Services;

public enum PestActionFailure
{
    None,
    FarmNotFound,
    PlotNotFound,
    NotFriends,
    PestNotActive,
    AlreadyProtected,
    InventoryNotFound,
    ItemUnavailable,
    FeatureDisabled,
    IdempotencyKeyReused,
    PestOccurrenceMismatch
}

public sealed record PestActionAttempt(
    PestActionFailure Failure,
    PestActionResponse? Response = null)
{
    public bool Succeeded =>
        Failure == PestActionFailure.None && Response != null;
}

public sealed record PestProtectionAttempt(
    PestActionFailure Failure,
    ApplyPestProtectionResponse? Response = null)
{
    public bool Succeeded =>
        Failure == PestActionFailure.None && Response != null;
}

public sealed class PestService
{
    private readonly AppDbContext _context;
    private readonly ExperienceService _experienceService;
    private readonly PestOptions _options;
    private readonly TimeProvider _timeProvider;

    public PestService(
        AppDbContext context,
        ExperienceService experienceService,
        IOptions<PestOptions> options,
        TimeProvider timeProvider)
    {
        _context = context;
        _experienceService = experienceService;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public DateTime UtcNow =>
        _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<bool> ProcessFarmAsync(
        Guid farmId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);
        var farm = await _context.LockFarmAsync(farmId, cancellationToken);

        if (farm == null)
            return false;

        var plots = await _context.LockFarmPlotsAsync(
            farmId,
            cancellationToken);
        await ProcessLockedFarmAsync(
            farm,
            plots,
            UtcNow,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task ProcessLockedFarmAsync(
        Farm farm,
        IReadOnlyList<Plot> plots,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        foreach (var plot in plots.Where(plot =>
                     plot.PestOccurrenceId == null
                     && plot.PestStatus is (
                         PestStatus.Scheduled or PestStatus.Active)))
        {
            PestRules.EnsureOccurrenceIdentity(plot);
        }

        if (!_options.Enabled)
            return;

        var seedIds = plots
            .Where(plot => plot.SeedId != null)
            .Select(plot => plot.SeedId!)
            .Distinct()
            .ToArray();
        var seeds = await _context.Seeds
            .Where(seed => seedIds.Contains(seed.Id))
            .ToDictionaryAsync(seed => seed.Id, cancellationToken);

        foreach (var plot in plots.OrderBy(plot => plot.Id))
        {
            if (PestRules.IsProtected(plot, now))
                PestRules.CancelByProtection(plot, now);

            if (plot.PestStatus == PestStatus.Active
                && plot.PestConsumesAt.HasValue
                && plot.PestConsumesAt.Value <= now)
            {
                var consumed = PestRules.Consume(plot, now, _options);

                if (consumed > 0)
                {
                    var cropName = plot.SeedId != null
                        && seeds.TryGetValue(plot.SeedId, out var seed)
                            ? seed.Name.ToLowerInvariant()
                            : "cultura";
                    _context.Notifications.Add(new Notification
                    {
                        RecipientUserId = farm.UserId,
                        Type = NotificationType.PestConsumed,
                        PestPlotId = plot.Id,
                        Message =
                            $"Uma lagarta comeu {consumed} {cropName} da sua produção.",
                        CreatedAt = now
                    });
                }
            }
        }

        foreach (var plot in plots.OrderBy(plot => plot.Id))
        {
            if (plot.SeedId == null
                || !seeds.TryGetValue(plot.SeedId, out var seed)
                || !PestRules.IsEligible(
                    plot,
                    seed.CropAmount,
                    now,
                    _options))
            {
                continue;
            }

            var intervalAllowsAt = farm.LastPestInfestationAt?.AddMinutes(
                _options.MinimumInfestationIntervalMinutes);
            var appearsAt = intervalAllowsAt.HasValue
                && intervalAllowsAt.Value > now
                    ? intervalAllowsAt.Value
                    : now;
            PestRules.Schedule(plot, now, appearsAt);
        }

        var activeCount = plots.Count(
            plot => plot.PestStatus == PestStatus.Active);

        foreach (var plot in plots
                     .Where(plot =>
                         plot.PestStatus == PestStatus.Scheduled)
                     .OrderBy(plot => plot.PestAppearsAt)
                     .ThenBy(plot => plot.Id))
        {
            if (PestRules.IsProtected(plot, now))
            {
                PestRules.CancelByProtection(plot, now);
                continue;
            }

            if (plot.RemainingYield.GetValueOrDefault() <= 1)
            {
                ResolveScheduledAfterTheft(plot, now);
                continue;
            }

            if (!PestRules.CanActivate(
                    plot,
                    activeCount,
                    farm.LastPestInfestationAt,
                    now,
                    _options))
            {
                var nextFarmInfestationAt =
                    farm.LastPestInfestationAt?.AddMinutes(
                        _options.MinimumInfestationIntervalMinutes);
                if (nextFarmInfestationAt.HasValue
                    && nextFarmInfestationAt.Value > now)
                {
                    plot.PestAppearsAt = nextFarmInfestationAt;
                }

                continue;
            }

            PestRules.Activate(plot, now, _options);
            farm.LastPestInfestationAt = now;
            activeCount++;
        }
    }

    public async Task<PestActionAttempt> RemoveAsync(
        Guid actorUserId,
        Guid farmId,
        Guid plotId,
        Guid expectedOccurrenceId,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);
        var actor = await _context.LockAsync(
            actorUserId,
            cancellationToken);

        if (actor == null)
            return new(PestActionFailure.NotFriends);

        var farm = await _context.LockFarmAsync(farmId, cancellationToken);

        if (farm == null)
            return new(PestActionFailure.FarmNotFound);

        if (!await CanInteractAsync(
                actorUserId,
                farm.UserId,
                cancellationToken))
        {
            return new(PestActionFailure.NotFriends);
        }

        var plots = await _context.LockFarmPlotsAsync(
            farmId,
            cancellationToken);
        var plot = plots.SingleOrDefault(candidate => candidate.Id == plotId);

        if (plot == null)
            return new(PestActionFailure.PlotNotFound);

        var persistedAttempt = await FindPersistedRemovalAsync(
            actorUserId,
            farmId,
            plotId,
            expectedOccurrenceId,
            idempotencyKey,
            plot,
            cancellationToken);

        if (persistedAttempt != null)
        {
            if (persistedAttempt.Succeeded)
            {
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            return persistedAttempt;
        }

        if (plot.PestOccurrenceId != expectedOccurrenceId)
            return new(PestActionFailure.PestOccurrenceMismatch);

        var now = UtcNow;
        await ProcessLockedFarmAsync(
            farm,
            plots,
            now,
            cancellationToken);

        if (plot.PestStatus != PestStatus.Active)
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(PestActionFailure.PestNotActive);
        }

        var pestOccurrenceId =
            PestRules.EnsureOccurrenceIdentity(plot);
        var inventory = await _context.Inventories
            .FromSqlInterpolated(
                $"""
                SELECT * FROM "Inventories"
                WHERE "UserId" = {actorUserId}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);

        if (inventory == null)
            return new(PestActionFailure.InventoryNotFound);

        var rewardWindowStart = now.AddHours(
            -_options.RemovalRewardRollingWindowHours);
        var rewardedRemovalCount =
            await _context.PestRemovalCompletions
                .AsNoTracking()
                .CountAsync(completion =>
                        completion.ActorUserId == actorUserId
                        && (completion.CoinsGained > 0
                            || completion.XpGained > 0)
                        && completion.RemovedAt >= rewardWindowStart,
                    cancellationToken);
        var rewardGranted = _options.Enabled
            && (_options.RemovalCoinsReward > 0
                || _options.RemovalXpReward > 0)
            && rewardedRemovalCount
                < _options.MaxRewardedRemovalsPerWindow;
        var coinsGained = rewardGranted
            ? _options.RemovalCoinsReward
            : 0;
        var xpGained = rewardGranted
            ? _options.RemovalXpReward
            : 0;

        if (coinsGained > 0)
            inventory.Coins = checked(inventory.Coins + coinsGained);
        if (xpGained > 0)
            _experienceService.AddXp(actor, xpGained);

        PestRules.Remove(plot, now);
        var completion = new PestRemovalCompletion
        {
            Id = Guid.NewGuid(),
            PestOccurrenceId = pestOccurrenceId,
            IdempotencyKey = idempotencyKey,
            ActorUserId = actorUserId,
            OwnerUserId = farm.UserId,
            FarmId = farmId,
            PlotId = plotId,
            RemovedAt = now,
            RemainingYieldAfter = plot.RemainingYield,
            CoinsGained = coinsGained,
            XpGained = xpGained,
            CoinsAfter = inventory.Coins
        };
        _context.PestRemovalCompletions.Add(completion);

        if (actorUserId != farm.UserId)
        {
            _context.Notifications.Add(new Notification
            {
                RecipientUserId = farm.UserId,
                ActorUserId = actorUserId,
                Type = NotificationType.PestRemoved,
                PestPlotId = plot.Id,
                Message =
                    $"{actor.Username} removeu uma lagarta da sua plantação.",
                CreatedAt = now
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(
            PestActionFailure.None,
            ToRemovalResponse(completion, plot, replayed: false));
    }

    public async Task<PestProtectionAttempt> ApplyProtectionAsync(
        Guid actorUserId,
        Guid farmId,
        Guid plotId,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return new(PestActionFailure.FeatureDisabled);

        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);
        var actor = await _context.LockAsync(
            actorUserId,
            cancellationToken);

        if (actor == null)
            return new(PestActionFailure.NotFriends);

        var farm = await _context.LockFarmAsync(farmId, cancellationToken);

        if (farm == null)
            return new(PestActionFailure.FarmNotFound);

        if (!await CanInteractAsync(
                actorUserId,
                farm.UserId,
                cancellationToken))
        {
            return new(PestActionFailure.NotFriends);
        }

        var plots = await _context.LockFarmPlotsAsync(
            farmId,
            cancellationToken);
        var plot = plots.SingleOrDefault(candidate => candidate.Id == plotId);

        if (plot == null || !plot.Unlocked)
            return new(PestActionFailure.PlotNotFound);

        var now = UtcNow;
        await ProcessLockedFarmAsync(
            farm,
            plots,
            now,
            cancellationToken);

        if (PestRules.IsProtected(plot, now))
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(PestActionFailure.AlreadyProtected);
        }

        var inventory = await _context.Inventories
            .SingleOrDefaultAsync(
                candidate => candidate.UserId == actorUserId,
                cancellationToken);

        if (inventory == null)
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(PestActionFailure.InventoryNotFound);
        }

        var item = await _context.InventoryItems
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.InventoryId == inventory.Id
                    && candidate.ItemType == ItemType.Item
                    && candidate.ItemId
                        == PestOptions.NaturalRepellentItemId,
                cancellationToken);

        if (item == null || item.Quantity <= 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(PestActionFailure.ItemUnavailable);
        }

        item.Quantity--;
        plot.ProtectedUntil = now.AddHours(
            _options.ProtectionDurationHours);
        PestRules.CancelByProtection(plot, now);

        if (item.Quantity == 0)
            _context.InventoryItems.Remove(item);

        if (actorUserId != farm.UserId)
        {
            _context.Notifications.Add(new Notification
            {
                RecipientUserId = farm.UserId,
                ActorUserId = actorUserId,
                Type = NotificationType.PestProtectionApplied,
                PestPlotId = plot.Id,
                Message =
                    $"{actor.Username} protegeu um lote da sua fazenda contra pragas.",
                CreatedAt = now
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(
            PestActionFailure.None,
            new ApplyPestProtectionResponse(
                plot.Id,
                ToContractStatus(plot.PestStatus),
                plot.RemainingYield,
                ToContractPest(plot),
                PestOptions.NaturalRepellentItemId,
                item.Quantity,
                plot.ProtectedUntil.Value));
    }

    public static string ToContractStatus(PestStatus status)
    {
        var value = status.ToString();
        return char.ToLowerInvariant(value[0]) + value[1..];
    }

    public DateTime? GetNextPestCheckAt(
        IEnumerable<Plot> plots,
        DateTime now)
    {
        if (!_options.Enabled)
            return null;

        return plots
            .Where(plot =>
                plot.SeedId != null
                && plot.PestStatus == PestStatus.None
                && plot.ReadyAt.HasValue)
            .Select(plot => plot.ReadyAt!.Value.AddMinutes(
                _options.SafetyPeriodMinutes))
            .Where(checkAt => checkAt > now)
            .Order()
            .Cast<DateTime?>()
            .FirstOrDefault();
    }

    public static PestStateResponse? ToContractPest(Plot plot)
    {
        if (plot.SeedId == null
            || plot.PestStatus == PestStatus.None
            || !plot.PestType.HasValue)
        {
            return null;
        }

        return new PestStateResponse(
            plot.PestType.Value.ToString().ToLowerInvariant(),
            ToContractStatus(plot.PestStatus),
            plot.PestScheduledAt,
            plot.PestAppearsAt,
            plot.PestAppearedAt,
            plot.PestConsumesAt,
            plot.PestResolvedAt,
            plot.PestConsumedAmount,
            plot.PestStatus == PestStatus.Active,
            plot.PestOccurrenceId);
    }

    private async Task<PestActionAttempt?> FindPersistedRemovalAsync(
        Guid actorUserId,
        Guid farmId,
        Guid plotId,
        Guid expectedOccurrenceId,
        Guid idempotencyKey,
        Plot currentPlot,
        CancellationToken cancellationToken)
    {
        var completion = await _context.PestRemovalCompletions
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate =>
                    candidate.ActorUserId == actorUserId
                    && candidate.IdempotencyKey == idempotencyKey,
                cancellationToken);

        if (completion == null)
            return null;

        if (completion.FarmId != farmId
            || completion.PlotId != plotId
            || completion.PestOccurrenceId != expectedOccurrenceId)
        {
            return new PestActionAttempt(
                PestActionFailure.IdempotencyKeyReused);
        }

        return new PestActionAttempt(
            PestActionFailure.None,
            ToRemovalResponse(completion, currentPlot, replayed: true));
    }

    private static PestActionResponse ToRemovalResponse(
        PestRemovalCompletion completion,
        Plot currentPlot,
        bool replayed)
    {
        var stillRepresentsCompletion =
            currentPlot.PestOccurrenceId == completion.PestOccurrenceId;

        return new PestActionResponse(
            currentPlot.Id,
            ToContractStatus(PestStatus.Removed),
            completion.RemainingYieldAfter,
            stillRepresentsCompletion
                ? ToContractPest(currentPlot)
                : null,
            completion.Id,
            completion.PestOccurrenceId,
            completion.CoinsGained,
            completion.XpGained,
            completion.CoinsAfter,
            completion.CoinsGained > 0 || completion.XpGained > 0,
            replayed);
    }

    private async Task<bool> CanInteractAsync(
        Guid actorUserId,
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        if (actorUserId == ownerUserId)
            return true;

        var (userAId, userBId) = FriendshipService.GetCanonicalPair(
            actorUserId,
            ownerUserId);
        return await _context.Friendships.AnyAsync(
            friendship =>
                friendship.UserAId == userAId
                && friendship.UserBId == userBId
                && friendship.Status == FriendshipStatus.Accepted,
            cancellationToken);
    }

    private static void ResolveScheduledAfterTheft(
        Plot plot,
        DateTime now)
    {
        plot.PestStatus = PestStatus.CancelledByTheft;
        plot.PestResolvedAt = now;
    }
}
