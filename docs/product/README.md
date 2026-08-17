# Farm & Friends — Product Direction

This document defines the product direction of Farm & Friends.

It describes:

* the game we are building;
* the experience we want to create;
* the audience we intend to serve;
* the principles used to evaluate features;
* the behaviors we want to encourage;
* the behaviors and product patterns we want to avoid.

Detailed gameplay rules belong in `docs/gameplay/`.

Technical structure belongs in `docs/architecture/`.

Long-term product and technical decisions belong in `docs/decisions/`.

Feature implementation plans belong in `docs/plans/` or the project's approved
ExecPlan location.

## Document status

Status: Active

Last reviewed: 2026-08-11

Product owner: Rato

Maintainers:

* Game Director
* Social Designer
* Economy Designer

## Product summary

Farm & Friends is a casual social farming game centered on caring for a farm,
visiting friends and creating small memorable interactions between players.

Players plant crops, wait for them to grow, harvest resources, improve their
farms and visit other players.

The farm provides progression and visual ownership.

The relationships between players provide the game's identity.

Farm & Friends is inspired by the social qualities that made classic browser
farming games engaging, but it must not simply reproduce their mechanics,
interfaces or manipulative engagement patterns.

The project should reinterpret social farming for a modern audience.

## Product statement

Farm & Friends is a friendly social farming game where players grow a personal
farm, visit friends and create stories through cooperation, discovery and
lighthearted competition.

## Product promise

The game should provide the player with:

* a farm that feels personally theirs;
* visible progress over time;
* simple actions with understandable consequences;
* pleasant reasons to return;
* curiosity about friends' farms;
* social interactions worth remembering;
* competition that does not destroy friendships;
* progression without constant pressure.

## Central product question

Every substantial feature should answer:

> What would give the player a pleasant reason to return to their farm or visit
> a friend?

A feature should not be accepted only because it increases session frequency.

The reason to return must also be understandable, fair and compatible with the
identity of Farm & Friends.

## Product vision

Farm & Friends should become a social farming world where:

* the player's farm visibly evolves;
* crops and resources create anticipation;
* visiting friends creates discovery;
* cooperation and theft create stories;
* returning after an absence still feels welcoming;
* progression remains approachable;
* monetization respects the player's time and relationships.

The long-term product is not only a farming simulator.

It is a social space expressed through farms.

## Target audience

Farm & Friends is primarily intended for players who enjoy:

* casual progression;
* farming and collection games;
* social browser games;
* decorating and personal expression;
* asynchronous interactions;
* short sessions throughout the day;
* friendly competition;
* games that can be understood without complex tutorials.

The game should remain approachable for players who do not identify as highly
competitive or technically experienced.

## Player profiles

Product decisions should consider several player profiles.

### Casual farmer

Plays once or twice per day.

Cares about:

* seeing progress;
* harvesting completed crops;
* making simple decisions;
* not losing everything while absent.

### Engaged farmer

Returns several times per day.

Cares about:

* optimizing planting;
* following short growth cycles;
* progressing efficiently;
* having several activities available.

### Social visitor

Enjoys visiting friends and seeing their farms.

Cares about:

* discovery;
* interaction;
* recognition;
* helping;
* reacting to what friends have done.

### Collector and decorator

Values ownership, customization and visible achievement.

Cares about:

* unlocking land;
* collecting objects;
* arranging the farm;
* expressing personality;
* showing progression to visitors.

### Friendly competitor

Enjoys theft, timing and comparison without wanting a hostile experience.

Cares about:

* opportunities;
* playful rivalry;
* revenge stories;
* limits that preserve fairness.

### Returning player

Comes back after days or weeks away.

Cares about:

* understanding what changed;
* recovering quickly;
* not feeling permanently punished;
* having a clear next action.

No feature should be evaluated using only the most engaged player profile.

## Core experience

The intended emotional experience combines:

* care;
* anticipation;
* satisfaction;
* curiosity;
* recognition;
* light surprise;
* playful tension;
* social warmth.

The player should generally feel:

* proud of their farm;
* curious about friends;
* pleased when progression becomes visible;
* amused by social events;
* motivated to return without feeling threatened.

