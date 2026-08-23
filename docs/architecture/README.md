# Farm & Friends — Architecture Overview

This document describes the current technical architecture of Farm & Friends.

It defines system boundaries, responsibilities, data flow and technical
invariants.

Gameplay rules belong in `docs/gameplay/`.

The reasoning behind major technical choices belongs in `docs/decisions/`.

## Document status

Status: Active

Last reviewed: 2026-08-18

## System overview

Farm & Friends is a web-based social farming game.

The application is divided into:

```text
Browser
├── React application
│   ├── routing
│   ├── authentication session
│   ├── API client
│   ├── application state
│   ├── HUD
│   ├── panels
│   └── modals
│
└── Phaser game
    ├── farm scene
    ├── plots
    ├── crop sprites
    ├── world input
    ├── animations
    └── visual effects

HTTP API
└── ASP.NET Core backend
    ├── authentication
    ├── authorization
    ├── gameplay rules
    ├── services
    ├── persistence
    └── API contracts

Database
└── PostgreSQL
    ├── users
    ├── farms
    ├── plots
    ├── seeds
    ├── inventories
    ├── inventory items
    ├── crop care completions
    ├── visitor-farm care cycles
    └── notifications and gameplay logs
```

## Technology stack

### Frontend

* Vite;
* React;
* TypeScript;
* TailwindCSS;
* React Router;
* Phaser 3.

### Backend

* ASP.NET Core;
* .NET 8;
* Entity Framework Core 8;
* JWT authentication;
* PostgreSQL.

## Architectural principles

### Backend authority

The backend is authoritative for:

* authenticated user identity;
* ownership;
* permissions;
* inventory;
* currencies;
* XP;
* levels;
* prices;
* rewards;
* RemainingYield;
* growth timing;
* action limits;
* persistent gameplay outcomes.

The client may display or predict presentation state, but it cannot confirm a
gameplay outcome.

### One authoritative state

Persistent gameplay state originates from the backend.

React owns the browser application's authoritative client-side representation.

Phaser renders the farm world from state supplied by React.

Phaser must not maintain a permanent independent version of gameplay state.

### Clear UI and world boundaries

React controls application interface and state.

Phaser controls the interactive farm world.

Communication occurs through documented events or an explicit bridge.

### Small vertical slices

Features should be implemented as small end-to-end slices that include only the
required:

* domain behavior;
* API contract;
* frontend state;
* interface;
* world feedback;
* tests.

Avoid building speculative infrastructure for unapproved future systems.

## Backend architecture

The backend currently contains or is expected to contain:

* domain entities;
* EF Core DbContext and mappings;
* controllers;
* application or domain services;
* DTOs;
* authentication services;
* migrations.

Exact folder names must be discovered from the repository.

Documentation must not assume a folder exists when the implementation uses a
different structure.

## Backend request flow

A typical authenticated mutation follows this flow:

```text
HTTP Request
    ↓
JWT authentication
    ↓
Controller
    ↓
Authenticated user resolved from claims
    ↓
Resource and authorization validation
    ↓
Domain/application service
    ↓
EF Core persistence
    ↓
Database transaction
    ↓
Explicit API response
```

The client does not provide an authoritative authenticated user ID.

## Controllers

Controllers are responsible for:

* transport input;
* authentication context;
* endpoint-level validation;
* service invocation;
* response mapping;
* HTTP status codes.

Complex gameplay calculations should live in services when they are reusable,
stateful or independently testable.

## Services

Services should represent meaningful application or gameplay concepts.

Examples include:

* experience granting;
* theft resolution;
* plot care resolution;
* pest scheduling, deadline processing, protection and resolution.

Services must preserve domain invariants and should not duplicate the same rule
across multiple controllers.

`Plot.RemainingYield` is the persisted source of truth for current production.
It must not be reconstructed from theft logs during farm loading or harvest.
`TheftLog` remains an audit and theft-limit ledger.

## Persistence

Entity Framework Core maps persistent entities to PostgreSQL.

Migrations are used when the persistent model changes.

Migrations are not required for:

* DTO-only changes;
* frontend changes;
* computed presentation values;
* local refactors without schema impact.

Destructive migrations require explicit approval and a data compatibility plan.

## Transaction-sensitive operations

The following operations may require transactions or concurrency protection:

