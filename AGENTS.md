# Farm & Friends — Project Guidance

## Product Vision

Farm & Friends is a casual social farming game.
Every feature must reinforce at least one of these pillars:

1. caring for and developing one's own farm;
2. discovering what is happening on friends' farms;
3. creating memorable social interactions;
4. offering simple, understandable progression.

## Product Rules

- Do not reveal ready-to-harvest crops directly in the friends list.
- Visits must remain engaging even without the possibility of theft.
- Theft must not completely wipe out the owner's harvest.
- The backend is the authority on inventory, currency, XP, yield, and timing.
- The client never determines rewards or permissions.
- The interface must remain casual, legible, and suitable for both desktop and mobile.
- Avoid native alerts; use in-game feedback.
- Clearly distinguish between harvesting, stealing, and helping actions.

## Product identity

Farm & Friends is a social farming game inspired by the principles that made
classic social farming games engaging, without copying their implementation
or reproducing manipulative mechanics.

The farm is the setting. Social interaction is the core experience.

The main product question is:

"What would give the player a pleasant reason to return or visit a friend?"

## Product pillars

Every significant feature must reinforce at least one of these pillars:

1. Meaningful social visits
   - Visiting a farm should remain useful even when there is nothing to steal.
   - Interactions should create stories between players.
   - Prefer actions that can benefit both visitor and owner.

2. Anticipation without anxiety
   - Growth timers may create expectation.
   - Absence must not completely destroy a player's progress.
   - Avoid mechanics that require constant checking to prevent severe losses.

3. Visible progression
   - The farm should visibly evolve over time.
   - Land, crops, decorations and future animals should communicate progress.
   - Prefer meaningful goals over increasing numbers without visual impact.

4. Friendly competition
   - Theft is part of the game's identity.
   - Theft must preserve a meaningful reward for the farm owner.
   - Use limits, counterplay and notifications to avoid harassment.

5. Fair economy
   - Standard currency comes from playing.
   - Premium currency should primarily unlock cosmetics, expression and convenience.
   - Never create artificial frustration primarily to sell its solution.
   - Avoid pay-to-win and excessive fear of missing out.

6. Simple actions, meaningful consequences
   - Individual interactions should be easy to understand.
   - Complexity should emerge from timing, progression and relationships.

## Current gameplay foundations

The project already includes:

- Planting, growth and harvesting.
- Seed and crop inventory.
- Standard and premium currencies.
- Farm visits.
- Crop theft with RemainingYield.
- Experience and player levels.
- Friends panel.
- Shop for buying seeds and selling crops.
- React, TypeScript, TailwindCSS and Phaser frontend.
- ASP.NET Core .NET 8, EF Core and PostgreSQL backend.

Preserve these foundations unless a task explicitly requires changing them.

## Feature decision process

Before implementing a substantial gameplay feature:

1. Ask the game director to evaluate alignment with the product pillars.
2. Ask the social designer to evaluate player interaction and abuse risks.
3. Ask the economy designer to evaluate rewards, costs and progression.
4. Ask the technical planner to estimate architecture and implementation scope.
5. Wait for all analyses.
6. Produce one consolidated feature decision.
7. Do not modify application code until the feature is approved.

## Feature proposal output

Every feature proposal must include:

- Player problem or opportunity.
- Reason the player would care.
- Reason the player would return.
- Social interaction created.
- Owner and visitor outcomes.
- Possible frustration or abuse.
- Economy impact.
- Smallest playable version.
- Success metrics.
- Estimated technical scope.
- Final verdict: Prototype, Backlog or Reject.

## Engineering rules

- Prefer small vertical slices over large speculative systems.
- Do not introduce new production dependencies without explaining why.
- Keep backend rules authoritative.
- Never trust client-supplied user, reward, price, timing or ownership data.
- Add or update tests for gameplay rules.
- Avoid unrelated refactors during feature implementation.
- Preserve API compatibility unless the task explicitly approves a breaking change.

## Technical Limits

- React controls the HUD, panels, modals, and application state.
- Phaser controls the farm world, tiles, sprites, input, and visual effects.
- React ↔ Phaser communication must use documented events.
- Do not duplicate domain rules in the frontend.
- Contract changes must update types and consumers.
- Migrations must reflect actual domain changes.
- Do not add dependencies without proven necessity.

## Process

- New mechanics require product analysis before implementation.
- Features affecting the frontend, backend, and Phaser require an Execution Plan (ExecPlan).
- Product agents do not alter code.
- Implementers must follow the approved plan.
- Reviewers must not simply agree silently; they must report issues.

## Delegation to specialized agents

Do not call all agents by default.
We do not need to involve all eleven agents for every change.

### Small, localized change

Example: Display “in” only when `timeLeft > 0`.

Workflow: `frontend_engineer` → `qa_reviewer`

No need for `game_director`, `economy_designer`, or ExecPlan.

### Product

Use `game_director` when the request alters the vision, the main loop,
or product priority.

Use `social_designer` when the request alters friendship, visits, theft,
assistance, reciprocity, or social notifications.

Use `economy_designer` when the request alters price, timing, yield,
XP, level, currency, rewards, limits, or progression.

Use `technical_planner` when the request spans multiple layers,
requires a migration, changes a contract, or carries a significant risk of regression.

### Implementation

Use `backend_engineer` only for backend tasks.

Use `frontend_engineer` only for React and TypeScript.

Use `phaser_engineer` only for the Phaser world.

Use `integration_engineer` only when connecting pre-defined components.

Do not allow two agents to edit the same file simultaneously.

Stabilize contracts and responsibilities before executing parallel tasks.

### Validation

After implementation:

- use `qa_reviewer` for functionality and regression;
- use `architecture_reviewer` for structural or sensitive changes;
- use `gameplay_reviewer` for new mechanics or changes noticeable
  to the player.

Reviewers do not silently fix code.
They return findings to the primary agent.

### Escalation

When an agent encounters a decision outside their domain, they must:

1. halt that part of the task;
2. log the pending decision;
3. identify the responsible agent;
4. proceed only with independent parts.