The game may create temporary frustration, such as losing part of a harvest,
but frustration must remain limited, understandable and recoverable.

## Product pillars

Every substantial feature must reinforce at least one product pillar and must not
seriously damage another.

### 1. Meaningful social visits

Visiting another farm should be a meaningful action.

A visit should create at least one of:

* discovery;
* assistance;
* opportunity;
* recognition;
* progress;
* social information;
* an amusing consequence.

The friends list must not reveal every relevant farm state before the visit.

Discovery should occur on the farm itself.

Visits should eventually remain useful even when no theft is possible.

### 2. Anticipation without anxiety

Growth timers and delayed rewards may create expectation.

They must not create severe fear of absence.

The game may reward returning at useful moments, but it should avoid demanding
constant surveillance.

Absence must not completely destroy progress.

Players should not feel obligated to interrupt sleep, work or personal routines
to avoid major losses.

### 3. Visible progression

Progress should be visible in the game world.

Preferred progression includes:

* more usable land;
* new crops;
* new visual stages;
* decorations;
* buildings;
* animals;
* customization;
* new social possibilities.

Increasing a number without changing the player's experience is weaker than
visible or meaningful progression.

Levels should represent access to new possibilities, not only increasing
requirements.

### 4. Friendly competition

Theft is part of the identity of Farm & Friends.

It should create:

* timing decisions;
* playful rivalry;
* stories between friends;
* notifications worth reacting to;
* reasons to visit.

Theft must not:

* eliminate the owner's full harvest;
* become the main economic strategy;
* enable repeated harassment;
* strongly punish less active players;
* create permanent hostility.

Competition should encourage another interaction rather than end the
relationship.

### 5. Fair economy

Standard currency should primarily come from normal play.

Premium currency should primarily support:

* cosmetics;
* personal expression;
* convenience;
* optional customization;
* reasonable time-saving without dominance.

The game must not deliberately create severe frustration primarily to sell its
solution.

Premium purchases must not give direct social dominance.

A paying player should not be able to repeatedly damage non-paying players with
an advantage they cannot reasonably counter.

### 6. Simple actions, meaningful consequences

Individual actions should be easy to understand.

Examples:

* plant;
* harvest;
* visit;
* steal;
* help;
* buy;
* sell.

Complexity should emerge from:

* timing;
* crop choices;
* relationships;
* progression;
* resource planning;
* interaction history.

The interface should not expose unnecessary technical complexity.

### 7. Personal ownership

The player's farm should feel like a personal space.

Players should be able to recognize:

* what they built;
* what they unlocked;
* what they planted;
* how they arranged the space;
* how their farm differs from others.

Future systems should strengthen identity through:

* decoration;
* layout;
* themes;
* farm names;
* avatars;
* collectible objects;
* visible achievements.

### 8. Respectful return

Returning to the game should feel welcoming.

After an absence, the player should quickly understand:

* what is ready;
* what changed;
* what happened socially;
* what action they can take next.

The game should avoid greeting returning players only with losses, expired
opportunities and punishment.

## Primary gameplay loop

The primary loop is:

```text
Acquire seeds
    ↓
Plant crops
    ↓
Wait for growth
    ↓
Harvest
    ↓
Receive crops and XP
    ↓
Sell, reinvest or progress
    ↓
Improve the farm
```

This loop must remain understandable and satisfying without requiring social
interaction.

Social systems should strengthen the loop rather than make it irrelevant.

## Social gameplay loop

The social loop is:

```text
Open friends
    ↓
Choose a friend
    ↓
Visit their farm
    ↓
Discover its current state
    ↓
Interact
    ↓
Create a consequence or story
    ↓
Return or visit someone else
```

Possible social interactions include:

* theft;
* plot care;
* notifications;
* reactions;
* reciprocal visits;
* future gifts or shared events.

The social loop should not be reduced to scanning a list for guaranteed rewards.

## Progression loop

The progression loop is:

```text
Perform meaningful actions
    ↓
Gain resources and experience
    ↓
Increase level or wealth
    ↓
Unlock visible possibilities
    ↓
Expand personal expression and strategy
```

