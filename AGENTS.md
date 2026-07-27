# Farm & Friends — Project Guidance

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