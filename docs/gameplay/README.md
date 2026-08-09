# Farm & Friends — Gameplay Rules

This document describes the currently accepted gameplay behavior of
Farm & Friends.

It is the reference for game rules and player-facing outcomes.

It does not describe implementation details. Technical details belong in
`docs/architecture/`.

Proposed mechanics must not be added here until they are approved.

## Document status

Status: Active

Last reviewed: 2026-07-29

Maintainers:

* Game Director
* Social Designer
* Economy Designer

## Core gameplay loop

The current primary loop is:

1. acquire seeds;
2. plant seeds in unlocked plots;
3. wait for crops to grow;
4. harvest ready crops;
5. receive crops and experience;
6. sell crops or use them in future systems;
7. improve the farm and unlock additional progression.

The social loop complements the primary loop:

1. open the friends panel;
2. select a friend;
3. visit the friend's farm;
4. inspect the farm;
5. interact with eligible crops;
6. return to the player's own farm.

Social interactions must support the farming loop rather than replace it.

## Gameplay terminology

### User

The authenticated player account.

### Farm

The persistent farm owned by a user.

A user currently owns one primary farm.

### Plot

A farm tile where a seed may be planted.

A plot can be:

* locked;
* unlocked and empty;
* planted and growing;
* ready for harvest.

### Seed

An inventory item that can be planted.

A seed defines gameplay properties such as:

* purchase price;
* crop produced;
* growth time;
* crop amount;
* minimum level.

### Crop

An inventory item produced by harvesting or stealing.

Crops may currently be sold in the shop.

### RemainingYield

The amount of crop still available in a ready plot.

RemainingYield decreases when theft or an unresolved pest consumes production.

RemainingYield is authoritative while a crop is planted. It is initialized on
planting, awarded on harvest and cleared when the plot becomes empty.

Theft and pest damage must never reduce RemainingYield below one unit.

The owner must retain a meaningful harvest after theft.

### Pest infestation

A server-controlled threat that may affect a ready, unprotected crop after its
safety period.

The MVP pest is a caterpillar. One planted crop may receive at most one
infestation, and an infestation can consume at most one unit. A successful
manual removal is a distinct helping action: the actor may receive a small,
server-confirmed XP and standard-currency reward, regardless of whether the
plot belongs to the actor or to an accepted friend.

### Pest protection

A time-limited property of one plot. Protection prevents new infestations and
cancels a scheduled or active infestation, but it does not prevent theft.

Protection remains attached to the plot after harvesting and replanting until
its server-provided expiration time.

### Plot care

A positive social action in which a visitor waters a growing crop on an
accepted friend's farm.

Plot care rewards the visitor and records visible authorship for the owner. It
does not change the crop's production, growth or theft state.

### Care opportunity

The server-created identity for the care state of one planted crop.

It begins when that crop is planted and remains valid while the crop is
growing. Opening or refreshing a farm does not create a care opportunity.

### Visitor-farm care cycle

The reward-accounting period shared by one visitor across the eligible plots
of one farm.

Its scope is the pair `VisitorUserId + FarmId`. A care cycle on one friend's
farm does not consume or reset the visitor's cycle on another friend's farm.

## Plot lifecycle

The normal plot lifecycle is:

```text
LOCKED
  ↓ unlock
EMPTY
  ↓ plant
GROWING
  ↓ readyAt reached
READY
  ↓ harvest
EMPTY
```

A theft action does not clear the plot.

It changes the ready plot's RemainingYield.

## Planting

A player may plant when:

* the player is authenticated;
* the plot belongs to the player's farm;
* the plot is unlocked;
* the plot is empty;
* the player owns at least one unit of the selected seed;

A successful planting action:

* consumes one seed;
* sets the planted seed;
* records the planting timestamp;
* calculates the ready timestamp on the server;
* initializes RemainingYield from the seed's crop amount;
* grants the approved planting XP;
* changes the plot to the growing state.

The client does not determine the ready timestamp, yield or XP reward.

## Growth

Growth is based on server timestamps.

Important timestamps include:

* `plantedAt`;
* `readyAt`.

The frontend may calculate visual progress from these timestamps.

The backend remains responsible for confirming whether the crop is ready.

Closing the game does not pause growth.

## Harvesting

Only the farm owner may harvest their own plot.

A plot may be harvested when:

* the user is authenticated;
* the user owns the farm;
* the plot exists;
* the plot contains a seed;
* the crop is ready;
* RemainingYield is greater than zero.

A successful harvest:

* grants the current RemainingYield to the owner's crop inventory;
* grants the approved harvest XP;
* clears the planted seed;
* clears growth timestamps;
* resets RemainingYield;
* returns the plot to the empty state.

A harvest action must not grant rewards more than once.

## Farm visits

A player may visit another player's farm.

While visiting:

* the visitor may inspect the friend's plots;
* the visitor cannot plant on the friend's farm;
* the visitor cannot harvest as the owner;
* the visitor may steal only when the theft rules allow it;
* the visitor must have a clear way to return to their own farm.

The friends list must not directly reveal which friends have ready crops.

The player visits to discover the current farm state.

