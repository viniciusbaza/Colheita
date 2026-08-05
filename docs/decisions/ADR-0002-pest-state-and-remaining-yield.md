# ADR-0002 — Pest State and Authoritative Remaining Yield

Status: Accepted, amended 2026-08-01

Date: 2026-07-30

Decision owners:

- Game Director;
- Technical Planner;
- Backend Engineer;
- Architecture Reviewer.

## Context

The first pest prototype allows a caterpillar to reduce the production of a
ready crop. Harvesting, theft and pest consumption can therefore mutate the
same `RemainingYield` concurrently.

Before this decision, planting and theft wrote `Plot.RemainingYield`, but farm
loading and harvesting reconstructed the value from `Seed.CropAmount` and
`TheftLog`. That model cannot represent a non-theft loss and creates multiple
sources of truth.

The prototype also needs one infestation per planted crop, timed protection
that survives crop replacement and farm-wide limits between infestations.

The reward added for manual caterpillar removal introduces a narrower
historical requirement: the server must identify one occurrence across
retries, grant the actor's economy at most once and enforce a rolling reward
limit even after the plot is replanted.

## Decision drivers

The solution must:

- preserve backend authority;
- prevent duplicate pest consumption;
- preserve at least one unit for the owner;
- keep harvesting, theft and pest damage consistent;
- support owner and accepted-friend actions;
- process deadlines without requiring a background worker;
- keep protection attached to the plot rather than the crop;
- avoid speculative generic infestation history while retaining the minimal
  reward audit required for idempotency and rate accounting;
- remain compatible with PostgreSQL and EF Core.

## Options considered

### Option A — Reconstruct yield from event logs

Continue deriving production from the original seed amount and append a new
kind of pest log.

Advantages:

- retains an event-based calculation;
- supports historical reconstruction.

Disadvantages:

- every loss source must participate in every calculation;
- harvesting and farm loading can diverge;
- concurrent events still require serialization;
- a generic event ledger is larger than the MVP requires;
- existing `RemainingYield` continues to have ambiguous meaning.

### Option B — Separate infestation entity

Persist each infestation as a related entity and keep protection on the plot.

Advantages:

- provides durable infestation history;
- naturally supports multiple pests and occurrences later.

Disadvantages:

- adds a table, relationship and lifecycle not queried by the MVP;
- still requires the plot row to protect `RemainingYield`;
- encourages designing future multi-pest behavior before it is approved.

### Option C — Persist one cycle state on the plot

Make `Plot.RemainingYield` authoritative and persist the single allowed
infestation directly on the plot. Keep `ProtectedUntil` outside the crop-cycle
reset and store the last farm-wide appearance on the farm.

Advantages:

- one source of truth for the current harvest;
- one row contains the state that is serialized with the yield;
- simple one-infestation-per-cycle invariant;
- protection naturally survives harvest and replanting;
- no speculative history table.

Disadvantages:

- terminal state is overwritten on a later planting;
- querying historical infestations will require a future event/log model;
- farm processing may lock several plot rows to enforce a farm-wide cap.

## Decision

Use Option C.

`Plot.RemainingYield` is the only authoritative amount for a planted crop:

- planting initializes it;
- theft and pest consumption reduce it;
- harvesting awards it;
- harvesting clears it.

`TheftLog` remains an audit and limit ledger. It does not reconstruct the
current yield.

The plot persists one pest type, an explicit pest status, scheduling and
resolution timestamps, the consumed amount, a stable `PestOccurrenceId` and
`ProtectedUntil`.

A pest status other than `None` proves that the current crop cycle has already
received its single infestation. Planting resets pest-cycle fields but does not
clear an unexpired protection timestamp.

The farm persists the last infestation appearance needed to enforce the
configured interval.

A narrow `PestRemovalCompletion` ledger records successful manual removals.
It is not the source of pest state or yield and does not model cancelled or
consumed infestations. Its responsibilities are limited to exactly-once
rewarding, idempotent HTTP replay, rolling actor limits and economic audit.

The manual-removal command must carry the `PestOccurrenceId` observed by the
client. The backend compares it under the plot lock before resolving the pest.
The idempotent completion is keyed to that requested occurrence rather than to
whichever cycle happens to occupy the plot when a delayed request arrives.

Time is materialized on relevant requests. A farm or plot interaction captures
one UTC instant, locks the farm and affected plots in a stable order, processes
expired state and commits before returning the authoritative projection.

Operations that also change an actor's economy acquire locks in this order:

```text
actor user
→ farm
→ plots in stable order
→ inventory and item
```

PostgreSQL row locks serialize harvesting, theft, pest consumption and
protection. The actor lock additionally serializes the global manual-removal
reward limit across farms. Reprocessing a terminal infestation is a no-op.

## Consequences

### Positive

- pest damage cannot disappear during farm synchronization;
- harvesting always awards the current persisted production;
- the owner-protection floor is shared by theft and pests;
- duplicate consumption and item debit can be prevented transactionally;
- duplicate manual-removal rewards and retries are durably prevented;
- React and Phaser continue to receive authoritative state;
- the MVP does not require a worker or new production dependency.

### Negative

- selected GET requests now persist time-based transitions;
- farm-wide locking increases contention;
- generic infestation history is not retained after a later planting;
- adding multiple simultaneous pest types will require revisiting the model.

### Neutral or operational

- notifications may record player-visible history without becoming the yield
  source;
- the removal ledger intentionally retains only the facts required for reward
  audit and does not replace the current plot state;
- feature configuration controls timing, damage, cap, interval and protection;
- disabling the feature stops scheduling, activation, consumption, catalog
  sales and protection application without requiring an immediate schema
  rollback; manual removal remains available for an already-active state;
- a future background worker must reuse the same transition service and locks.

## Implementation guidance

- Never derive current yield from theft logs.
- Never accept yield, damage, price, duration, timestamp or actor identity from
  the client.
- Process an already expired active pest before harvest, theft or protection.
- Do not consume an item when protection is already valid and stacking is not
  allowed.
- Keep protection independent from theft permission.
- Keep terminal transition reasons explicit until the next planting.
- Use additive API fields and let `farm:sync` replace browser state.
- Do not expand `PestRemovalCompletion` into a generic infestation event log
  until a product requirement needs full history, multiple occurrences or
  analytical queries.

## Validation

Automated tests must verify:

- harvest uses persisted `RemainingYield`;
- repeated processing consumes once;
- theft and pests preserve at least one unit;
- concurrent harvest, theft and consumption produce one consistent result;
- concurrent protection consumes at most one item;
- protection survives harvest and replanting;
- farm synchronization never restores a lost unit.

Architecture review must reject any implementation that:

- recalculates current yield from only one event source;
- lets React or Phaser confirm a pest transition;
- changes yield without the plot lock;
- debits a visitor's or owner's inventory outside the protection transaction.

## Related documents

- `docs/decisions/ADR-0001-backend-authority.md`
- `docs/gameplay/README.md`
- `docs/architecture/README.md`
- `docs/plans/2026-07-30-pest-system-mvp.md`
