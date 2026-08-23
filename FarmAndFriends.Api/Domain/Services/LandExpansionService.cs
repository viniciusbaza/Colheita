using FarmAndFriends.Api.Configuration;
using FarmAndFriends.Api.Contracts.Land;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Domain.Rules;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FarmAndFriends.Api.Domain.Services;

public sealed class LandExpansionService
{
    public const string CoinsCurrency = "coins";
    public const string PremiumCoinsCurrency = "premiumCoins";

    private readonly AppDbContext _context;
    private readonly LandExpansionOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LandExpansionService> _logger;
    private readonly PremiumCurrencyService _premiumCurrencyService;

    public LandExpansionService(
        AppDbContext context,
        IOptions<LandExpansionOptions> options,
        TimeProvider timeProvider,
        ILogger<LandExpansionService> logger,
        PremiumCurrencyService premiumCurrencyService)
    {
        _context = context;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
        _premiumCurrencyService = premiumCurrencyService;
    }

    public LandOfferResponse? GetOffer(
        Guid farmId,
        ICollection<Plot> plots)
    {
        if (!_options.Enabled)
            return null;

        var evaluation = FarmLayoutRules.Evaluate(plots);
        if (evaluation.Status == FarmLayoutStatus.Unsupported)
        {
            LogUnsupportedLayout(farmId, plots);
            return null;
        }

        return evaluation.Status == FarmLayoutStatus.OfferAvailable
            ? ToOfferResponse(evaluation)
            : null;
    }

    public async Task<LandPurchaseAttempt> PurchaseAsync(
        Guid buyerUserId,
        Guid plotId,
        LandPaymentCurrency paymentCurrency,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);
        var buyer = await _context.LockAsync(
            buyerUserId,
            cancellationToken);

        if (buyer == null)
            return new(LandPurchaseFailure.Unauthorized);

        var persisted = await _context.LandPurchaseCompletions
            .AsNoTracking()
            .SingleOrDefaultAsync(completion =>
                    completion.BuyerUserId == buyerUserId
                    && completion.IdempotencyKey == idempotencyKey,
                cancellationToken);

        if (persisted != null)
        {
            if (persisted.PlotId != plotId
                || persisted.PaymentCurrency
                    != ToContractCurrency(paymentCurrency))
            {
                return new(LandPurchaseFailure.IdempotencyKeyReused);
            }

            if (paymentCurrency == LandPaymentCurrency.PremiumCoins)
            {
                if (!persisted.PremiumCurrencyTransactionId.HasValue)
                {
                    return EconomyInconsistent(
                        "completion_missing_ledger_link",
                        buyerUserId,
                        plotId,
                        idempotencyKey,
                        persisted.Id);
                }

                var replay = await _premiumCurrencyService.DebitAsync(
                    buyerUserId,
                    persisted.AmountSpent,
                    PremiumCurrencyEventType.LandPurchase,
                    persisted.Id.ToString("D").ToLowerInvariant(),
                    LandIdempotencyKey(idempotencyKey),
                    cancellationToken: cancellationToken);
                if (!replay.Succeeded
                    || replay.Disposition
                        != PremiumCurrencyOperationDisposition.Replayed
                    || replay.Transaction?.Id
                        != persisted.PremiumCurrencyTransactionId)
                {
                    return EconomyInconsistent(
                        "completion_ledger_replay_mismatch",
                        buyerUserId,
                        plotId,
                        idempotencyKey,
                        persisted.Id,
                        persisted.PremiumCurrencyTransactionId,
                        replay.Failure);
                }
            }

            return new(
                LandPurchaseFailure.None,
                ToPurchaseResponse(persisted, replayed: true));
        }

        var ownedFarmId = await _context.Plots
            .AsNoTracking()
            .Where(plot =>
                plot.Id == plotId
                && plot.Farm.UserId == buyerUserId)
            .Select(plot => (Guid?)plot.FarmId)
            .SingleOrDefaultAsync(cancellationToken);

        if (!ownedFarmId.HasValue)
            return new(LandPurchaseFailure.PlotNotFound);

        if (!_options.Enabled)
            return new(LandPurchaseFailure.FeatureDisabled);

        var farm = await _context.LockFarmAsync(
            ownedFarmId.Value,
            cancellationToken);
        if (farm == null || farm.UserId != buyerUserId)
            return new(LandPurchaseFailure.PlotNotFound);

        var plots = await _context.LockFarmPlotsAsync(
            farm.Id,
            cancellationToken);
        var evaluation = FarmLayoutRules.Evaluate(plots);