A visit should eventually remain meaningful even when no theft is available.

## Plot care

Plot care is available only when:

* the visitor is authenticated;
* the visitor and owner have an accepted friendship;
* the visitor is not the farm owner;
* the plot contains a crop that is still growing;
* the server reports that both care and its reward are available.

Every accepted friend has independent eligibility. One friend's care does not
consume another friend's opportunity to care for the same crop.

The visitor may care for every eligible plot on the farm during the same visit.
No energy, tool or consumable is spent.

The current runtime values configured in
`FarmAndFriends.Api/appsettings.json` are:

* five hours of cooldown for the same visitor on the same care opportunity;
* a five-hour reward cycle scoped to `VisitorUserId + FarmId`;
* at most five rewarded cycles for that same visitor and farm in a rolling
  24-hour window;
* 2 standard coins and 5 XP for each eligible plot rewarded in the cycle;
* reward values do not depend on crop, player level or farm value.

The reward cycle is counted separately for every visited farm. Caring for six
eligible plots on Ana's farm is one rewarded visitor-farm cycle with six
per-plot rewards; caring for Paula's farm uses Paula's independent cycle and
quota.

A plot can grant its per-plot reward only once to that visitor in the same
visitor-farm cycle. A crop planted after the active cycle began waits for the
next cycle before its care reward becomes available.

When care is temporarily unavailable:

* the world does not show the water-drop badge;
* the modal does not show the `Regar` action;
* the modal shows the remaining server-provided time instead of reward-limit
  copy;
* the client refreshes the farm at the deadline and lets the server confirm
  whether care is available again.

After a successful care action, the water-drop badge disappears without being
replaced by a completion badge. Coin feedback is shown first and XP feedback
immediately afterward.

For the owner, care:

* records who cared for the crop;
* may show the most recent caregiver names on the plot;
* creates a non-urgent attributed notification;
* creates no obligation, streak, ranking or penalty for not reciprocating.

Care never:

* accelerates growth;
* creates additional production;
* restores yield already stolen;
* makes a crop immune to theft;
* resets theft limits;
* grants theft priority;
* changes the amount that can be stolen.

Care and theft remain independent and may both occur during the same visit.
Care must not hide a theft action or its history.

If nobody cares for a crop, the owner receives no delay, damage or yield
reduction. The care opportunity becomes inactive when the crop stops growing
and is cleared when the crop is harvested.

## Theft

Theft is a social interaction performed by a visitor against a ready crop.

Theft may occur only when:

* the visitor is authenticated;
* the farm belongs to another player;
* the plot exists;
* the plot contains a ready crop;
* RemainingYield allows a valid theft;
* applicable limits permit the action.

A successful theft:

* removes an approved amount from RemainingYield;
* grants that crop amount to the visitor;
* may grant approved XP;
* creates a theft log or notification;
* preserves a meaningful harvest for the owner.

Theft must never reduce RemainingYield below the protected owner amount.

Theft must not become more rewarding than maintaining the player's own farm.

Repeated theft must be limited to avoid harassment or coordinated exploitation.

## Theft outcome principles

The owner should experience:

* a visible but limited loss;
* a clear explanation of what happened;
* the ability to identify the social interaction;
* a remaining reason to harvest.

The visitor should experience:

* a meaningful but limited reward;
* immediate confirmation;
* motivation to visit farms;
* limits that prevent theft from replacing farming.

## Pests

The caterpillar is a light alternative threat on ready crops. Its MVP cycle is:

```text
Ready crop
→ configured safety period
→ Scheduled
→ Active
→ configured reaction window
→ Removed, cancelled or Consumed
```

The development prototype currently uses a 1-minute safety period and a
5-minute reaction window. These remain centralized backend configuration, not
client rules.

~~The base runtime configuration keeps the prototype disabled. Development
enables it explicitly; target environments must opt in only after the
migration and acceptance checks~~. Disabling it prevents new timed damage and
repellent spending while still allowing a player to remove an already-active
caterpillar without receiving a reward.

A crop is eligible only when:

* it is ready;
* its safety period has ended;
* its total production is greater than one;
* its current RemainingYield is greater than one;
* its plot is not protected;
* the current planting cycle has not already received an infestation.

The backend processes elapsed deadlines when the farm is loaded or when a
relevant plot action occurs. The client may display countdowns, but it never
activates a caterpillar or applies damage.

An active caterpillar:

* is visible to the owner and accepted-friend visitors;
* may be removed by the owner or an accepted friend;
* grants the actor 2 standard coins and 5 XP while a reward slot is available;
* consumes exactly one unit if its reaction deadline expires;
* never consumes the owner's final unit;
* generates an owner notification when consumption occurs.

The reward limit is global per actor: the first 15 rewarded removals inside a
rolling 24-hour window pay, combining the actor's own farm and every visited
farm. Further removals remain available and still save the crop, but grant
zero resources until a slot becomes available again. Only the manual
`Active -> Removed` transition is eligible; harvest, theft, protection,
consumption and other cancellation paths never grant this reward.