* planting;
* harvesting;
* stealing;
* plot care and its coin and XP rewards;
* buying;
* selling;
* granting rewards;
* reducing RemainingYield;
* pest scheduling, consumption, removal and protection;
* future reward claims.

The system must prevent:

* duplicate rewards;
* negative inventory;
* negative currency;
* negative RemainingYield;
* simultaneous harvest of the same crop;
* simultaneous theft that violates owner protection;
* duplicate care completion or reward for one idempotent request;
* partially completed transactions.

## API contracts

API responses should use explicit DTOs when exposing entities directly would
create unstable or unsafe contracts.

A contract change must identify:

* endpoint;
* method;
* authentication;
* request;
* response;
* status codes;
* affected frontend types;
* affected consumers.

Prefer additive changes unless a breaking change is explicitly approved.

## Frontend architecture

The React application is responsible for:

* routes;
* authenticated session;
* API requests;
* user state;
* farm state;
* visit session;
* inventory state;
* HUD;
* panels;
* modals;
* application-level feedback.

Frontend modules may include:

* API request functions;
* feature hooks;
* contexts;
* view components;
* utility functions;
* Phaser bridge code.

Exact paths should be confirmed from the repository.

## Frontend data flow

A typical query flow is:

```text
React component
    ↓
Feature hook or context
    ↓
API request function
    ↓
Backend endpoint
    ↓
Typed response
    ↓
React state update
    ↓
Component rendering
    ↓
Optional Phaser synchronization
```

A typical gameplay mutation is:

```text
Player intent in React or Phaser
    ↓
React action handler
    ↓
API request
    ↓
Backend validation and mutation
    ↓
Confirmed response
    ↓
React state update
    ↓
HUD and panel update
    ↓
Phaser visual update
```

The UI must not present an attempted action as successful before confirmation
unless a safe rollback strategy is explicitly implemented.

## React responsibilities

React controls:

* navigation;
* API calls;
* application state;
* HUD;
* panels;
* modals;
* shop;
* friends;
* inventory;
* notifications;
* visiting session;
* loading and error feedback.

React should not directly manipulate internal Phaser objects.

## Phaser responsibilities

Phaser controls:

* farm scene;
* isometric coordinates;
* plot objects;
* crop sprites;
* pointer input in the farm;
* animation;
* world-space text;
* glows;
* badges;
* visual transitions.

Phaser does not determine:

* permissions;
* rewards;
* inventory changes;
* currency changes;
* XP;
* final yield;
* action limits.

## React–Phaser communication

React and Phaser communicate through documented events or the established bridge.

Current event concepts include:

| Event               | Direction      | Meaning                                  |
| ------------------- | -------------- | ---------------------------------------- |
| `plot:click`        | Phaser → React | Player selected a plot                   |
| `plot:harvest:done` | React → Phaser | Harvest confirmed                        |
| `plot:steal:done`   | React → Phaser | Theft confirmed                          |
| `plot:care:done`    | React → Phaser | Plot care confirmed                      |
| `farm:sync`         | React → Phaser | Authoritative farm state synchronization |
| `farm:change`       | React → Phaser | Active farm changed                      |
| `ui:modal`          | React → Phaser | Blocking UI opened or closed             |

These names and payloads must be verified against the current code before being
changed.

## Event categories

Events should be classified as:

### Intent

The player attempted an action.

Example:

```text
plot:click
```

An intent does not prove success.

### Confirmed result

The backend accepted an action.

Examples:

```text
plot:harvest:done
plot:steal:done
plot:care:done
```

### State synchronization

Authoritative state is supplied to the scene.

Examples:

```text
farm:sync
farm:change
```

### UI control

Application UI controls world behavior.

Example:

```text
ui:modal
```

## Event contract requirements

Every event must document:

* name;
* sender;
* receiver;
* payload type;
* semantic category;
* firing condition;
* cleanup responsibility.

Events must not carry mutable Phaser objects.

Prefer stable identifiers and serializable payloads.

## Land expansion contract and concurrency

Land expansion is a server-authoritative economic mutation. The canonical
3×3 and 7-column × 4-row coordinate sets, unlock order, level gates and prices
are resolved by backend rules. React and Phaser never select the next plot or
derive a price from coordinates.

`GET /farms/my` adds an optional `landOffer` containing the next plot ID,
progress number, minimum level, both server prices and the optional 7×4
transition. A public farm response has no offer, but includes every persisted
plot in the owner's current topology. Visitors therefore see all 9 plots of a
3×3 farm or all 28 plots after the 7×4 expansion, including their authoritative
`unlocked` state, without receiving prices or a purchase action.

