<!-- Esse arquivo deve funcionar como a constituição do Farm & Friends. -->

# Farm & Friends Backend Guidance

This file applies to all work inside `FarmAndFriends.Api/`.

The repository root `AGENTS.md` remains authoritative for product vision,
gameplay principles, delegation and project-wide engineering rules.

This file adds backend-specific guidance.

## Backend responsibilities

The backend is responsible for:

* authentication and authorization;
* users, farms and friendships;
* plots, seeds and crops;
* planting, growth and harvesting rules;
* theft rules and RemainingYield;
* pests and other future farm events;
* inventories and inventory items;
* standard and premium currencies;
* experience and player levels;
* shop transactions;
* persistent notifications and logs;
* server-side timing;
* database consistency.

The backend is the final authority for all persistent gameplay outcomes.

## Technology

The backend uses:

* ASP.NET Core on .NET 8;
* Entity Framework Core 8;
* PostgreSQL;
* JWT authentication;
* ASP.NET Core Identity password hashing.

Do not introduce a new framework, ORM, mediator, mapping library or validation
library without explaining why the existing stack is insufficient.

## Project discovery

Before editing backend code:

1. locate the solution and project files;
2. inspect the existing folder structure;
3. identify the relevant controller, entity, service and DbContext;
4. inspect existing DTOs and response shapes;
5. inspect related tests;
6. trace the current request from controller to persistence;
7. confirm whether the task changes a gameplay rule or only implementation.

Do not assume that a named service, folder or abstraction exists.

Use existing project conventions before introducing a new pattern.

## Domain authority

Never trust the client to provide authoritative values for:

* authenticated user identity;
* ownership;
* friendship or visit permission;
* seed or crop prices;
* rewards;
* XP;
* level;
* RemainingYield;
* theft quantity;
* harvest quantity;
* growth completion;
* timestamps;
* inventory totals;
* currency balances.

Derive the authenticated user from trusted JWT claims.

Load authoritative values from the database or trusted server configuration.

Client-provided identifiers may identify a requested resource, but they do not
prove ownership or permission.

## Controllers

Controllers should:

* receive and validate transport input;
* identify the authenticated user;
* load or delegate loading of the requested resource;
* call the appropriate application or domain service;
* map results to an explicit response;
* return consistent HTTP status codes.

Controllers should not contain long economic calculations or large gameplay
decision trees when that behavior belongs in a service.

Small endpoint-specific checks may remain in a controller when extracting them
would create unnecessary indirection.

Do not expose EF Core entities directly when doing so would:

* expose internal fields;
* create unstable contracts;
* create serialization cycles;
* reveal information unavailable to that player;
* couple the client to persistence details.

## Services and gameplay rules

Prefer services for rules that:

* are used by multiple endpoints;
* involve multiple entities;
* calculate rewards or losses;
* modify inventory, yield, currency or XP;
* need isolated tests;
* represent named gameplay concepts.

Examples of existing concepts include:

* experience calculation;
* theft resolution;
* farm yield population.

Before creating a new service, verify that an existing service is not already
responsible for that behavior.

Do not split trivial logic across many services merely to imitate an
architectural pattern.

## Entities and invariants

Entities and services must preserve gameplay invariants.

Important current invariants include:

* a locked plot cannot be planted;
* an occupied plot cannot be planted again;
* planting consumes the required seed;
* a crop cannot be harvested before it is ready;
* a visitor cannot harvest as the owner;
* an owner cannot steal from their own farm;
* theft cannot remove the owner's entire meaningful harvest;
* RemainingYield cannot become negative;
* rewards cannot be granted twice for the same successful operation;
* inventory quantities cannot become negative;
* currency balances cannot become negative through a normal transaction;
* unauthorized users cannot mutate another player's resources.

When adding a new invariant, add or update tests that demonstrate it.

## Time handling

Use server-side UTC timestamps for persisted gameplay time.

Do not accept client clocks as authoritative.

When comparing crop readiness or event timing:

* use one captured server time for the operation;
* avoid calling the current clock repeatedly during the same calculation;
* keep persisted and returned timestamp semantics consistent.

Do not move time-based authority to React or Phaser.

## Transactions and concurrency

Treat these operations as consistency-sensitive:

* planting;
* harvesting;
* stealing;
* buying;
* selling;
* granting XP;
* changing RemainingYield;
* applying pest damage;
* claiming rewards.

When an operation changes multiple related records, ensure that it either
completes consistently or fails without leaving partial rewards.

Consider concurrent requests against the same plot or inventory.

Do not rely only on a value read earlier in the request when another request may
change that value before saving.

For concurrency-sensitive changes, explain the selected strategy, such as:

* database transaction;
* optimistic concurrency token;
* atomic conditional update;
* database constraint;
* safe retry.

Do not introduce locking or concurrency infrastructure without a concrete race
condition or invariant to protect.

## Entity Framework Core

Before changing the model:

1. identify the entity and relationships affected;
2. inspect the current DbContext configuration;
3. determine whether the change is persisted or derived;
4. assess existing data compatibility;
5. decide whether a migration is necessary.

Create a migration only when the persistent model changes.

Do not create a migration for:

* DTO-only changes;
* controller-only changes;
* computed behavior that is not persisted;
* frontend contract changes;
* renaming a local variable.

Review generated migrations before accepting them.

A migration must not silently delete or reset player data unless the task
explicitly approves that behavior.

Avoid destructive development shortcuts in implementation work unless the user
explicitly requests a database reset.

## PostgreSQL

Use types and queries that are compatible with PostgreSQL.

Do not write provider-specific SQL without confirming that EF Core cannot
express the required behavior safely.

When using raw SQL:

* parameterize values;
* document why raw SQL is needed;
* preserve transaction behavior;
* add focused tests where practical.

## Authentication and authorization

Authentication answers who the user is.

Authorization answers whether the user can perform the requested action.

Do not treat possession of a farm ID, plot ID or user ID as authorization.

For protected operations, verify all applicable conditions, including:

* authenticated user;
* resource existence;
* resource ownership;
* visitor versus owner mode;
* friendship or access rule when required;
* action-specific limits;
* current resource state.

Return an appropriate distinction between:

* unauthenticated;
* forbidden;
* not found;
* invalid state;
* conflict.

Do not reveal sensitive resource existence when the authorization design
requires concealment.

## API contracts

Preserve existing API compatibility unless a breaking change is explicitly
approved.

When changing an endpoint, document:

* route;
* HTTP method;
* authentication requirement;
* request shape;
* response shape;
* error status codes;
* fields added, removed or renamed;
* affected frontend consumers.

Prefer additive contract changes where practical.

Do not silently change the meaning of an existing field.

Use stable and consistent JSON naming.

## Error handling

Errors returned to the client should be understandable and actionable.

Do not leak:

* stack traces;
* connection strings;
* SQL details;
* JWT secrets;
* internal exception messages;
* implementation-only identifiers.

Do not use successful HTTP responses to represent failed gameplay operations.

Keep error response conventions consistent with existing endpoints.

## Logging

Log enough context to investigate failures without recording secrets.

Useful context may include:

* operation name;
* authenticated user ID;
* farm or plot ID;
* outcome;
* exception category.

Do not log:

* passwords;
* password hashes;
* access tokens;
* refresh tokens;
* JWT signing keys;
* full sensitive request bodies.

## Testing requirements

Add or update backend tests when changing:

* gameplay rules;
* authorization;
* resource ownership;
* rewards;
* inventories;
* currency;
* XP;
* RemainingYield;
* time-sensitive behavior;
* concurrency-sensitive behavior;
* API contracts.

Test both successful and rejected operations.

Important negative scenarios include:

* unauthenticated user;
* wrong owner;
* self-theft;
* missing resource;
* locked plot;
* empty plot;
* crop not ready;
* exhausted yield;
* insufficient seed;
* insufficient currency;
* repeated action;
* concurrent action when relevant.

Tests must verify resulting state, not only the HTTP status.

## Validation commands

Before declaring backend work complete:

1. locate the relevant `.sln` or `.csproj`;
2. restore dependencies when needed;
3. build the affected project;
4. run relevant focused tests;
5. run the broader backend test suite when practical;
6. inspect the final diff;
7. confirm that no unrelated migration or generated file was added.

Use the actual project paths discovered in the repository.

Typical commands may include:

```bash
dotnet restore
dotnet build
dotnet test
```

Do not claim that a command passed unless it was executed successfully.

If tests cannot run, report the exact blocker.

## Scope boundaries

Backend work must not modify React components, hooks or Phaser scenes unless the
task explicitly includes integration work and the applicable frontend guidance
has also been read.

When a backend change requires frontend work, report:

* the changed contract;
* affected consumers;
* required state changes;
* expected visual behavior.

Do not implement product, social or economic decisions that are still pending.

## Code review rules

Flag changes that:

* trust client-supplied rewards, prices, timing, ownership or user identity;
* allow duplicate rewards;
* allow negative inventory, currency or RemainingYield;
* expose private farm information to visitors;
* perform ownership checks only in the frontend;
* introduce a race condition in harvest, theft, purchase or sale;
* create destructive migrations without explicit approval;
* duplicate an existing gameplay rule in multiple controllers;
* change an API contract without updating or identifying consumers;
* persist a value that should remain derived without justification.

Prefer findings about correctness, security and data integrity over formatting
preferences.

## Definition of done

Backend work is complete only when:

* the requested server behavior is implemented;
* authorization is enforced on the server;
* invariants are preserved;
* contracts are documented;
* relevant tests pass;
* migration impact is reviewed when applicable;
* build succeeds;
* no unrelated refactor is included;
* frontend or Phaser dependencies are clearly reported.
