# ADR-0001 — Backend Authority for Persistent Gameplay

Status: Accepted

Date: 2026-01-07

Decision owners:

* Game Director
* Technical Planner
* Backend Engineer
* Architecture Reviewer

## Context

Farm & Friends includes persistent and socially competitive gameplay systems.

Examples include:

* inventories;
* currencies;
* experience;
* player levels;
* crop growth;
* harvesting;
* RemainingYield;
* crop theft;
* plot care;
* shop transactions;
* future pests and protection systems.

The frontend runs in a player's browser and can be inspected, modified or called
outside the intended interface.

If the client determines rewards, prices, timing or permissions, a player could
produce invalid requests or manipulate local state to gain resources.

The project requires one trusted authority for persistent gameplay outcomes.

## Decision drivers

The decision must:

* prevent client-controlled rewards;
* protect player inventories and currencies;
* preserve fair social interactions;
* support multiple devices and sessions;
* prevent duplicate actions;
* provide consistent gameplay state;
* keep React and Phaser focused on presentation and interaction;
* remain compatible with the current ASP.NET Core architecture.

## Options considered

### Option A — Client-authoritative gameplay

React or Phaser calculates action results and sends the final values to the
backend.

Example:

```text
Client sends:
- crop amount;
- XP gained;
- price;
- user ID;
- final RemainingYield.
```

Advantages:

* simpler initial backend endpoints;
* faster visual prototyping.

Disadvantages:

* easily manipulated;
* creates multiple sources of truth;
* makes social gameplay unfair;
* creates inconsistent state across devices;
* requires the backend to trust an untrusted environment;
* makes duplicate rewards harder to prevent.

### Option B — Shared authority

The frontend calculates some gameplay outcomes while the backend validates
others.

Advantages:

* may reduce some backend calculations;
* may provide immediate client feedback.

Disadvantages:

* ownership of each rule becomes unclear;
* rules can diverge between client and server;
* debugging becomes more difficult;
* contract changes require synchronized rule updates;
* manipulation remains possible when validation is incomplete.

### Option C — Backend-authoritative gameplay

The client sends player intent and resource identifiers.

The backend:

* resolves the authenticated user;
* loads authoritative state;
* validates permission;
* calculates outcome;
* persists the result;
* returns the confirmed state.

Advantages:

* one source of truth;
* improved security;
* consistent cross-device behavior;
* easier rule testing;
* safer social competition;
* clearer React and Phaser responsibilities.

Disadvantages:

* requires complete backend validation;
* actions depend on API availability;
* visual feedback must account for network latency;
* concurrency-sensitive operations require careful persistence.

## Decision

Farm & Friends will use backend-authoritative persistent gameplay.

The frontend may send intent such as:

* selected seed ID;
* requested plot ID;
* requested quantity;
* requested farm ID.

The frontend must not authoritatively determine:

* authenticated user identity;
* ownership;
* rewards;
* prices;
* XP;
* yield;
* growth completion;
* action limits;
* inventory totals;
* currency totals.

The backend must derive and validate these values using trusted server state.

## Consequences

### Positive

* persistent progression is harder to manipulate;
* theft and other social actions remain fair;
* gameplay rules can be tested on the server;
* React and Phaser remain presentation and interaction layers;
* multiple clients receive consistent state.

### Negative

* endpoint implementations require more validation;
* network failure must be handled in the interface;
* optimistic updates require rollback or reconciliation;
* concurrency must be considered for harvesting, theft and transactions.

### Neutral or operational

* API responses should return enough confirmed data to update the interface;
* frontend types must follow backend contracts;
* Phaser effects should occur after confirmation or be safely reversible;
* backend tests must cover rejected and repeated actions.

## Implementation guidance

Backend endpoints must:

1. resolve the authenticated user from JWT claims;
2. load the requested resource;
3. validate ownership or permission;
4. load authoritative prices, rewards and timing;
5. calculate the outcome;
6. persist all related changes consistently;
7. return an explicit response.

React should:

1. collect user intent;
2. send the request;
3. handle loading and errors;
4. update application state from the confirmed response;
5. synchronize Phaser when necessary.

Phaser should:

1. emit world interaction intent;
2. render state supplied by React;
3. show confirmed visual outcomes;
4. avoid directly mutating persistent gameplay state.

## Validation

Architecture review should flag any change that:

* accepts XP from the client;
* accepts final reward quantity from the client;
* accepts a user ID as proof of identity;
* accepts client-computed prices;
* relies on frontend ownership checks;
* changes RemainingYield only inside Phaser;
* grants a visual reward before confirmation without reconciliation;
* duplicates authoritative gameplay rules in React.

Backend tests should verify that manipulated request values cannot override
server rules.

## Related documents

* `AGENTS.md`
* `FarmAndFriends.Api/AGENTS.md`
* `FarmAndFriends.Frontend/AGENTS.md`
* `FarmAndFriends.Frontend/src/game/AGENTS.md`
* `docs/gameplay/README.md`
* `docs/architecture/README.md`
