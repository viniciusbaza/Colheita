# Farm & Friends Phaser World Guidance

This file applies to the Phaser game world inside `FarmAndFriends.Frontend/src/game/`.

The repository root `AGENTS.md` remains authoritative for product and
project-wide rules.

The parent `FarmAndFriends.Frontend/AGENTS.md` remains authoritative for React, TypeScript,
API consumption and the React–Phaser boundary.

This file adds Phaser-specific guidance.

## Phaser responsibility

Phaser controls the visual and interactive farm world, including:

* the farm scene;
* isometric positioning;
* plots and world objects;
* crop sprites;
* visual growth stages;
* pointer input inside the world;
* depth ordering;
* tweens and animations;
* glows and ready-state effects;
* world-space badges;
* floating XP or action feedback;
* asset loading;
* visual synchronization with authoritative farm state.

Phaser is not the authority for gameplay outcomes.

## Source of truth

Phaser may display:

* whether a plot is locked;
* the planted seed;
* growth progress;
* ready state;
* RemainingYield;
* owner or visiting mode;
* confirmed harvest or theft results.

These values must come from:

* authoritative farm state supplied by React; or
* a confirmed backend result propagated by React.

Phaser must not decide:

* whether the player is authorized;
* whether a crop can actually be harvested;
* how many items are awarded;
* how much XP is granted;
* how much yield is stolen;
* whether a daily limit has been reached;
* item prices;
* inventory totals;
* currency balances.

A local visual guard may prevent an obviously invalid click, but the backend
must still validate the operation.

## Project discovery

Before editing Phaser code:

1. inspect the current scene implementation;
2. locate the React wrapper or bridge;
3. list existing custom events;
4. inspect event listeners and cleanup;
5. identify texture keys and preload behavior;
6. inspect plot object storage;
7. inspect coordinate and depth calculations;
8. determine how farm synchronization currently occurs;
9. identify whether the task changes state, presentation or both.

Do not invent a parallel event system when an established bridge exists.

Do not assume that an event listed in documentation still matches the current
implementation without checking the code.

## Scene lifecycle

Respect the Phaser scene lifecycle.

Initialize scene-owned collections in a predictable place.

Register global or window listeners once per active scene lifecycle.

Remove listeners when the scene shuts down or is destroyed.

When rebuilding or synchronizing a farm:

* remove obsolete objects;
* stop obsolete tweens;
* destroy obsolete containers;
* clear stale references;
* avoid duplicating pointer handlers;
* preserve objects when an incremental update is safer.

Do not leave hidden objects, active tweens or listeners attached to replaced
plots.

## World object ownership

The scene should own Phaser objects.

React should not directly mutate Phaser sprites, containers or tweens.

Store scene object references in clear collections keyed by stable domain IDs,
such as the plot ID.

Do not identify persistent world objects only by array position when a stable ID
exists.

When a plot changes, update the object associated with that plot rather than
assuming the rendered order has remained unchanged.

## Idempotent synchronization

Farm synchronization must be safe to receive more than once.

Applying the same authoritative state twice must not:

* duplicate plots;
* duplicate badges;
* duplicate glows;
* add multiple pointer listeners;
* restart permanent effects unnecessarily;
* increment values twice;
* leave stale crop textures.

Separate creation, update and removal responsibilities where practical.

A synchronization event represents state, not an additional reward.

## React–Phaser events

Use the established event bridge for communication.

Current event concepts may include:

* `plot:click`;
* `plot:harvest:done`;
* `plot:steal:done`;
* `farm:sync`;
* `farm:change`;
* `ui:modal`.

Verify actual names and payloads in the code before modifying them.

For every new or changed event, document:

* name;
* sender;
* receiver;
* payload;
* semantic category;
* firing condition;
* cleanup responsibility.

Semantic categories:

* `intent`: player attempted an action;
* `confirmed-result`: backend accepted an action;
* `state-sync`: authoritative state replacement or update;
* `ui-control`: application UI affects scene behavior;
* `visual-only`: no domain state change.

Do not treat `plot:click` as proof that a plot action succeeded.

Do not emit a confirmed-result event before the backend confirms the action.

## Event payloads

Define TypeScript types for custom event payloads when practical.

Payloads should contain stable identifiers and only the data needed by the
receiver.

Prefer:

```ts
type PlotClickDetail = {
  plotId: string
  x: number
  y: number
}
```

over passing complete mutable Phaser objects through browser events.

Do not expose scene internals as event payloads.

When a payload changes, update all senders and receivers in the same coordinated
change.

## Input

World input must respect application UI state.

When React reports that a modal or blocking overlay is open:

* disable relevant scene input;
* prevent plot actions;
* preserve the current scene state;
* restore input when the UI closes.

Do not depend only on React event propagation to protect Phaser input.

Avoid registering pointer handlers repeatedly during visual updates.

A disabled or transitioning plot must not dispatch duplicate action intents.

## Isometric coordinates

Use the established isometric coordinate helpers and constants.

Do not duplicate coordinate formulas across unrelated functions.

When changing:

* tile width;
* tile height;
* world offset;
* scale;
* origin;
* camera position;
* depth calculation;

verify all affected object types, not only plots.

Maintain deterministic depth ordering so crops, effects, text and tiles overlap
correctly.

Do not fix one sprite by adding an unexplained per-item offset when the issue is
shared coordinate logic.