        if (evaluation.Status == FarmLayoutStatus.Unsupported)
        {
            LogUnsupportedLayout(farm.Id, plots);
            return new(LandPurchaseFailure.UnsupportedLayout);
        }

        if (evaluation.Status != FarmLayoutStatus.OfferAvailable
            || evaluation.Offer == null
            || evaluation.OfferedPlot == null
            || evaluation.OfferedPlot.Id != plotId)
        {
            return new(LandPurchaseFailure.StaleOffer);
        }

        var offer = evaluation.Offer;
        if (buyer.Level < offer.MinimumLevel)
            return new(LandPurchaseFailure.LevelRequired);

        var amountSpent = paymentCurrency switch
        {
            LandPaymentCurrency.Coins => offer.CoinsPrice,
            LandPaymentCurrency.PremiumCoins => offer.PremiumCoinsPrice,
            _ => throw new ArgumentOutOfRangeException(
                nameof(paymentCurrency))
        };

        var completionId = Guid.NewGuid();
        Inventory? inventory;
        PremiumCurrencyTransaction? premiumTransaction = null;
        if (paymentCurrency == LandPaymentCurrency.Coins)
        {
            inventory = await _context.LockInventoryAsync(
                buyerUserId,
                cancellationToken);
            if (inventory == null)
                return new(LandPurchaseFailure.InventoryNotFound);
            if (inventory.Coins < amountSpent)
                return new(LandPurchaseFailure.InsufficientCoins);
            inventory.Coins -= amountSpent;
        }
        else
        {
            var debit = await _premiumCurrencyService.DebitAsync(
                buyerUserId,
                amountSpent,
                PremiumCurrencyEventType.LandPurchase,
                completionId.ToString("D").ToLowerInvariant(),
                LandIdempotencyKey(idempotencyKey),
                cancellationToken: cancellationToken);
            if (!debit.Succeeded)
            {
                return new(debit.Failure switch
                {
                    PremiumCurrencyOperationFailure.InsufficientBalance =>
                        LandPurchaseFailure.InsufficientPremiumCoins,
                    PremiumCurrencyOperationFailure.InventoryNotFound =>
                        LandPurchaseFailure.InventoryNotFound,
                    PremiumCurrencyOperationFailure.IdempotencyConflict =>
                        LandPurchaseFailure.IdempotencyKeyReused,
                    _ => EconomyInconsistent(
                        "premium_debit_failed",
                        buyerUserId,
                        plotId,
                        idempotencyKey,
                        completionId,
                        debit.Transaction?.Id,
                        debit.Failure).Failure
                });
            }

            if (debit.Disposition == PremiumCurrencyOperationDisposition.Replayed)
            {
                var replayedCompletion = await _context.LandPurchaseCompletions
                    .AsNoTracking()
                    .SingleOrDefaultAsync(completion =>
                            completion.PremiumCurrencyTransactionId
                                == debit.Transaction!.Id,
                        cancellationToken);
                if (replayedCompletion == null
                    || replayedCompletion.BuyerUserId != buyerUserId
                    || replayedCompletion.PlotId != plotId
                    || replayedCompletion.IdempotencyKey != idempotencyKey)
                {
                    return EconomyInconsistent(
                        "ledger_replay_missing_matching_completion",
                        buyerUserId,
                        plotId,
                        idempotencyKey,
                        replayedCompletion?.Id,
                        debit.Transaction!.Id);
                }

                return new(
                    LandPurchaseFailure.None,
                    ToPurchaseResponse(replayedCompletion, replayed: true));
            }

            premiumTransaction = debit.Transaction;
            inventory = await _context.Inventories
                .SingleAsync(candidate => candidate.UserId == buyerUserId,
                    cancellationToken);
        }

        evaluation.OfferedPlot.Unlocked = true;
        var addedPlotCount = 0;
        if (offer.PlotNumber == FarmLayoutRules.InitialPlotCount)
        {
            var expansionPlots = FarmLayoutRules.ExpansionPlots()
                .Select(definition => new Plot
                {
                    Id = Guid.NewGuid(),
                    FarmId = farm.Id,
                    X = definition.X,
                    Y = definition.Y,
                    Unlocked = false
                })
                .ToList();
            _context.Plots.AddRange(expansionPlots);
            addedPlotCount = expansionPlots.Count;
        }

