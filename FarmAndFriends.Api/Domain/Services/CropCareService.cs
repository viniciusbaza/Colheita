using FarmAndFriends.Api.Configuration;
using FarmAndFriends.Api.Contracts.Care;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Domain.Rules;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FarmAndFriends.Api.Domain.Services;

public enum CropCareFailure
{
    None,
    PlotNotFound,
    SelfCare,
    NotFriends,
    NotGrowing,
    OpportunityUnavailable,
    CooldownActive,
    InventoryNotFound,
    IdempotencyKeyReused
}

public sealed record CropCareAttempt(
    CropCareFailure Failure,
    CareForPlotResponse? Response = null,
    DateTime? NextCareAt = null)
{
    public bool Succeeded => Failure == CropCareFailure.None && Response != null;

    public static CropCareAttempt Success(CareForPlotResponse response)
        => new(CropCareFailure.None, response);

    public static CropCareAttempt Failed(
        CropCareFailure failure,
        DateTime? nextCareAt = null)
        => new(failure, NextCareAt: nextCareAt);
}

public sealed record CropCaregiverState(
    Guid UserId,
    string Username,
    DateTime CaredAt);

public sealed record PlotCropCareState(
    Guid OpportunityId,
    bool CanCare,
    bool RewardAvailable,
    DateTime? NextCareAt,
    DateTime? CareCycleEndsAt,
    CropCaregiverState? ViewerCare,
    int CaregiverCount,
    IReadOnlyList<CropCaregiverState> Caregivers);

public sealed class CropCareService
{
    private const int VisibleCaregiverLimit = 3;

    private readonly AppDbContext _context;
    private readonly ExperienceService _experienceService;
    private readonly CropCareOptions _options;
    private readonly ILogger<CropCareService> _logger;
    private readonly TimeProvider _timeProvider;