Purchases use:

```http
POST /plots/{plotId}/purchase
Idempotency-Key: <uuid>
Content-Type: application/json

{ "paymentCurrency": "coins" }
```

Only the choice between `coins` and `premiumCoins` is client input. The
authenticated buyer, farm ownership, current offer, level, amount, balance and
topology are loaded and revalidated by the backend under lock.

The transaction owns:

```text
Buyer User lock
-> Farm lock
-> stable Plot locks
-> current-offer validation
-> Inventory lock
-> one currency debit
-> one plot unlock
-> optional insertion of 19 locked plots
-> LandPurchaseCompletion
-> Commit
```

`LandPurchaseCompletion` is the durable gameplay completion and idempotency
record. `(BuyerUserId, IdempotencyKey)`, `PlotId` and
`(FarmId, PlotNumber)` are unique. Composite foreign keys prove that the buyer
owns the recorded farm and that the recorded plot belongs to that same farm.
Checks restrict currency, positive spend, non-negative post-purchase balances
and expansion metadata: plot 9 must record exactly 19 added plots and 7×4;
every other purchase must record no topology change. A retry of the same plot
and payment currency replays the persisted result; reusing the key for another
command is rejected. The coordinate index `(FarmId, X, Y)` prevents a
concurrent 3×3-to-7×4 transition from creating duplicate plots.

Premium payment additionally creates a `PremiumCurrencyTransaction` linked
one-to-one from the completion. Its persisted key is
`land-purchase:{request UUID}`. A replay must find both records linked and does
not debit, unlock or expand again. Coin payment remains outside the premium
ledger.

## Premium currency ledger

`Inventory.PremiumCoins` is the materialized current balance. It is never
reconstructed on normal requests. Every production write to that property goes
through `PremiumCurrencyService`, which requires a caller-owned EF transaction
and locks the user's inventory after the established user/gameplay locks.

Each movement creates one ordered `PremiumCurrencyTransaction`. Accounting
uses its database-generated `LedgerSequence`; timestamps are informational.
The persisted event is an explicit stable token. The operation fingerprint is
SHA-256 over length-prefixed semantic fields, using invariant integer
representations and canonical item ordering.

Namespaced keys are unique per user. External provider/environment plus
transaction ID is globally unique. Identical retries return `Replayed`;
different semantics return conflict. Consequences of gameplay execute only for
`Applied`. Premium purchase items are immutable snapshots with no catalog FK.
Corrections create one integral compensating transaction linked through unique
`ReversesTransactionId`.

The application rejects update/delete of ledger rows in `AppDbContext`; this
is application-level immutability, not protection from direct DBA access.
Reconciliation verifies transaction sum, latest balance, and continuity
against `Inventory.PremiumCoins`.

The current development baseline creates the final schema, constraints,
indexes and seed catalog directly. It supports only the canonical 3×3 and 7×4
layouts and does not contain upgrade paths for discarded prototype layouts.
`LandPurchaseCompletion.PlotNumber` is the purchase ordinal in the single
canonical sequence defined by `FarmLayoutRules`.

Future persistent-model changes use incremental migrations from this baseline.
Resetting disposable development data recreates the PostgreSQL volume and
reapplies the versioned migrations; it never regenerates migration history.

React keeps one idempotency key for the active `plotId + paymentCurrency`
attempt. The detailed purchase panel requires an explicit payment-route choice,
then its `Buy` action starts the POST without an intermediate review screen. A
successful POST first enters a reconciliation state; success is shown and the
key is discarded only after
an authoritative farm snapshot contains the purchased plot as unlocked. A
failed farm refresh retains the key for an idempotent replay. Inventory is
refreshed best-effort after the farm is reconciled. A transition closes the old
anchored modal before the 7×4 snapshot is synchronized and reports progress or
failure outside that modal.

Phaser preloads one sale-sign texture and derives its presence exclusively from
`farm.landOffer.plotId`. `farm:sync` creates, moves or removes the sprite
idempotently; a topology change uses the existing scene restart path. The sign
is static, keeps using the plot's existing `plot:click` intent and introduces
no new bridge event or direct API call.

## Plot care contract and timing