        // FarmLayoutRules and the locked offer remain authoritative for the
        // PlotNumber-to-coordinate mapping and configured price. Those
        // cross-table/economic invariants are not duplicated in ledger checks.
        var completion = new LandPurchaseCompletion
        {
            Id = completionId,
            IdempotencyKey = idempotencyKey,
            BuyerUserId = buyerUserId,
            FarmId = farm.Id,
            PlotId = plotId,
            PremiumCurrencyTransactionId = premiumTransaction?.Id,
            PlotNumber = offer.PlotNumber,
            PaymentCurrency = ToContractCurrency(paymentCurrency),
            AmountSpent = amountSpent,
            CoinsAfter = inventory.Coins,
            PremiumCoinsAfter = inventory.PremiumCoins,
            AddedPlotCount = addedPlotCount,
            ExpandedWidth = offer.ExpandsTo?.Width,
            ExpandedHeight = offer.ExpandsTo?.Height,
            PurchasedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        _context.LandPurchaseCompletions.Add(completion);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(
            LandPurchaseFailure.None,
            ToPurchaseResponse(completion, replayed: false));
    }

    public static bool TryParsePaymentCurrency(
        string? value,
        out LandPaymentCurrency paymentCurrency)
    {
        switch (value)
        {
            case CoinsCurrency:
                paymentCurrency = LandPaymentCurrency.Coins;
                return true;
            case PremiumCoinsCurrency:
                paymentCurrency = LandPaymentCurrency.PremiumCoins;
                return true;
            default:
                paymentCurrency = default;
                return false;
        }
    }

    private static LandOfferResponse ToOfferResponse(
        FarmLayoutEvaluation evaluation)
    {
        var offer = evaluation.Offer!;
        return new LandOfferResponse(
            evaluation.OfferedPlot!.Id,
            offer.PlotNumber,
            FarmLayoutRules.MaxPlotCount,
            offer.MinimumLevel,
            new LandPricesResponse(
                offer.CoinsPrice,
                offer.PremiumCoinsPrice),
            offer.ExpandsTo == null
                ? null
                : new LandDimensionsResponse(
                    offer.ExpandsTo.Width,
                    offer.ExpandsTo.Height));
    }

    private static PurchaseLandResponse ToPurchaseResponse(
        LandPurchaseCompletion completion,
        bool replayed) =>
        new(
            completion.Id,
            completion.PlotId,
            completion.PlotNumber,
            completion.PaymentCurrency,
            completion.AmountSpent,
            completion.AddedPlotCount,
            completion.ExpandedWidth.HasValue
                && completion.ExpandedHeight.HasValue
                    ? new LandDimensionsResponse(
                        completion.ExpandedWidth.Value,
                        completion.ExpandedHeight.Value)
                    : null,
            replayed);

    private static string ToContractCurrency(
        LandPaymentCurrency paymentCurrency) =>
        paymentCurrency == LandPaymentCurrency.Coins
            ? CoinsCurrency
            : PremiumCoinsCurrency;

    private static string LandIdempotencyKey(Guid idempotencyKey) =>
        $"land-purchase:{idempotencyKey:D}".ToLowerInvariant();

    private LandPurchaseAttempt EconomyInconsistent(
        string reason,
        Guid buyerUserId,
        Guid plotId,
        Guid idempotencyKey,
        Guid? completionId = null,
        Guid? premiumCurrencyTransactionId = null,
        PremiumCurrencyOperationFailure? premiumFailure = null)
    {
        _logger.LogError(
            "Premium land purchase economy inconsistency. Reason={Reason} BuyerUserId={BuyerUserId} PlotId={PlotId} IdempotencyKey={IdempotencyKey} CompletionId={CompletionId} PremiumCurrencyTransactionId={PremiumCurrencyTransactionId} PremiumFailure={PremiumFailure}",
            reason,
            buyerUserId,
            plotId,
            idempotencyKey,
            completionId,
            premiumCurrencyTransactionId,
            premiumFailure);
        return new(LandPurchaseFailure.EconomyInconsistent);
    }

    private void LogUnsupportedLayout(
        Guid farmId,
        IEnumerable<Plot> plots)
    {
        _logger.LogWarning(
            "Land expansion rejected unsupported layout for farm {FarmId}. PlotCount={PlotCount}",
            farmId,
            plots.Count());
    }
}

public enum LandPaymentCurrency
{
    Coins,
    PremiumCoins
}

public enum LandPurchaseFailure
{
    None,
    Unauthorized,
    FeatureDisabled,
    PlotNotFound,
    StaleOffer,
    LevelRequired,
    InsufficientCoins,
    InsufficientPremiumCoins,
    UnsupportedLayout,
    InventoryNotFound,
    IdempotencyKeyReused,
    EconomyInconsistent
}

public sealed record LandPurchaseAttempt(
    LandPurchaseFailure Failure,
    PurchaseLandResponse? Response = null)
{
    public bool Succeeded => Failure == LandPurchaseFailure.None;
}