    public CropCareService(
        AppDbContext context,
        ExperienceService experienceService,
        IOptions<CropCareOptions> options,
        ILogger<CropCareService> logger,
        TimeProvider? timeProvider = null)
    {
        _context = context;
        _experienceService = experienceService;
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<IReadOnlyDictionary<Guid, PlotCropCareState>>
        GetFarmCareStatesAsync(
            Guid farmId,
            Guid viewerUserId,
            Guid ownerUserId,
            IEnumerable<Plot> plots,
            DateTime now,
            CancellationToken cancellationToken = default)
    {
        var currentPlots = plots
            .Where(plot => plot.CareOpportunityId.HasValue)
            .ToList();

        if (currentPlots.Count == 0)
            return new Dictionary<Guid, PlotCropCareState>();

        var opportunityIds = currentPlots
            .Select(plot => plot.CareOpportunityId!.Value)
            .Distinct()
            .ToArray();

        var isOwner = viewerUserId == ownerUserId;
        VisitorFarmCareCycle? activeCycle = null;
        var rewardAvailable = false;

        if (!isOwner)
        {
            activeCycle = await _context.VisitorFarmCareCycles
                .AsNoTracking()
                .Where(cycle =>
                    cycle.VisitorUserId == viewerUserId
                    && cycle.FarmId == farmId
                    && cycle.EndsAt > now)
                .OrderByDescending(cycle => cycle.StartedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (activeCycle != null)
            {
                rewardAvailable = activeCycle.RewardGranted;
            }
            else
            {
                var rewardWindowStart = now.AddHours(
                    -_options.VisitorFarmRewardRollingWindowHours);
                var rewardedCycleCount =
                    await _context.VisitorFarmCareCycles
                        .AsNoTracking()
                        .CountAsync(cycle =>
                                cycle.VisitorUserId == viewerUserId
                                && cycle.FarmId == farmId
                                && cycle.RewardGranted
                                && cycle.StartedAt > rewardWindowStart
                                && cycle.StartedAt <= now,
                            cancellationToken);
                rewardAvailable = rewardedCycleCount
                    < _options
                        .MaxRewardedCyclesPerVisitorFarmWindow;
            }
        }

        var completions = await _context.CropCareCompletions
            .AsNoTracking()
            .Where(completion =>
                opportunityIds.Contains(completion.CareOpportunityId)
                && (isOwner
                    ? completion.OwnerUserId == ownerUserId
                    : completion.VisitorUserId == viewerUserId))
            .OrderByDescending(completion => completion.CaredAt)
            .ToListAsync(cancellationToken);

        var completionsByOpportunity = completions
            .GroupBy(completion => completion.CareOpportunityId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var states = new Dictionary<Guid, PlotCropCareState>();

        foreach (var plot in currentPlots)
        {
            var opportunityId = plot.CareOpportunityId!.Value;
            completionsByOpportunity.TryGetValue(
                opportunityId,
                out var opportunityCompletions);
            opportunityCompletions ??= [];

            var viewerCompletion = isOwner
                ? null
                : opportunityCompletions.FirstOrDefault();

            var isGrowing = CropCareRules.IsGrowing(
                plot,
                opportunityId,
                now);
            var viewerNextCareAt = viewerCompletion?.CaredAt.AddHours(
                _options.VisitorPlotCareCooldownHours);
            var cooldownActive = isGrowing && viewerNextCareAt > now;
            var nextCareAt = cooldownActive
                ? viewerNextCareAt
                : null;
            var canCare = !isOwner
                && !cooldownActive
                && isGrowing;
            var plotRewardAvailable = canCare
                && rewardAvailable
                && (activeCycle == null
                    || CropCareRules.WasGrowingAt(
                        plot,
                        opportunityId,
                        activeCycle.StartedAt))
                && (activeCycle == null
                    || opportunityCompletions.All(completion =>
                        completion.VisitorFarmCareCycleId
                            != activeCycle.Id));

            var visibleCaregivers = isOwner
                ? opportunityCompletions
                    .GroupBy(completion => completion.VisitorUserId)
                    .Select(group => group
                        .OrderByDescending(completion => completion.CaredAt)
                        .First())
                    .OrderByDescending(completion => completion.CaredAt)
                    .Take(VisibleCaregiverLimit)
                    .Select(ToCaregiverState)
                    .ToList()
                : [];

            var caregiverCount = isOwner
                ? opportunityCompletions
                    .Select(completion => completion.VisitorUserId)
                    .Distinct()
                    .Count()
                : 0;

            if (!canCare && viewerCompletion == null && caregiverCount == 0)
                continue;

            states[plot.Id] = new PlotCropCareState(
                opportunityId,
                canCare,
                plotRewardAvailable,
                nextCareAt,
                activeCycle?.EndsAt,
                viewerCompletion == null
                    ? null
                    : ToCaregiverState(viewerCompletion),
                caregiverCount,
                visibleCaregivers);
        }

        return states;
    }

    public async Task<CropCareAttempt> CareAsync(
        Guid visitorUserId,
        Guid farmId,
        Guid plotId,
        Guid opportunityId,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var persistedAttempt = await FindPersistedAttemptAsync(
            visitorUserId,
            farmId,
            plotId,
            opportunityId,
            idempotencyKey,
            cancellationToken);

        if (persistedAttempt != null)
            return persistedAttempt;

        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        var plotSnapshot = await _context.Plots
            .AsNoTracking()
            .Include(candidate => candidate.Farm)
            .FirstOrDefaultAsync(
                candidate => candidate.Id == plotId
                    && candidate.FarmId == farmId,
                cancellationToken);

        if (plotSnapshot == null)
            return CropCareAttempt.Failed(CropCareFailure.PlotNotFound);

        var ownerUserId = plotSnapshot.Farm.UserId;
        if (ownerUserId == visitorUserId)
            return CropCareAttempt.Failed(CropCareFailure.SelfCare);

        // This is the global mutex for this visitor's economy and care rounds.
        var visitor = await _context.LockAsync(
            visitorUserId,
            cancellationToken);

        if (visitor == null)
            return CropCareAttempt.Failed(CropCareFailure.PlotNotFound);

        var (userAId, userBId) = FriendshipService.GetCanonicalPair(
            visitorUserId,
            ownerUserId);

        // The accepted friendship is the canonical authorization lock.
        var friendship = await _context.Friendships
            .FromSqlInterpolated(
                $"""
                SELECT * FROM "Friendships"
                WHERE "UserAId" = {userAId} AND "UserBId" = {userBId}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);

        if (friendship?.Status != FriendshipStatus.Accepted)
            return CropCareAttempt.Failed(CropCareFailure.NotFriends);

        var plot = await _context.Plots
            .FromSqlInterpolated(
                $"""
                SELECT * FROM "Plots"
                WHERE "Id" = {plotId} AND "FarmId" = {farmId}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);

        if (plot == null)
            return CropCareAttempt.Failed(CropCareFailure.PlotNotFound);

        // Re-read the durable ledger after all contended locks.
        persistedAttempt = await FindPersistedAttemptAsync(
            visitorUserId,
            farmId,
            plotId,
            opportunityId,
            idempotencyKey,
            cancellationToken);

        if (persistedAttempt != null)
        {
            await transaction.CommitAsync(cancellationToken);
            return persistedAttempt;
        }

        var now = TruncateToPostgresPrecision(
            _timeProvider.GetUtcNow().UtcDateTime);

        if (plot.SeedId == null || plot.ReadyAt <= now)
            return CropCareAttempt.Failed(CropCareFailure.NotGrowing);

        if (!CropCareRules.IsGrowing(plot, opportunityId, now))
        {
            _logger.LogInformation(
                "Crop care opportunity {OpportunityId} no longer matches the growing crop.",
                opportunityId);
            return CropCareAttempt.Failed(
                CropCareFailure.OpportunityUnavailable);
        }

        var latestCompletion = await _context.CropCareCompletions
            .AsNoTracking()
            .Where(completion =>
                completion.VisitorUserId == visitorUserId
                && completion.CareOpportunityId == opportunityId)
            .OrderByDescending(completion => completion.CaredAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestCompletion != null)
        {
            var nextCareAt = latestCompletion.CaredAt.AddHours(
                _options.VisitorPlotCareCooldownHours);

            if (now < nextCareAt)
            {
                return CropCareAttempt.Failed(
                    CropCareFailure.CooldownActive,
                    nextCareAt);
            }
        }

        var cycle = await _context.VisitorFarmCareCycles
            .Where(candidate =>
                candidate.VisitorUserId == visitorUserId
                && candidate.FarmId == farmId
                && candidate.EndsAt > now)
            .OrderByDescending(candidate => candidate.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var createdCycle = cycle == null;

        if (cycle == null)
        {
            var rewardWindowStart = now.AddHours(
                -_options.VisitorFarmRewardRollingWindowHours);
            var rewardedCycleCount = await _context.VisitorFarmCareCycles
                .AsNoTracking()
                .CountAsync(candidate =>
                        candidate.VisitorUserId == visitorUserId
                        && candidate.FarmId == farmId
                        && candidate.RewardGranted
                        && candidate.StartedAt > rewardWindowStart
                        && candidate.StartedAt <= now,
                    cancellationToken);
            var rewardGranted =
                rewardedCycleCount
                    < _options
                        .MaxRewardedCyclesPerVisitorFarmWindow;

            cycle = new VisitorFarmCareCycle
            {
                Id = Guid.NewGuid(),
                VisitorUserId = visitorUserId,
                OwnerUserId = ownerUserId,
                FarmId = farmId,
                StartedAt = now,
                EndsAt = now.AddHours(
                    _options.VisitorFarmRewardCycleHours),
                RewardGranted = rewardGranted,
                CoinsReward = rewardGranted ? _options.CoinsReward : 0,
                XpReward = rewardGranted ? _options.XpReward : 0
            };
            _context.VisitorFarmCareCycles.Add(cycle);
        }

        var inventory = await _context.Inventories
            .FromSqlInterpolated(
                $"""
                SELECT * FROM "Inventories"
                WHERE "UserId" = {visitorUserId}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);

        if (inventory == null)
            return CropCareAttempt.Failed(CropCareFailure.InventoryNotFound);

        var rewardGrantedForPlot = cycle.RewardGranted
            && CropCareRules.WasGrowingAt(
                plot,
                opportunityId,
                cycle.StartedAt)
            && latestCompletion?.VisitorFarmCareCycleId != cycle.Id;
        var coinsGained = rewardGrantedForPlot
            ? cycle.CoinsReward
            : 0;
        var xpGained = rewardGrantedForPlot
            ? cycle.XpReward
            : 0;

        if (coinsGained > 0)
            inventory.Coins = checked(inventory.Coins + coinsGained);
        if (xpGained > 0)
            _experienceService.AddXp(visitor, xpGained);

        var completion = new CropCareCompletion
        {
            Id = Guid.NewGuid(),
            VisitorFarmCareCycleId = cycle.Id,
            VisitorFarmCareCycle = cycle,
            VisitorUserId = visitorUserId,
            OwnerUserId = ownerUserId,
            FarmId = farmId,
            PlotId = plotId,
            CareOpportunityId = opportunityId,
            IdempotencyKey = idempotencyKey,
            CaredAt = now,
            CaredByUsername = visitor.Username,
            CoinsGained = coinsGained,
            XpGained = xpGained,
            CoinsAfter = inventory.Coins
        };
        _context.CropCareCompletions.Add(completion);

        if (createdCycle)
        {
            var notificationWindowStart = now.AddHours(
                -_options.OwnerNotificationDeduplicationWindowHours);
            var alreadyNotified = await _context.Notifications
                .AsNoTracking()
                .AnyAsync(notification =>
                        notification.Type
                            == NotificationType.CropCaredFor
                        && notification.ActorUserId == visitorUserId
                        && notification.RecipientUserId == ownerUserId
                        && notification.CreatedAt
                            > notificationWindowStart,
                    cancellationToken);

            if (!alreadyNotified)
            {
                _context.Notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    RecipientUserId = ownerUserId,
                    ActorUserId = visitorUserId,
                    Type = NotificationType.CropCaredFor,
                    Message =
                        $"{visitor.Username} passou pela sua fazenda e regou as plantações.",
                    CreatedAt = now
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "User {VisitorUserId} cared for plot {PlotId} on farm {FarmId} in cycle {CareCycleId}.",
            visitorUserId,
            plotId,
            farmId,
            cycle.Id);

        return CropCareAttempt.Success(ToResponse(completion, cycle));
    }

    private async Task<CropCareAttempt?> FindPersistedAttemptAsync(
        Guid visitorUserId,
        Guid farmId,
        Guid plotId,
        Guid opportunityId,
        Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        var completionForKey = await _context.CropCareCompletions
            .AsNoTracking()
            .Include(completion => completion.VisitorFarmCareCycle)
            .SingleOrDefaultAsync(completion =>
                    completion.VisitorUserId == visitorUserId
                    && completion.IdempotencyKey == idempotencyKey,
                cancellationToken);

        if (completionForKey != null)
        {
            return HasSameTarget(
                completionForKey,
                farmId,
                plotId,
                opportunityId)
                ? CropCareAttempt.Success(ToResponse(
                    completionForKey,
                    completionForKey.VisitorFarmCareCycle))
                : CropCareAttempt.Failed(
                    CropCareFailure.IdempotencyKeyReused);
        }

        return null;
    }

    private static CropCaregiverState ToCaregiverState(
        CropCareCompletion completion)
    {
        return new CropCaregiverState(
            completion.VisitorUserId,
            completion.CaredByUsername,
            completion.CaredAt);
    }

    private static bool HasSameTarget(
        CropCareCompletion completion,
        Guid farmId,
        Guid plotId,
        Guid opportunityId)
    {
        return completion.FarmId == farmId
            && completion.PlotId == plotId
            && completion.CareOpportunityId == opportunityId;
    }

    private CareForPlotResponse ToResponse(
        CropCareCompletion completion,
        VisitorFarmCareCycle cycle)
    {
        return new CareForPlotResponse(
            completion.Id,
            completion.PlotId,
            completion.CareOpportunityId,
            completion.CaredAt,
            completion.CaredAt.AddHours(
                _options.VisitorPlotCareCooldownHours),
            completion.VisitorUserId,
            completion.CaredByUsername,
            completion.CoinsGained,
            completion.XpGained,
            completion.CoinsAfter,
            cycle.Id,
            cycle.EndsAt,
            completion.CoinsGained > 0 || completion.XpGained > 0);
    }

    private static DateTime TruncateToPostgresPrecision(DateTime value)
    {
        const long ticksPerMicrosecond =
            TimeSpan.TicksPerMillisecond / 1000;
        return new DateTime(
            value.Ticks - value.Ticks % ticksPerMicrosecond,
            DateTimeKind.Utc);
    }
}
