# Farm & Friends — Architecture Overview

This document describes the current technical architecture of Farm & Friends.

It defines system boundaries, responsibilities, data flow and technical
invariants.

Gameplay rules belong in `docs/gameplay/`.

The reasoning behind major technical choices belongs in `docs/decisions/`.

## Document status

Status: Active

Last reviewed: 2026-07-29

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
* crop care resolution;
* farm yield population.

Services must preserve domain invariants and should not duplicate the same rule
across multiple controllers.

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
* future pest damage;
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