Progression should introduce new choices gradually.

It should not overwhelm a new player with every system at once.

## Session design

Farm & Friends should support short and medium sessions.

### Short session

A player should be able to:

* collect a ready crop;
* replant;
* check notifications;
* visit one friend;
* perform one useful interaction.

### Medium session

A player may:

* manage several plots;
* use the shop;
* visit multiple friends;
* review inventory;
* make progression decisions;
* customize the farm in future systems.

The game should not require long uninterrupted sessions for normal progress.

## Reasons to return

Healthy reasons to return include:

* a crop became ready;
* new land or content became available;
* a friend interacted with the player's farm;
* the player wants to check a friend's progress;
* a visual goal is close to completion;
* a new social opportunity appeared;
* the player wants to change or improve the farm.

Weak or harmful reasons to return include:

* severe punishment for not checking;
* repeated loss notifications;
* intentionally confusing expiration systems;
* artificial scarcity created mainly to generate fear;
* constant interruption demands;
* rewards that become worthless unless claimed immediately.

## Social interaction principles

Social features should answer:

* Who initiates the interaction?
* What does the initiator gain?
* What does the other player experience?
* Does the other player understand what happened?
* Can they respond?
* Can the interaction be repeated abusively?
* Does the feature create a story?
* Does it create another reason to visit?

Prefer interactions with consequences visible to both sides.

A social action should not feel like an invisible database transaction.

## Owner and visitor balance

When a visitor affects another player's farm, evaluate both perspectives.

### Farm owner

The owner should retain:

* meaningful progress;
* understandable feedback;
* a useful next action;
* reasonable protection;
* the possibility of responding.

### Visitor

The visitor should receive:

* a clear opportunity;
* a meaningful but limited reward;
* immediate feedback;
* a reason to explore;
* limits against repetitive exploitation.

Neither side should exist only to provide resources for the other.

## Discovery principle

Farm discovery is an important part of social play.

The game should not reveal all valuable information before a visit.

For example, the friends list must not directly identify which farms contain
ready crops.

The player should visit and inspect the farm.

This principle may be relaxed for accessibility or usability when necessary,
but convenience must not eliminate the purpose of visiting.

## Progress protection

Loss may exist, but it must be bounded.

The product should protect:

* a minimum meaningful harvest;
* basic player progression;
* new-player stability;
* recovery after absence;
* resources purchased with premium currency;
* important one-time rewards.

The game must avoid complete loss caused by one social interaction.

Repeated systems must consider cumulative loss, not only the impact of one
action.

## Current product foundations

The project currently includes:

* account registration and authentication;
* player identity;
* farm identity;
* planting;
* crop growth;
* harvesting;
* seed inventory;
* crop inventory;
* standard currency;
* premium currency;
* experience;
* player levels;
* farm visits;
* crop theft;
* RemainingYield;
* theft history or notifications;
* plot care;
* friends panel;
* shop for buying seeds;
* shop for selling crops;
* React interface;
* Phaser farm world;
* ASP.NET Core backend;
* persistent PostgreSQL data.

These foundations should be preserved unless an approved decision explicitly
changes them.

## Current product stage

Farm & Friends is currently in an early playable product stage.

The focus is not maximizing the number of systems.

The focus is validating that these foundations work together:

* farming feels satisfying;
* farm state is clear;
* visits are understandable;
* theft is playful and fair;
* progression is visible;
* the interface supports the loop;
* the architecture allows safe expansion.

New features should preferably improve an existing loop before creating a
completely separate loop.

## Product priorities

Current product priorities should follow this order unless an approved decision
changes it.

### Priority 1 — Stable farming loop

Ensure that planting, growth, harvesting, inventory, XP and shop behavior are:

* correct;
* understandable;
* responsive;
* visually satisfying.

### Priority 2 — Reliable social visits

Ensure that the player can:

* find friends;
* visit farms;
* understand whose farm they are visiting;
* interact according to permissions;
* return to their own farm;
* receive useful feedback.

### Priority 3 — Fair theft experience

Ensure that theft:

* works consistently;
* preserves owner reward;
* cannot be duplicated;
* communicates consequences;
* has enforceable limits;
* does not replace farming.

### Priority 4 — Visible farm progression

Introduce improvements that make higher progression visible in the world.

Possible future directions include:

* additional plots;
* decorations;
* buildings;
* visual farm upgrades;
* animals;
* themed customization.

These are product directions, not approved implementation commitments.

### Approved land expansion prototype

Land expansion is approved as a visible-progression prototype from the initial
3×3 farm to a maximum of 7 columns × 4 rows.

The owner unlocks one plot at a time through a single in-world sale sign. Each
purchase requires both a progression level and one of two server-priced payment
routes: standard currency or premium currency. Premium never bypasses the level
gate, and every productive plot remains obtainable through normal play.

This exception does not approve premium-only productive progression or future
sale of economic advantage. If premium currency becomes purchasable or gains a
renewable source, the land route must return to product and economy review.

The expansion should create anticipation without pressure:

* no expiry, daily offer or absence penalty;
* one explicit `Buy` action after the player chooses a payment route and sees
  its price, balance and level requirement;
* one understandable next plot;
* immediate planting value after purchase;
* no social announcement, ranking or request for help;
* visitors see the owner's complete current topology, including which plots
  are still locked, but never receive a land offer or purchase action.

The completed 7×4 farm has no placeholder for a speculative next stage. The
sale sign disappears after the 28th plot is unlocked.

## Smallest playable feature principle

Every new feature should begin with the smallest version that can validate its
main player value.

A smallest playable version must include:

* one clear player action;
* one understandable result;
* required backend validation;
* sufficient visual feedback;
* basic abuse protection;
* measurable expected behavior.

It should avoid:

* speculative variants;
* premature content expansion;
* multiple currencies without necessity;
* large administration systems;
* complex configuration before learning from play.

## Feature evaluation

Every substantial feature proposal must answer the following.

### Player opportunity

What player problem, desire or opportunity does this address?

### Player value

Why would the player care?

### Return motivation

Why would this make the player return?

### Social value

What interaction, discovery or story does it create?

### Owner outcome

What does the farm owner gain, lose or understand?

### Visitor outcome

What does the visitor gain, lose or understand?

### Frustration

What could feel unfair, repetitive or confusing?

### Abuse

How could players exploit, automate or coordinate around it?

### Economy

What resources, rewards, prices, limits or progression does it affect?

### Visual impact

How does the player see the feature in the farm or interface?

### Smallest playable version

What is the smallest version that tests the idea?

### Success signal

What observation or metric would indicate that it works?

### Technical scope

Which system layers are affected?

### Verdict

Use one of:

* Prototype;
* Backlog;
* Reject.

## Feature approval process

Before implementing a substantial gameplay feature:

1. the Game Director evaluates product alignment;
2. the Social Designer evaluates interaction and abuse;
3. the Economy Designer evaluates rewards, costs and progression;
4. the Technical Planner maps implementation impact;
5. all analyses are consolidated;
6. unresolved decisions are identified;
7. the feature receives a verdict;
8. application code remains unchanged until approval.

Approved feature work should update relevant product, gameplay, architecture and
decision documents when necessary.

## Product verdicts

### Prototype

Use when:

* the player value is plausible;
* the smallest version is testable;
* risks are manageable;
* implementation can produce meaningful learning.

Prototype does not mean permanent approval.

### Backlog

Use when:

* the idea is aligned;
* it is not the current priority;
* dependencies are missing;
* more foundational work is required.

### Reject

Use when:

* the idea conflicts with product identity;
* it creates harmful pressure;
* it damages social fairness;
* it duplicates another system without added value;
* it is too complex for its expected benefit;
* it depends primarily on manipulative monetization.

Rejected ideas may be reconsidered only when the context meaningfully changes.

## Success metrics

Metrics must be interpreted carefully.

No single metric defines product success.

### Farming health

Possible metrics include:

* percentage of planted crops eventually harvested;
* frequency of replanting after harvest;
* number of active plots;
* failed planting or harvesting attempts;
* time between harvest and replanting;
* crop variety used.