Plot care is a server-authoritative mutation exposed by:

```text
POST /farms/{farmId}/plots/{plotId}/care
```

The request sends the current `careOpportunityId` in the body and a UUID
`Idempotency-Key` header. The authenticated visitor identity, friendship,
eligibility, timestamps and rewards are resolved by the backend.

Farm plot responses expose a care projection with:

* `opportunityId`, the identity of the currently planted crop's care
  opportunity;
* `canCare`, the server decision for the visitor and plot;
* `rewardAvailable`, the server decision for a rewarded action in the active
  visitor-farm cycle;
* `viewerCared`, whether the current visitor already cared for the opportunity;
* `nextCareAt`, the end of that visitor's cooldown for the current opportunity;
* `careCycleEndsAt`, the end of the active reward cycle scoped to the visitor
  and farm;
* `caregiverCount` and `caregivers`, the attributed owner-facing summary.

The two care deadlines have different meanings:

| Field             | Scope                       | Frontend use |
| ----------------- | --------------------------- | ------------ |
| `nextCareAt`      | visitor + care opportunity  | Countdown after the visitor cared for that growing crop |
| `careCycleEndsAt` | visitor + farm reward cycle | Countdown when a plot cannot be rewarded again until the current farm cycle ends |

React may format these server timestamps into a live countdown, but it must not
turn care on locally when a countdown reaches zero. The farm hook schedules a
server refresh at the earliest relevant crop or care deadline, with the normal
polling fallback retained.

Phaser shows the water-drop badge only when `canCare` and `rewardAvailable` are
both true. It does not show a completion badge. After the POST succeeds, React
updates authoritative application state and emits the confirmed-result event:

| Property | Contract |
| -------- | -------- |
| name | `plot:care:done` |
| sender | `PlotModal` in React |
| receiver | `FarmScene` in Phaser |
| payload | `plotId`, `coinsGained`, `xpGained`, `caredByUsername` |
| firing condition | Only after the backend confirms care |
| cleanup | `FarmScene` removes the listener during scene shutdown |

The scene removes the water-drop badge, plays the care animation, displays the
server-returned coin reward and then displays the server-returned XP reward.

Persistent care accounting uses:

* `CropCareCompletions` as the durable per-action and idempotency ledger;
* `VisitorFarmCareCycles` for five-hour visitor-and-farm reward cycles and the
  rolling reward quota;
* the existing notifications store for owner-facing attributed care messages.

The current runtime values are bound from the `CropCare` section of
`FarmAndFriends.Api/appsettings.json`:

| Setting | Value | Definition |
| ------- | ----- | ---------- |
| `VisitorPlotCareCooldownHours` | 5 | Minimum interval for the same visitor to care for the same current crop opportunity again |
| `VisitorFarmRewardCycleHours` | 5 | Duration of one reward cycle scoped to `VisitorUserId + FarmId` |
| `VisitorFarmRewardRollingWindowHours` | 24 | Rolling window used to count rewarded visitor-farm cycles |
| `MaxRewardedCyclesPerVisitorFarmWindow` | 5 | Maximum rewarded cycle starts for the same visitor and farm inside the rolling window |
| `OwnerNotificationDeduplicationWindowHours` | 24 | Deduplication window for care notifications from the same visitor to the same owner |
| `CoinsReward` | 2 | Standard coins granted for each eligible rewarded plot |
| `XpReward` | 5 | XP granted for each eligible rewarded plot |

Property initializers in `CropCareOptions` are fallback values. The bound
runtime configuration above is the source for the currently documented
experiment.

## Pest contract, time and concurrency

The pest prototype is exposed through server-authoritative farm projections
and mutations:

```text
GET  /farms/my
GET  /farms/{farmId}
POST /farms/{farmId}/plots/{plotId}/pest/remove
POST /farms/{farmId}/plots/{plotId}/pest/protection
GET  /shop/items
POST /shop/buy-item
```

Manual removal requires both a UUID `Idempotency-Key` request header and the
occurrence observed by the player:

```json
{
  "pestOccurrenceId": "00000000-0000-0000-0000-000000000000"
}
```

Under the actor, farm and plot locks, the backend requires this value to match
the occurrence being removed. A delayed request can therefore never resolve a
new caterpillar that later appeared on the same plot.

The response keeps the authoritative plot fields and adds:

```text
completionId
pestOccurrenceId
coinsGained
xpGained
coins
rewardGranted
replayed
```

