# Farm & Friends Frontend Guidance

This file applies to all work inside `FarmAndFriends.Frontend/`.

The repository root `AGENTS.md` remains authoritative for product vision,
gameplay principles, delegation and project-wide engineering rules.

This file adds frontend-specific guidance for React, TypeScript, TailwindCSS and
integration with Phaser.

Files inside the Phaser game area may also be governed by a more specific
`AGENTS.md`.

## Frontend responsibilities

React is responsible for:

* application routing;
* authentication session handling;
* API requests;
* application state;
* farm session state;
* HUD;
* panels;
* modals;
* inventory;
* shop;
* friends list;
* notifications;
* loading, success and error feedback;
* accessibility outside the Phaser canvas;
* responsive interface behavior.

Phaser is responsible for the interactive farm world.

Do not blur these responsibilities merely because both systems run in the same
browser application.

## Technology

The frontend uses:

* React;
* TypeScript;
* Vite;
* TailwindCSS;
* React Router;
* Phaser.

Use the package manager and scripts already configured in the repository.

Do not add a new state library, UI framework, HTTP library or styling framework
without explaining why the existing stack is insufficient.

## Project discovery

Before editing frontend code:

1. inspect `package.json`;
2. identify the package manager from the repository lockfile;
3. inspect available scripts;
4. locate the relevant component, hook, context and API function;
5. inspect existing types;
6. identify whether Phaser participates in the flow;
7. trace the state from API response to rendered interface;
8. inspect related tests.

Do not assume that a hook, context, event or component exists based only on its
name in a task description.

Follow the patterns actually used by the project.

## Source of truth

The backend remains authoritative for:

* authenticated identity;
* permissions;
* ownership;
* inventory quantities;
* currency balances;
* XP;
* levels;
* rewards;
* prices;
* RemainingYield;
* growth timing;
* action limits.

The frontend may calculate display-only values, such as a countdown derived from
a server-provided timestamp.

Display calculations must not grant rewards, unlock actions or override a server
decision.

When the server rejects an action, update the interface according to the server
result rather than forcing the optimistic state to remain.

## API layer

Keep HTTP concerns in the existing API layer or request functions.

Components should not duplicate:

* base URL handling;
* authorization headers;
* response parsing;
* standard error extraction;
* token handling.

When an endpoint contract changes:

1. update its TypeScript request and response types;
2. update the API function;
3. update hooks or contexts consuming it;
4. update components;
5. identify any Phaser event or visual state affected.

Do not silently use `any` to bypass a changed contract.

Use `unknown` for untrusted errors and narrow the value safely when practical.

## Types

Prefer explicit types for:

* API requests;
* API responses;
* component props;
* context values;
* hook return values;
* custom event payloads;
* farm and plot state;
* inventory entries;
* player state.

Avoid duplicating structurally different versions of the same contract without
a clear reason.

If an API DTO differs from the frontend view model, use an explicit mapping
function.

Do not modify generated or shared types manually if the repository uses a
generation workflow.

## Components

Components should primarily:

* render state;
* receive user intent;
* call hooks or callbacks;
* show feedback;
* remain reasonably focused.

Avoid placing large data-fetching workflows or gameplay calculations directly
inside visual components.

Extract behavior when it:

* is reused;
* requires effects and cleanup;
* coordinates multiple requests;
* represents a named application flow;
* makes the component difficult to understand.

Do not split very small components purely to satisfy an arbitrary file size.

## Hooks and contexts

Hooks should follow React hook rules and must not be called conditionally.

A hook should have a clear owner and responsibility.

Contexts are appropriate for state shared across meaningful parts of the
application, such as:

* authenticated user;
* active farm;
* visiting session;
* inventory;
* UI coordination when genuinely global.

Do not place every local component state in a global context.

Avoid multiple contexts independently owning different copies of the same farm
or player data.

When an action changes shared data, update the established owner of that state.

## State updates

Use immutable state updates.

State belonging to repeated rows or items must be keyed by item identity when
each row needs an independent value.

For example, quantity selectors for shop items must not share one accidental
global quantity unless that behavior is intentional.

Avoid stale closures in asynchronous callbacks and event listeners.

When state depends on previous state, use the functional update form where
appropriate.

## Effects and cleanup

Effects must declare the dependencies needed by their behavior.

When registering:

* window events;
* document events;
* timers;
* intervals;
* subscriptions;
* Phaser bridge listeners;

remove them during cleanup.

Do not register a new anonymous listener on every render.

Avoid effects that continuously synchronize two states in both directions.
Prefer one clear source of truth and explicit events.

## Loading, error and empty states

Every asynchronous interface flow must consider:

* initial loading;
* retry or recoverable failure;
* empty result;
* successful result;
* action in progress;
* action failure.

Do not leave an action button active while the same mutation is already pending
when duplicate requests could cause problems.

Show server errors in language appropriate to the game interface.

Do not use native `alert`, `confirm` or `prompt` for normal gameplay feedback.

Use an in-game modal, message, toast or existing feedback component.

## Modals and overlays

When a React modal or overlay is open:

* prevent click-through to the Phaser canvas;
* stop propagation where necessary;
* communicate modal state through the documented integration mechanism;
* restore input when the modal closes;
* preserve keyboard accessibility;
* provide a clear close action.

Do not rely on CSS appearance alone to prevent world interaction.

## React and Phaser boundary

React controls:

* API calls;
* application state;
* panels;
* HUD;
* modals;
* navigation;
* farm visit session;
* error and success messaging.

Phaser controls:

* world objects;
* plot sprites;
* scene input;
* animations;
* world-space effects;
* isometric positioning.

Communication must use documented events or the established bridge.

Every new event must define:

* event name;
* sender;
* receiver;
* payload type;
* when it fires;
* whether it represents intent, confirmed state or visual notification.

Do not access internal Phaser scene collections directly from arbitrary React
components.

Do not let Phaser call application endpoints directly when React already owns
the request flow.

## Event semantics

Use events according to their meaning.

An intent event communicates what the player attempted.

A confirmed-result event communicates a result accepted by the backend.

A synchronization event communicates authoritative state.

Do not use an intent event as proof that an action succeeded.

Avoid creating several events that represent the same state transition.

Prefer updating the authoritative farm state and synchronizing the scene when
that produces a simpler and more reliable flow.

## Styling

Use the existing TailwindCSS conventions.

The interface should remain:

* casual;
* legible;
* visually consistent;
* usable on desktop;
* usable on mobile.

Reuse established spacing, typography, panel and modal patterns.

Do not introduce a second styling system for one component.

Avoid desktop-only fixed dimensions when the component is part of a responsive
flow.

Preserve adequate contrast and readable text sizes.

## Accessibility

For React-managed interface elements:

* use semantic buttons for actions;
* provide accessible labels for icon-only buttons;
* keep keyboard focus visible;
* ensure modals can be closed intentionally;
* avoid using color as the only status signal;
* associate labels with form inputs;
* communicate disabled and loading states.

Canvas-specific accessibility limitations should not be used as justification
for inaccessible React panels.

## Authentication

Use the established authentication flow.

Do not derive authorization from visible UI state.

Hiding a button does not secure an operation.

Handle expired or invalid authentication consistently with the existing client
behavior.

Do not log access tokens or expose them in rendered errors.

## Error handling

Do not assume all caught errors have an Axios-shaped `response` field.

Narrow error shapes safely or use an established error helper.

Prefer server-provided user-facing messages when they are safe and appropriate.

Provide a useful fallback message.

Do not expose stack traces or internal server details to the player.

## Performance

Avoid premature optimization.

Investigate before adding memoization.

Pay attention to:

* repeated farm requests;
* duplicate event registration;
* whole-scene recreation;
* unnecessary context-wide rerenders;
* unstable callback dependencies;
* large asset reloads.

Do not cache authoritative gameplay state indefinitely without a refresh or
invalidation strategy.

## Testing requirements

Add or update frontend tests when changing:

* hooks;
* contexts;
* contract mapping;
* conditional actions;
* modal behavior;
* error handling;
* list item state;
* visiting versus own-farm behavior;
* React and Phaser event integration.

Verify relevant states:

* loading;
* success;
* server rejection;
* empty content;
* disabled action;
* repeated click;
* own farm;
* visiting farm;
* modal open;
* modal closed.

For visual behavior that lacks automated coverage, report the manual scenario
used for validation.

## Validation commands

Before declaring frontend work complete:

1. inspect the scripts defined in `package.json`;
2. run the relevant type check;
3. run lint when configured;
4. run focused tests;
5. run the broader frontend tests when practical;
6. build the application;
7. inspect the final diff.

Use the repository's configured package manager.

Typical scripts may include:

```bash
npm run lint
npm run test
npm run build
```

Only run scripts that actually exist.

Do not claim that a check passed unless it was executed successfully.

If a check cannot run, report the exact blocker.

## Scope boundaries

Normal React work should not modify backend entities, controllers or migrations.

Normal React work should not make deep changes to Phaser scene internals.

When a task requires Phaser changes:

1. read the `AGENTS.md` closest to the Phaser code;
2. document the integration contract;
3. coordinate file ownership;
4. avoid editing the same integration file concurrently.

Do not implement product, social or economic decisions that are still pending.

## Code review rules

Flag changes that:

* calculate authoritative rewards or permissions in the client;
* use UI visibility as authorization;
* introduce `any` merely to suppress contract errors;
* create a second source of truth for farm state;
* register event listeners without cleanup;
* allow modal click-through into Phaser;
* use action intent as proof of backend success;
* make direct API calls from Phaser when React owns the flow;
* share accidental state between repeated list rows;
* omit loading or failure behavior for important mutations;
* alter an API contract without updating TypeScript consumers;
* add native alerts for normal gameplay feedback.

Prefer correctness and player-impacting findings over cosmetic preferences.

## Definition of done

Frontend work is complete only when:

* the requested behavior is implemented;
* TypeScript contracts match the API;
* shared state remains consistent;
* loading and errors are handled;
* React and Phaser responsibilities remain separated;
* event listeners are cleaned up;
* modal input behavior is correct;
* relevant checks pass;
* responsive behavior is preserved;
* no unrelated refactor is included.
