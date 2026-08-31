# ADR-0004 — Multi-harvest crop lifecycle

Status: Accepted

Date: 2026-08-23

Decision owners:

- Game Director;
- Social Designer;
- Economy Designer;
- Technical Planner;
- Architecture Reviewer.

## Context

The original plot lifecycle assumes that every harvest removes the planted
crop. That prevents strawberries, vines and fruit trees from producing again
without being planted again, and makes future tree-specific logic likely to
leak into the generic crop flow.

The existing `Plot.RemainingYield`, pest state, care opportunity and server
deadlines already describe one production window. The new lifecycle must reuse
those concepts without turning the browser into an authority or introducing a
speculative production ledger.

## Decision drivers

The solution must:

- keep single-harvest crops behavior-compatible;
- model multi-harvest crops generically rather than as trees;
- distinguish initial growth from later production time;
- grant yield and harvest XP once per completed cycle;
- make theft, pests and care independently available in each cycle;
- serialize every economic transition under the existing plot locks;
- keep the UI simple for ordinary seeds;
- avoid an infinite or per-cycle-configuration model in V1.

## Options considered

### Option A — Tree-specific state

Add flags and branches for trees. This is small initially, but it cannot
represent strawberries or vines and spreads special cases across all layers.

### Option B — Persist production-window entities

Create a child record for every cycle. This provides history, but no current
requirement queries that history. It duplicates the plot's authoritative state.

### Option C — Extend Seed and Plot lifecycle state

Configure total cycles and regrowth time on `Seed`; persist only the active
cycle and its start on `Plot`; reuse `RemainingYield`, pest and care state for
the current production window.

## Decision

Use Option C.

`Seed` persists `CropName`, `HarvestCycles` and nullable `RegrowTime`.
`Plot` persists nullable `CurrentHarvestCycle` and
`CurrentHarvestCycleStartedAt` while retaining `PlantedAt` as the original
planting time. `IsPerennial` is not persisted; V1 presentation derives the
label from `HarvestCycles > 1`.

The active cycle is one-based. Planting starts cycle 1 with `GrowTime`. A
non-final harvest grants the current persisted yield and XP, then atomically:

- advances the cycle by one;
- starts a new deadline with `RegrowTime`;
- resets `RemainingYield` to `CropAmount`;
- resets the pest cycle while preserving plot protection;
- starts a new care opportunity.

The final harvest grants the same rewards and clears the planted-crop state.
Time alone never skips or accumulates cycles.

`RemainingYield` means production remaining in the current cycle, not across
the lifetime of the plant. Theft and pest loss never carry into the following
cycle. The existing actor, farm, ordered-plots and inventory lock order remains
mandatory. `ExpectedHarvestCycle` is a client precondition for multi-harvest
crops, never a source of authority.

## Consequences

### Positive

- trees, vines and other recurring crops share one lifecycle;
- existing crops follow the same code path with one cycle;
- each production window naturally supports theft, pests and care;
- `PlantedAt` retains historical meaning while cycle timing is unambiguous;
- React and Phaser continue to reconcile from backend state.

### Negative

- yield and XP multiply across a plant's lifetime and require monitoring;
- catalog changes cannot reduce cycles below an active plot's current cycle;
- rollback after activating recurring crops requires a data plan;
- V1 has no manual early removal for an occupied recurring crop.

### Neutral or operational

- no `IsPerennial`, crop-cycle table, harvest ledger, trigger or new index;
- `ProtectedUntil` remains plot-scoped and survives all crop cycles;
- Phaser composes fixed Plot ground and independently scaled crop sprites;
- crop presentation uses `sprout`, fruitless `mature` and `ready`; the same
  `mature` art represents late initial growth and later regrow periods;
- a display-only Phaser timer advances the 25% and 50% initial-growth visuals,
  while readiness still requires authoritative synchronized state;
- `farm:sync` chooses the final Phaser state; harvest events remain feedback;
- clients older than the coordinated contract must not receive recurring crops.

## Implementation guidance

- Keep cycle transitions in the pure `CropCycleRules` boundary.
- Validate seed configuration in domain code and database constraints.
- Capture one UTC `now` per mutation.
- Process elapsed pest state before awarding harvest yield.
- Never derive current yield from theft or pest logs.
- Do not display recurring-crop details for `HarvestCycles = 1`.

## Validation

Automated tests cover one-cycle compatibility, the complete Macieira and Tomate
lifecycles, per-cycle yield/pest/care reset, protection preservation, stale
requests, overflow and concurrent harvest/theft/pest mutations. PostgreSQL
tests validate both a fresh schema and upgrade/backfill behavior.

## Related documents

- `docs/decisions/ADR-0001-backend-authority.md`
- `docs/decisions/ADR-0002-pest-state-and-remaining-yield.md`
- `docs/gameplay/README.md`
- `docs/architecture/README.md`
- `docs/plans/2026-08-23-multi-harvest-crops.md`