`replayed` lets React reconcile a retry without incrementing shared XP or
replaying the reward animation. A retry is matched to the completion by actor,
key, target and requested occurrence; it remains reproducible after the plot
has moved to another planting cycle. The protection response remains a
separate contract and does not expose reward fields.

Farm plot responses add:

* `protectedUntil`;
* a nullable `pest` projection containing `occurrenceId`, `type`, `status`,
  scheduling,
  appearance, consumption and resolution timestamps, `consumedAmount` and
  `canRemove`.

Farm responses also add `nextPestCheckAt`. This is the server-computed
`ReadyAt + SafetyPeriod` deadline for the next crop that still needs its first
pest evaluation. React schedules a refresh after this deadline instead of
duplicating the safety-period rule.

The persisted status lifecycle is:

```text
None
→ Scheduled
→ Active
→ Removed | Consumed | CancelledByTheft
           | CancelledByHarvest | CancelledByProtection
```

One status other than `None` is also the one-infestation marker for the current
planting cycle. `PestOccurrenceId` provides stable correlation for that
infestation until the next planting resets the pest-cycle fields. Planting
preserves `ProtectedUntil`.

`PestService` captures one UTC time per operation and lazily materializes
elapsed state while loading a farm or before theft, harvest, removal or
protection. A late first observation starts a full reaction window at actual
activation; an already persisted and expired active infestation consumes at
most once.

The backend serializes mutations with PostgreSQL row locks in this order:

```text
actor User, when actor economy changes
→ Farm
→ all Farm plots ordered by Id
→ Inventory and InventoryItem
→ SaveChanges and commit
```

Farm-wide locking is intentional for the MVP because the active cap and minimum
appearance interval are farm-level invariants. It also serializes theft,
harvest and pest consumption against the same authoritative RemainingYield.

Theft keeps `Scheduled` unchanged and cancels only `Active`. Harvest first
processes an elapsed active deadline, then cancels any still-pending
infestation. Applying protection also processes elapsed state before debiting
the actor's item, so protection does not retroactively undo damage.

Theft returns an explicit `pestCancelled` boolean. React and Phaser may show
the caterpillar-scared feedback only from this confirmed result; the presence
of a local sprite is not an authorization or transition decision.

Manual removal also changes the actor economy and therefore always begins by
locking the actor. In the same transaction it transitions the active
occurrence, credits the inventory and XP, creates the owner notification when
applicable and inserts one `PestRemovalCompletion`. The completion is unique
by pest occurrence and by `ActorUserId + IdempotencyKey`.

The rolling reward limit counts only completions with positive gains whose
`RemovedAt` falls inside the configured window. A completion with zero gains
is still persisted after the limit, so the crop can always be helped without
extending the reward window.

React includes ready, care, pest and protection deadlines when scheduling the
next server refresh and retains fallback polling. It may format countdowns but
does not confirm a transition locally.

Successful remove, protection and theft responses patch only the fields
explicitly confirmed by the backend and immediately emit `farm:sync`; the next
farm GET remains the full reconciliation. Farm requests use a monotonic
sequence so an older response or a response from a previous visit cannot
overwrite newer state.

Phaser creates the caterpillar only for server-returned `Active`, shows a
small animated glyph without obscuring the crop, and updates yield feedback
from the next `farm:sync`. Protection is described in `PlotModal`; it does not
add a second floating plot badge. A confirmed `plot:pest:remove:done` event
carries `pestOccurrenceId` and backend-returned reward values from React to
Phaser. The occurrence correlation prevents a late retry from hiding a newer
caterpillar. React emits this world-feedback event only for a new completion,
after closing the plot modal; an idempotent replay only reconciles shared state
and displays a synchronization message. Phaser never calls the pest endpoint
or calculates a reward.

Runtime values are bound from `Pests` in
`FarmAndFriends.Api/appsettings.json`:

| Setting | Initial value | Definition |
| ------- | ------------: | ---------- |
| `Enabled` | `false` | Fail-closed kill switch in the base configuration |
| `SafetyPeriodMinutes` | 1 | Delay after crop readiness in the current development prototype |
| `ReactionWindowMinutes` | 5 | Full window after actual appearance in the current development prototype |
| `DamageAmount` | 1 | Maximum units consumed by the MVP caterpillar |
| `MaxActivePestsPerFarm` | 9 | Concurrent active cap |
| `MinimumInfestationIntervalMinutes` | 1 | Minimum interval between appearances |
| `ProtectionDurationHours` | 4 | Protection applied by one item |
| `NaturalRepellentBuyPrice` | 30 | Standard-currency catalog price |
| `NaturalRepellentMinLevel` | 1 | Minimum purchase level |
| `RemovalCoinsReward` | 2 | Standard coins for a rewarded manual removal |
| `RemovalXpReward` | 5 | XP for a rewarded manual removal |
| `RemovalRewardRollingWindowHours` | 24 | Rolling actor reward window |
| `MaxRewardedRemovalsPerWindow` | 15 | Global rewarded removals per actor and window |

`appsettings.Development.json` overrides `Enabled` to `true`. When the switch
is off, the backend does not schedule, activate or consume pests and does not
expose, sell or apply the repellent. Manual removal remains available so an
already-active state can still be resolved safely, but creates a zero-gain
completion while the switch is off.

Pest action failures use RFC 7807 `ProblemDetails` with stable codes:

```text
PEST_FEATURE_DISABLED
PEST_FARM_NOT_FOUND
PEST_PLOT_NOT_FOUND
PEST_FORBIDDEN
PEST_NOT_ACTIVE
PEST_ALREADY_PROTECTED
PEST_INVENTORY_NOT_FOUND
PEST_ITEM_UNAVAILABLE
PEST_IDEMPOTENCY_KEY_REQUIRED
PEST_IDEMPOTENCY_KEY_REUSED
PEST_OCCURRENCE_ID_REQUIRED
PEST_OCCURRENCE_MISMATCH
```

The storage decision and authoritative-yield rationale are recorded in
`docs/decisions/ADR-0002-pest-state-and-remaining-yield.md`.

## Farm session

The frontend distinguishes between:

* the player's own farm;
* a visited friend's farm.

The farm session should contain enough information to identify:

* mode;
* farm ID;
* owner user ID;
* owner username.

Actions available to the player depend on this session, but backend authorization
remains mandatory.

## Authentication

The frontend stores and sends the authentication token according to the current
authentication implementation.

The backend validates the token and resolves the authenticated user.

The presence of a button or route in the frontend does not prove authorization.

Sensitive tokens must not be logged.

## Error handling

Backend failures should be translated into safe player-facing feedback.

The client should distinguish when practical between:

* authentication failure;
* forbidden action;
* missing resource;
* invalid gameplay state;
* insufficient inventory;
* insufficient currency;
* conflict;
* unavailable service.

Phaser must not keep successful visuals when the server rejected the action.

## Testing strategy

### Backend tests

Cover:

* domain rules;
* authorization;
* inventory;
* currency;
* XP;
* yield;
* time;
* concurrency-sensitive operations;
* endpoint contracts.

### Frontend tests

Cover:

* hooks;
* contexts;
* API mapping;
* loading;
* errors;
* modal behavior;
* own versus visiting mode;
* independent row state;
* React–Phaser event integration.

### Manual gameplay validation

Use manual validation when necessary for:

* isometric positioning;
* scene input;
* animations;
* visual growth;
* badges;
* modal click-through;
* desktop and mobile behavior.

Manual validation must be reported honestly and must not be described as
automated coverage.

## Security invariants

The architecture must prevent:

* client-selected rewards;
* client-selected prices;
* client-selected timestamps;
* client-selected authenticated identity;
* unauthorized farm mutation;
* duplicate rewards;
* negative resource balances;
* access to private farm information;
* token or secret leakage.

## Architectural change process

A change requires an architecture review when it:

* moves responsibility between backend, React and Phaser;
* introduces a new persistent entity;
* changes authentication or authorization;
* changes event communication;
* adds a major dependency;
* creates a new integration pattern;
* alters transaction or concurrency behavior;
* changes a public API contract substantially.

The decision should be recorded in `docs/decisions/` when it has long-term
consequences.

## Known architectural risks

Track active risks here until they are resolved or moved to a dedicated document.

Current risks may include:

* duplicated farm state between React and Phaser;
* event listeners without lifecycle cleanup;
* concurrent theft or harvest requests;
* API contracts represented by duplicated manual types;
* gameplay rules remaining inside controllers;
* incomplete automated coverage for Phaser flows.

Only list a risk as current after confirming it in the repository.
