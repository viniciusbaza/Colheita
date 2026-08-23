using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FarmAndFriends.Api.Domain.Services;

public sealed class PremiumCurrencyService
{
    public const int MaximumIdempotencyKeyLength = 200;
    public const int MaximumEventReferenceLength = 200;
    public const int MaximumExternalReferenceLength = 200;
    public const int MaximumItemIdentifierLength = 120;
    public const int MaximumItemNameLength = 200;

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;

    public PremiumCurrencyService(
        AppDbContext context,
        TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public Task<PremiumCurrencyOperationResult> CreditAsync(
        Guid userId,
        int amount,
        PremiumCurrencyEventType eventType,
        string eventReference,
        string? idempotencyKey,
        string? externalSource = null,
        string? externalTransactionId = null,
        string? metadata = null,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        return ApplyAsync(
            new PremiumCurrencyOperation(
                userId,
                amount,
                eventType,
                eventReference,
                idempotencyKey,
                externalSource,
                externalTransactionId,
                null,
                [],
                metadata),
            cancellationToken);
    }

    public Task<PremiumCurrencyOperationResult> DebitAsync(
        Guid userId,
        int amount,
        PremiumCurrencyEventType eventType,
        string eventReference,
        string idempotencyKey,
        IReadOnlyCollection<PremiumCurrencyPurchaseItemSnapshot>? items = null,
        string? metadata = null,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        return ApplyAsync(
            new PremiumCurrencyOperation(
                userId,
                checked(-amount),
                eventType,
                eventReference,
                idempotencyKey,
                null,
                null,
                null,
                items ?? [],
                metadata),
            cancellationToken);
    }

    public async Task<PremiumCurrencyOperationResult> ReverseAsync(
        Guid userId,
        Guid transactionId,
        PremiumCurrencyEventType eventType,
        string eventReference,
        string idempotencyKey,
        string? metadata = null,
        CancellationToken cancellationToken = default)
    {
        EnsureExplicitTransaction();
        var inventory = await _context.LockInventoryAsync(
            userId,
            cancellationToken);
        if (inventory == null)
            return PremiumCurrencyOperationResult.Failed(
                PremiumCurrencyOperationFailure.InventoryNotFound);

        var original = await _context.PremiumCurrencyTransactions
            .AsNoTracking()
            .SingleOrDefaultAsync(transaction =>
                    transaction.Id == transactionId,
                cancellationToken);
        if (original == null || original.UserId != userId)
            return PremiumCurrencyOperationResult.Failed(
                PremiumCurrencyOperationFailure.TransactionNotFound);

        var operation = new PremiumCurrencyOperation(
            userId,
            checked(-original.Amount),
            eventType,
            eventReference,
            idempotencyKey,
            null,
            null,
            transactionId,
            [],
            metadata);

        return await ApplyLockedAsync(
            inventory,
            operation,
            cancellationToken);
    }

    public async Task<PremiumCurrencyOperationResult> ApplyAsync(
        PremiumCurrencyOperation operation,
        CancellationToken cancellationToken = default)
    {
        EnsureExplicitTransaction();
        var inventory = await _context.LockInventoryAsync(
            operation.UserId,
            cancellationToken);
        if (inventory == null)
            return PremiumCurrencyOperationResult.Failed(
                PremiumCurrencyOperationFailure.InventoryNotFound);

        return await ApplyLockedAsync(
            inventory,
            operation,
            cancellationToken);
    }

    private async Task<PremiumCurrencyOperationResult> ApplyLockedAsync(
        Inventory inventory,
        PremiumCurrencyOperation operation,
        CancellationToken cancellationToken)
    {
        ValidateOperation(operation);
        var fingerprint = PremiumCurrencyOperationFingerprint.Create(operation);

        var existingResult = await FindExistingAsync(
            operation,
            fingerprint,
            cancellationToken);
        if (existingResult != null)
            return existingResult;

        if (operation.ReversesTransactionId.HasValue)
        {
            var reversalValidation = await ValidateReversalAsync(
                operation,
                cancellationToken);
            if (reversalValidation != PremiumCurrencyOperationFailure.None)
                return PremiumCurrencyOperationResult.Failed(reversalValidation);
        }

        int balanceAfter;
        try
        {
            balanceAfter = checked(inventory.PremiumCoins + operation.Amount);
        }
        catch (OverflowException)
        {
            return PremiumCurrencyOperationResult.Failed(
                PremiumCurrencyOperationFailure.BalanceOverflow);
        }

        if (balanceAfter < 0)
            return PremiumCurrencyOperationResult.Failed(
                PremiumCurrencyOperationFailure.InsufficientBalance);

        var balanceBefore = inventory.PremiumCoins;
        inventory.PremiumCoins = balanceAfter;
        var transaction = new PremiumCurrencyTransaction
        {
            Id = Guid.NewGuid(),
            UserId = operation.UserId,
            Amount = operation.Amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            EventType = operation.EventType,
            EventReference = operation.EventReference,
            IdempotencyKey = operation.IdempotencyKey,
            OperationFingerprint = fingerprint,
            ExternalSource = operation.ExternalSource,
            ExternalTransactionId = operation.ExternalTransactionId,
            ReversesTransactionId = operation.ReversesTransactionId,
            Metadata = operation.Metadata,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        foreach (var item in operation.Items)
        {
            transaction.PurchaseItems.Add(new PremiumCurrencyPurchaseItem
            {
                Id = Guid.NewGuid(),
                ItemIdSnapshot = item.ItemIdSnapshot,
                ItemNameSnapshot = item.ItemNameSnapshot,
                Quantity = item.Quantity,
                UnitPremiumPrice = item.UnitPremiumPrice
            });
        }

        _context.PremiumCurrencyTransactions.Add(transaction);
        try
        {
            // Flush inside the caller-owned transaction so database-enforced
            // idempotency is classified before gameplay consequences run.
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            DetachFailedOperation(transaction, inventory, balanceBefore);

            var racedResult = await FindExistingAsync(
                operation,
                fingerprint,
                cancellationToken);
            if (racedResult != null)
                return racedResult;

            if (operation.ReversesTransactionId.HasValue
                && await _context.PremiumCurrencyTransactions
                    .AsNoTracking()
                    .AnyAsync(candidate =>
                            candidate.ReversesTransactionId
                                == operation.ReversesTransactionId,
                        cancellationToken))
            {
                return PremiumCurrencyOperationResult.Failed(
                    PremiumCurrencyOperationFailure.AlreadyReversed);
            }

            throw;
        }

        return PremiumCurrencyOperationResult.Applied(transaction);
    }

    private async Task<PremiumCurrencyOperationResult?> FindExistingAsync(
        PremiumCurrencyOperation operation,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        PremiumCurrencyTransaction? byKey = null;
        if (operation.IdempotencyKey != null)
        {
            byKey = await _context.PremiumCurrencyTransactions
                .AsNoTracking()
                .SingleOrDefaultAsync(transaction =>
                        transaction.UserId == operation.UserId
                        && transaction.IdempotencyKey
                            == operation.IdempotencyKey,
                    cancellationToken);
        }

        PremiumCurrencyTransaction? byExternalReference = null;
        if (operation.ExternalSource != null)
        {
            byExternalReference = await _context.PremiumCurrencyTransactions
                .AsNoTracking()
                .SingleOrDefaultAsync(transaction =>
                        transaction.ExternalSource == operation.ExternalSource
                        && transaction.ExternalTransactionId
                            == operation.ExternalTransactionId,
                    cancellationToken);
        }

        if (byKey != null && byExternalReference != null
            && byKey.Id != byExternalReference.Id)
        {
            return PremiumCurrencyOperationResult.Failed(
                PremiumCurrencyOperationFailure.InconsistentIdempotencyState);
        }

        var existing = byKey ?? byExternalReference;
        if (existing == null)
            return null;

        if (!string.Equals(
                existing.OperationFingerprint,
                fingerprint,
                StringComparison.Ordinal))
        {
            return PremiumCurrencyOperationResult.Failed(
                byKey != null
                    ? PremiumCurrencyOperationFailure.IdempotencyConflict
                    : PremiumCurrencyOperationFailure.ExternalTransactionConflict);
        }

        return PremiumCurrencyOperationResult.Replayed(existing);
    }

    private async Task<PremiumCurrencyOperationFailure> ValidateReversalAsync(
        PremiumCurrencyOperation operation,
        CancellationToken cancellationToken)
    {
        var original = await _context.PremiumCurrencyTransactions
            .AsNoTracking()
            .SingleOrDefaultAsync(transaction =>
                    transaction.Id == operation.ReversesTransactionId,
                cancellationToken);

        if (original == null || original.UserId != operation.UserId)
            return PremiumCurrencyOperationFailure.TransactionNotFound;
        if (original.ReversesTransactionId.HasValue)
            return PremiumCurrencyOperationFailure.CannotReverseReversal;
        if (operation.Amount != checked(-original.Amount))
            return PremiumCurrencyOperationFailure.InvalidReversalAmount;
        if (await _context.PremiumCurrencyTransactions
            .AsNoTracking()
            .AnyAsync(transaction =>
                    transaction.ReversesTransactionId == original.Id,
                cancellationToken))
        {
            return PremiumCurrencyOperationFailure.AlreadyReversed;
        }

        return PremiumCurrencyOperationFailure.None;
    }

    private void EnsureExplicitTransaction()
    {
        if (_context.Database.CurrentTransaction == null)
        {
            throw new InvalidOperationException(
                "Premium currency operations require an explicit transaction.");
        }
    }

    private static void ValidateOperation(PremiumCurrencyOperation operation)
    {
        if (operation.UserId == Guid.Empty)
            throw new ArgumentException("UserId is required.", nameof(operation));
        if (operation.Amount == 0)
            throw new ArgumentException("Amount cannot be zero.", nameof(operation));
        ValidateRequiredText(
            operation.EventReference,
            MaximumEventReferenceLength,
            nameof(operation.EventReference));

        if (operation.IdempotencyKey == null
            && operation.ExternalSource == null)
        {
            throw new ArgumentException(
                "An idempotency key or external transaction reference is required.",
                nameof(operation));
        }

        if (operation.IdempotencyKey != null)
        {
            ValidateRequiredText(operation.IdempotencyKey,
                MaximumIdempotencyKeyLength, nameof(operation.IdempotencyKey));
            if (operation.IdempotencyKey.IndexOf(':',
                    StringComparison.Ordinal) <= 0)
            {
                throw new ArgumentException(
                    "Idempotency keys must have a backend-defined namespace.",
                    nameof(operation));
            }
        }

        var hasExternalSource = operation.ExternalSource != null;
        var hasExternalId = operation.ExternalTransactionId != null;
        if (hasExternalSource != hasExternalId)
        {
            throw new ArgumentException(
                "ExternalSource and ExternalTransactionId must be supplied together.",
                nameof(operation));
        }

        if (hasExternalSource)
        {
            ValidateRequiredText(operation.ExternalSource!,
                MaximumExternalReferenceLength, nameof(operation.ExternalSource));
            ValidateRequiredText(operation.ExternalTransactionId!,
                MaximumExternalReferenceLength,
                nameof(operation.ExternalTransactionId));
        }

        if (operation.Metadata != null)
            using (JsonDocument.Parse(operation.Metadata)) { }

        var isReversalEvent = operation.EventType is
            PremiumCurrencyEventType.Refund
            or PremiumCurrencyEventType.Compensation;
        if (isReversalEvent != operation.ReversesTransactionId.HasValue)
        {
            throw new ArgumentException(
                "Refund and compensation events must reverse one transaction.",
                nameof(operation));
        }

        var validDirection = operation.EventType switch
        {
            PremiumCurrencyEventType.AccountInitialGrant or
                PremiumCurrencyEventType.PremiumCurrencyPurchase or
                PremiumCurrencyEventType.AdminGrant => operation.Amount > 0,
            PremiumCurrencyEventType.LandPurchase or
                PremiumCurrencyEventType.PremiumItemPurchase =>
                    operation.Amount < 0,
            _ => true
        };
        if (!validDirection)
        {
            throw new ArgumentException(
                "The transaction amount direction does not match its event.",
                nameof(operation));
        }

        var itemIdentifiers = new HashSet<string>(StringComparer.Ordinal);
        long purchaseTotal = 0;
        foreach (var item in operation.Items)
        {
            ValidateRequiredText(item.ItemIdSnapshot,
                MaximumItemIdentifierLength, nameof(item.ItemIdSnapshot));
            ValidateRequiredText(item.ItemNameSnapshot,
                MaximumItemNameLength, nameof(item.ItemNameSnapshot));
            if (!itemIdentifiers.Add(item.ItemIdSnapshot))
                throw new ArgumentException(
                    "Purchase item identifiers must be unique.", nameof(operation));
            if (item.Quantity <= 0 || item.UnitPremiumPrice <= 0)
                throw new ArgumentException(
                    "Purchase item quantity and unit price must be positive.",
                    nameof(operation));
            purchaseTotal = checked(purchaseTotal
                + (long)item.Quantity * item.UnitPremiumPrice);
        }

        if (operation.EventType == PremiumCurrencyEventType.PremiumItemPurchase)
        {
            if (operation.Items.Count == 0
                || operation.Amount >= 0
                || purchaseTotal != -(long)operation.Amount)
            {
                throw new ArgumentException(
                    "Premium item purchase lines must equal the debit amount.",
                    nameof(operation));
            }
        }
        else if (operation.Items.Count != 0)
        {
            throw new ArgumentException(
                "Purchase item snapshots are only valid for premium item purchases.",
                nameof(operation));
        }
    }

    private static void ValidateRequiredText(
        string value,
        int maximumLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
            throw new ArgumentException($"Invalid {parameterName}.", parameterName);
    }

    private void DetachFailedOperation(
        PremiumCurrencyTransaction transaction,
        Inventory inventory,
        int balanceBefore)
    {
        foreach (var item in transaction.PurchaseItems)
            _context.Entry(item).State = EntityState.Detached;
        _context.Entry(transaction).State = EntityState.Detached;
        inventory.PremiumCoins = balanceBefore;
        _context.Entry(inventory)
            .Property(candidate => candidate.PremiumCoins)
            .IsModified = false;
    }
}

public sealed record PremiumCurrencyOperation(
    Guid UserId,
    int Amount,
    PremiumCurrencyEventType EventType,
    string EventReference,
    string? IdempotencyKey,
    string? ExternalSource,
    string? ExternalTransactionId,
    Guid? ReversesTransactionId,
    IReadOnlyCollection<PremiumCurrencyPurchaseItemSnapshot> Items,
    string? Metadata = null);

public sealed record PremiumCurrencyPurchaseItemSnapshot(
    string ItemIdSnapshot,
    string ItemNameSnapshot,
    int Quantity,
    int UnitPremiumPrice);

public enum PremiumCurrencyOperationDisposition
{
    Applied,
    Replayed
}

public enum PremiumCurrencyOperationFailure
{
    None,
    InventoryNotFound,
    InsufficientBalance,
    BalanceOverflow,
    IdempotencyConflict,
    ExternalTransactionConflict,
    InconsistentIdempotencyState,
    TransactionNotFound,
    CannotReverseReversal,
    InvalidReversalAmount,
    AlreadyReversed
}

public sealed record PremiumCurrencyOperationResult(
    PremiumCurrencyOperationFailure Failure,
    PremiumCurrencyOperationDisposition? Disposition,
    PremiumCurrencyTransaction? Transaction)
{
    public bool Succeeded => Failure == PremiumCurrencyOperationFailure.None;

    public static PremiumCurrencyOperationResult Applied(
        PremiumCurrencyTransaction transaction) =>
        new(PremiumCurrencyOperationFailure.None,
            PremiumCurrencyOperationDisposition.Applied, transaction);

    public static PremiumCurrencyOperationResult Replayed(
        PremiumCurrencyTransaction transaction) =>
        new(PremiumCurrencyOperationFailure.None,
            PremiumCurrencyOperationDisposition.Replayed, transaction);

    public static PremiumCurrencyOperationResult Failed(
        PremiumCurrencyOperationFailure failure) =>
        new(failure, null, null);
}

public static class PremiumCurrencyOperationFingerprint
{
    public static string Create(PremiumCurrencyOperation operation)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, "v1");
        Append(hash, operation.UserId.ToString("D").ToLowerInvariant());
        Append(hash, operation.EventType.ToToken());
        Append(hash, operation.EventReference);
        Append(hash, Integer(operation.Amount));
        Append(hash, operation.ExternalSource);
        Append(hash, operation.ExternalTransactionId);
        Append(hash, operation.ReversesTransactionId?.ToString("D")
            .ToLowerInvariant());

        foreach (var item in operation.Items
            .OrderBy(item => item.ItemIdSnapshot, StringComparer.Ordinal))
        {
            Append(hash, item.ItemIdSnapshot);
            Append(hash, Integer(item.Quantity));
            Append(hash, Integer(item.UnitPremiumPrice));
        }

        return $"v1:{Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()}";
    }

    private static string Integer(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static void Append(IncrementalHash hash, string? value)
    {
        if (value == null)
        {
            Span<byte> nullLength = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(nullLength, -1);
            hash.AppendData(nullLength);
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}
