# ADR-0003 — Premium currency ledger

Status: Accepted

Date: 2026-08-18

Decision owners:

- Backend Engineer
- Economy Designer
- Technical Planner

## Context

Premium currency may later be sold for real money. A mutable balance alone
cannot explain why value entered or left an account, prevent duplicate payment
delivery, or support safe compensation. Standard coins do not carry that
requirement in this version.

## Decision

`Inventory.PremiumCoins` remains the materialized current balance. Every
premium movement passes through `PremiumCurrencyService` and creates an
immutable-at-application-level `PremiumCurrencyTransaction` in the same
PostgreSQL transaction as the balance and gameplay consequence.

The ledger records signed amount, balances before and after, a stable event
token, origin references, a deterministic database sequence and a canonical
operation fingerprint. Namespaced idempotency keys and external transaction
references have partial unique indexes. A retry is classified explicitly as
`Applied` or `Replayed`; gameplay consequences run only for `Applied`.

Premium item purchase lines are snapshots without a catalog foreign key.
Reversals are integral and unique in v1. Partial refunds are outside scope.
Standard coins and standard-coin item purchases are not ledgered.

## Consequences

- Current balances remain cheap to read.
- Premium history can be reconciled from the first account grant.
- Payment retries cannot credit twice when the provider reference is reused.
- The service and its caller must share an explicit transaction and canonical
  lock order.
- Ledger update/delete protection is enforced by `AppDbContext`; PostgreSQL
  roles or triggers may strengthen this later.
- The disposable development database must be recreated from the new single
  `InitialCreate` baseline. There is no legacy backfill or opening balance.

## Implementation guidance

Acquire locks in this global order: user, canonical friendship, farm, ordered
plots, inventory, inventory items, then completion/ledger rows. Callers own the
transaction and commit. They must not apply a terrain, item or reward again
when the service returns `Replayed`.

The accounting order is `LedgerSequence`, not `CreatedAt` or the transaction
GUID. Event tokens are explicit persisted strings and are not derived from enum
names. Fingerprints encode semantic fields with length-prefixed UTF-8 and
integer, invariant-culture numeric representations.

## Validation

For each user:

```text
SUM(Amount) == Inventory.PremiumCoins
latest BalanceAfter by LedgerSequence == Inventory.PremiumCoins
each BalanceBefore == the previous BalanceAfter
```

Integration tests cover credit, debit, insufficient funds, replay, conflicting
keys, external-reference concurrency, reversals, immutable snapshots,
transaction rollback and concurrent overspend.

## Related documents

- `docs/architecture/README.md`
- `docs/gameplay/README.md`
- `docs/decisions/ADR-0001-backend-authority.md`