### Social health

Possible metrics include:

* percentage of active players who visit a friend;
* number of meaningful visits per active player;
* return visits between the same players;
* percentage of visits with an interaction;
* percentage of visits without theft that still produce value;
* response rate after receiving a social notification.

### Theft health

Possible metrics include:

* percentage of ready crops affected by theft;
* average amount lost by owners;
* percentage of owner harvest preserved;
* concentration of theft against the same player;
* repeated failed theft attempts;
* theft reward compared with normal farming income;
* player return after being stolen from.

### Progression health

Possible metrics include:

* time to first harvest;
* time to first purchase;
* time to first friend visit;
* level progression speed;
* percentage of players reaching meaningful unlocks;
* use of newly unlocked content;
* visible farm variation between levels.

### Experience health

Possible metrics include:

* players returning after one, seven and thirty days;
* action completion rate;
* abandoned flows;
* error frequency;
* manual feedback;
* confusion observed in testing;
* negative behavior around losses or notifications.

Metrics should be used to understand behavior, not to justify manipulation.

## Qualitative validation

Because Farm & Friends depends on feelings such as curiosity, fairness and
ownership, qualitative validation is essential.

Useful questions include:

* Did the player understand what to do next?
* Did the farm feel personal?
* Did visiting feel different from using a menu?
* Did theft feel playful or unfair?
* Did the owner still want to harvest?
* Did the visitor want to return?
* Did the player notice their progression?
* Did the player understand why a reward changed?
* Did any notification create pressure or anxiety?
* Did the interaction create a story worth telling?

## Monetization principles

Monetization must respect the product pillars.

Acceptable directions may include:

* cosmetic decorations;
* farm themes;
* avatar customization;
* visual effects;
* expressive social items;
* convenience that does not dominate competition;
* optional content collections;
* reasonable expansion choices.

Risky directions include:

* paying to steal more from friends;
* paying to remove all theft limits;
* buying direct dominance;
* severe crop loss designed to sell protection;
* excessive timer pressure;
* premium-only access to core social participation;
* obscured purchase probabilities;
* manipulative notification pressure.

Before approving monetization, evaluate its effect on:

* fairness;
* friendships;
* free-player progression;
* anxiety;
* product trust;
* long-term economy.

## Notification principles

Notifications should inform players about meaningful changes.

Examples may include:

* a crop becoming ready;
* a friend interacting with the farm;
* a meaningful progression event;
* a social response.

Notifications should not:

* exaggerate urgency;
* repeatedly punish inactivity;
* imply severe loss when none exists;
* expose private information unnecessarily;
* become the only reason a player remembers the game.

Notification frequency and channels require explicit product decisions.

## Interface principles

The interface should remain:

* casual;
* readable;
* welcoming;
* responsive;
* understandable on desktop and mobile;
* visually connected to the farm theme.

Prefer:

* in-game messages;
* clear icons with labels;
* contextual actions;
* visible consequences;
* short explanations;
* consistent modals and panels.

Avoid:

* native browser alerts;
* administrative language;
* excessive numeric detail;
* hidden consequences;
* several competing calls to action;
* unexplained disabled actions.

Harvesting, stealing and helping must appear as distinct actions.

## Visual direction

The visual direction should support:

* a warm and friendly atmosphere;
* cartoon-inspired assets;
* clear isometric readability;
* strong state recognition;
* visible farm progression;
* playful effects;
* social personality.

Visual polish should improve comprehension, not hide unclear rules.

Important states should remain visually distinct:

* locked;
* empty;
* growing;
* ready;
* selected;
* affected by theft;
* plot cared;
* future pest or protection state.

## Content principles

New crops, decorations, animals and objects should add at least one of:

* a new decision;
* visual expression;
* progression meaning;
* collection value;
* social value;
* a useful economic role.

Content should not be added only to increase item count.

Prefer a smaller number of meaningful and recognizable items over a large number
of nearly identical items.

## Tone and language

Farm & Friends should communicate in a tone that is:

* friendly;
* light;
* playful;
* clear;
* respectful.

The game may use humor around theft and social events, but it must not humiliate
the affected player.