Document intentional exceptions for unusually sized assets.

## Textures and assets

Use stable, descriptive texture keys.

Preload an asset before referencing it.

Do not load the same asset repeatedly under different keys without a reason.

When adding a crop or growth stage:

* define all required texture mappings;
* provide a safe missing-texture behavior;
* verify growing and ready states;
* verify scale and origin;
* verify depth;
* verify the empty plot transition.

Do not select gameplay behavior based only on a filename.

Assets should represent state supplied by the application.

## Plot visual states

A plot may visually represent combinations such as:

* locked;
* empty;
* growing;
* ready;
* selected;
* temporarily disabled;
* being harvested;
* being stolen from;
* affected by a future pest or protection effect.

Avoid stacking incompatible effects without an explicit priority.

Define which state owns:

* base texture;
* crop texture;
* glow;
* pulse;
* badge;
* tint;
* input;
* temporary animation.

When a plot leaves a state, remove visual effects owned by that state.

## Growth display

Growth progress may be displayed from server timestamps.

Use a single consistent interpretation of `plantedAt` and `readyAt`.

Clamp display progress to a valid range.

A client countdown reaching zero may update presentation, but the backend remains
responsible for confirming that an action is allowed.

Do not grant readiness-dependent rewards solely because a local timer elapsed.

## RemainingYield badge

RemainingYield is supplied by authoritative state.

The badge must:

* show the current remaining amount when appropriate;
* update after a confirmed theft or farm synchronization;
* disappear when the plot no longer has a ready crop;
* avoid showing negative values;
* avoid surviving after harvest or plot reset.

Visual emphasis such as bounce or tint may communicate a confirmed reduction,
but it must not independently mutate the underlying yield.

## Tweens and visual effects

Track long-lived tweens associated with plots or objects.

Before replacing or destroying an object:

* stop its tweens;
* remove related timers;
* destroy related containers;
* clear stored references.

Do not create a new permanent pulse on every synchronization.

Visual effects must not block the authoritative state update.

Keep animations short enough that the player understands the result without
delaying normal interaction unnecessarily.

## Visual feedback

Important world actions should provide immediate, understandable feedback after
confirmation.

Examples include:

* planting;
* growth-stage transition;
* crop readiness;
* harvest;
* theft;
* XP gain;
* RemainingYield reduction;
* failed interaction when world-level feedback is appropriate.

Do not use the same feedback for harvesting and stealing when the product rules
require those actions to be clearly distinguishable.

Avoid effects that obscure the plot state or make the farm difficult to read.

## Performance

Prefer incremental updates when they are simpler and safer than rebuilding the
entire scene.

Avoid:

* recreating every plot for one yield change;
* adding duplicate tweens;
* loading assets during normal pointer actions;
* dispatching synchronization events every frame;
* allocating large objects in update loops;
* repeatedly querying the entire scene tree when keyed references exist.

Do not optimize without evidence when the simpler implementation is already
adequate.

## Error behavior

Phaser should not invent a successful visual result when the API operation
failed.

If an action fails:

* keep or restore the authoritative state;
* remove temporary pending visuals;
* allow React to display the main error message;
* avoid leaving the plot permanently disabled.

Do not hide integration errors by forcing the sprite into the expected state.

## Scope boundaries

Phaser work should not modify:

* backend controllers;
* backend services;
* database entities;
* migrations;
* economic formulas;
* authorization rules;
* React application state ownership.

Small bridge changes may be made when explicitly required, but the applicable
frontend guidance must be followed.

When a new backend field is needed, report the contract requirement rather than
simulating that field permanently inside Phaser.

When new art is needed, report the required asset keys, dimensions and states.

## Testing and validation

For Phaser changes, validate the relevant scenarios:

* initial farm load;
* own farm;
* visiting farm;
* empty plot;
* growing crop;
* ready crop;
* locked plot;
* repeated farm synchronization;
* harvest result;
* theft result;
* RemainingYield update;
* modal opening;
* modal closing;
* scene restart;
* returning from a friend's farm;
* failed API action;
* repeated pointer input.

Run configured TypeScript, lint, test and build commands from the frontend
project.

When automated visual testing is unavailable, document the manual scenarios
verified.

Do not claim that a visual scenario was tested unless it was actually observed.

## Code review rules

Flag changes that:

* calculate authoritative gameplay results inside Phaser;
* call backend endpoints directly from the scene without an approved reason;
* create a second source of truth for farm state;
* register global listeners without cleanup;
* create duplicate pointer listeners during synchronization;
* create permanent tweens repeatedly;
* use array position instead of stable plot identity;
* leave badges, glows or crop sprites after a plot reset;
* allow canvas interaction while a blocking modal is open;
* treat player intent as confirmed backend success;
* mutate RemainingYield only in the visual layer;
* pass Phaser objects through application events;
* rebuild the full scene for every minor state change without justification.

Prioritize state consistency, lifecycle safety and player-visible correctness.

## Definition of done

Phaser work is complete only when:

* the world reflects authoritative state;
* scene lifecycle cleanup is correct;
* synchronization is idempotent;
* events are documented and typed where practical;
* input respects React overlays;
* effects are created and removed correctly;
* relevant farm states were validated;
* frontend build succeeds;
* no domain rule was moved into the scene;
* React or backend dependencies are clearly reported.