Every successful manual removal is recorded durably and tied to one pest
occurrence. This makes retries idempotent, prevents concurrent duplicate
rewards and leaves an audit trail even when the planting cycle later resets.
The removal request names the occurrence the player actually saw, so a delayed
request cannot affect a later caterpillar on the same plot.
The browser only displays `coinsGained`, `xpGained` and `rewardGranted` values
confirmed by the backend. A new completion closes the plot modal before the
world displays its coin and XP feedback. An idempotent replay synchronizes
balances without replaying that celebration.

Harvesting processes an already elapsed consumption first, cancels any
remaining scheduled or active infestation and awards the resulting
RemainingYield.

A successful theft follows this precedence:

* `Scheduled` remains scheduled;
* `Active` becomes `CancelledByTheft`;
* a scheduled infestation whose yield later reaches one resolves without pest
  damage when activation is attempted.

Therefore, a theft earlier in the crop cycle does not make the crop permanently
immune to pests, but a theft while the caterpillar is active prevents that
infestation from also consuming production.

The farm may have multiple active caterpillars, subject to the configured
farm-wide active limit and minimum interval between appearances. No pest
information is exposed in the friends list; it is discovered inside the farm.

### Repelente Natural

`natural_repellent` is a standard-currency consumable stored as an inventory
item. It may be applied to an unlocked empty, growing or ready plot.

Applying it:

* consumes one item from the authenticated actor, including a visiting friend;
* sets the plot's protection expiration atomically;
* cancels a scheduled or active infestation;
* does not affect theft permission;
* is rejected without consuming an item while protection is already valid.

The current catalog price is 30 standard coins and the current protection
duration is four hours. Both values come from backend configuration.

## Inventory

The inventory currently contains:

* seeds;
* crops;
* generic consumable items;
* standard currency;
* premium currency.

Inventory quantities cannot become negative.

All inventory changes are confirmed by the backend.

## Shop

The shop currently supports:

* buying seeds with standard currency;
* buying configured consumable items with standard currency;
* selling crops for standard currency.

A purchase requires:

* a valid seed or catalog item;
* a valid quantity;
* sufficient currency;
* eligibility for any applicable level restriction.

A sale requires:

* a valid crop;
* a valid quantity;
* sufficient crop inventory.

The server determines prices and final transaction values. Selling crops grants
standard currency only; it does not grant XP or alter the player's level.

## Experience and levels

Players gain experience from approved gameplay actions.

Current XP sources include:

* planting;
* harvesting;
* stealing;
* plot caring;

Selling crops is not an XP source.

The backend calculates and grants XP.

The frontend only displays the resulting XP and level.

A single action must not grant XP more than once.

Level progression should remain understandable and should unlock visible or
meaningful improvements.

## Currency

### Standard currency

Standard currency is earned through normal gameplay.

It may be used for:

* buying seeds;
* future farm progression;
* future standard items.

### Premium currency

Premium currency should primarily support:

* cosmetics;
* expression;
* convenience;
* optional customization.

Premium currency must not provide direct dominance over other players.

The game must not create severe artificial frustration primarily to sell its
solution.

## Player feedback

Harvesting, stealing and helping must have distinct feedback.

Normal gameplay feedback should use:

* in-game messages;
* modals;
* notifications;
* visual world effects;
* HUD updates.

Native browser alerts should not be used for normal gameplay.

## Fairness principles

The following principles are mandatory:

* absence must not completely destroy progress;
* theft must not wipe out the owner's harvest;
* new players require reasonable protection;
* social actions must have limits against abuse;
* progression must not depend exclusively on visiting or stealing;
* paid advantages must not dominate social competition;
* important consequences must be understandable.

## Current gameplay foundations

The game currently includes:

* planting;
* timed crop growth;
* harvesting;
* seed inventory;
* crop inventory;
* standard currency;
* premium currency;
* shop transactions;
* player XP and levels;
* friends panel;
* farm visits;
* crop theft;
* RemainingYield;
* plot care;
* caterpillar infestations;
* timed plot protection and Repelente Natural.

These systems must be preserved unless an approved feature explicitly changes
them.

## Approved versus proposed rules

This document contains only approved or currently implemented rules.

Unapproved concepts must be labeled elsewhere as:

* proposal;
* hypothesis;
* prototype;
* backlog item.

Examples of concepts that must not silently become rules:

* daily quests;
* automated harvesting;
* premium economic advantages.

## Rule change process

A substantial gameplay rule change requires:

1. product alignment review;
2. social impact review when players affect each other;
3. economic review when values or progression are affected;
4. technical planning;
5. consolidated decision;
6. implementation;
7. gameplay validation.

After approval, update this document in the same change or immediately before
implementation.

## Open gameplay questions

Record unresolved questions here temporarily.

Each question should eventually become:

* an approved rule;
* a rejected proposal;
* a documented decision;
* a backlog item.

Current questions:

* What limits will apply to repeated theft?
* Should the accelerated development safety and reaction windows return to
  15 minutes after prototype telemetry?
* Should infestation frequency become probabilistic after the deterministic
  prototype?
* How much protection should new players receive?
* What farm progression becomes visually available at each level?