Avoid:

* aggressive blame;
* threatening urgency;
* technical error language;
* insulting competitive messages;
* language that encourages harassment.

## Product anti-patterns

The following patterns should be challenged during review.

### Feature accumulation

Adding systems without strengthening the main loops.

### Menu-first social design

Turning social play into lists, indicators and reward buttons without meaningful
farm visits.

### Notification dependence

Using notifications as a substitute for an enjoyable return experience.

### Punishment-based retention

Making absence dangerous primarily to force repeated checking.

### Invisible progression

Increasing stats without meaningful visual or gameplay change.

### Economy without purpose

Adding currencies, prices or rewards without a clear behavioral objective.

### Client authority

Allowing the browser to determine persistent results.

### Theft dominance

Making stealing the most efficient way to progress.

### Premium dominance

Allowing payment to overpower social fairness.

### Premature scale

Building generalized infrastructure before validating one playable version.

### Administrative interface

Exposing implementation concepts instead of player actions and outcomes.

## Non-goals

Farm & Friends is not currently intended to become:

* a real-time competitive strategy game;
* a hardcore economic simulator;
* a game requiring continuous online presence;
* a system where one player can destroy another player's farm;
* a pay-to-win social competition;
* a purely single-player farming game;
* a collection of disconnected minigames;
* a direct recreation of an older social farm game;
* a platform for speculative systems before the core loop is stable.

These non-goals may be revisited only through an explicit product decision.

## Proposed product directions

The following ideas are potential directions, not approved commitments:

* pests that create limited crop loss;
* crop protection products;
* decorations;
* animals;
* buildings;
* reciprocal social actions;
* social reaction messages;
* daily or weekly objectives;
* collections;
* achievements;
* farm themes;
* seasonal visual content.

Each proposal must follow the feature approval process.

## Open product questions

The following questions remain unresolved and should not be silently decided
during implementation:

* What are the final limits for repeated theft?
* How should a player respond after being stolen from?
* What new-player protection is necessary?
* What should each level visibly unlock?
* Which forms of customization should appear first?
* Should decorations have gameplay effects?
* How should pests create anticipation without anxiety?
* How should crop protection work without selling relief from artificial pain?
* What information should social notifications contain?
* Which interactions should be reciprocal?
* What should premium currency support in the first public version?
* How should inactive or returning players be welcomed?
* Which metrics can be collected while respecting player privacy?

Each resolved question should update the appropriate gameplay document and may
require a decision record.

## Relationship with other documentation

Use this document when deciding:

* whether a feature belongs in Farm & Friends;
* whether it reinforces the product pillars;
* whether its player value is sufficient;
* whether it creates healthy return motivation;
* whether it respects social fairness.

Use `docs/gameplay/` when deciding:

* exact action rules;
* eligibility;
* rewards;
* limits;
* player-facing outcomes.

Use `docs/architecture/` when deciding:

* system ownership;
* data flow;
* API boundaries;
* React and Phaser responsibilities;
* persistence and integration.

Use `docs/decisions/` when explaining:

* why an important choice was made;
* which alternatives were rejected;
* which trade-offs were accepted.

## Document maintenance

Update this document when:

* the product vision changes;
* a new pillar is accepted;
* the target audience changes;
* a long-term product priority changes;
* a major non-goal is reconsidered;
* monetization direction changes;
* a new product stage begins.

Do not update this document for every implementation detail.

Detailed rule changes should update `docs/gameplay/`.

Technical changes should update `docs/architecture/`.

Durable decisions should create or update an ADR.

## Final product test

Before approving a major feature, ask:

1. Does it make the farm more meaningful?
2. Does it create a pleasant reason to return?
3. Does it improve discovery or interaction between friends?
4. Can both sides understand the consequence?
5. Does it preserve progress and fairness?
6. Is it simple for a casual player?
7. Does it create visible or meaningful progression?
8. Is its smallest version enough to test the idea?
9. Can it be implemented without duplicating authority?
10. Would Farm & Friends lose part of its identity without it?

A feature that cannot answer these questions should be revised, postponed or
rejected.
