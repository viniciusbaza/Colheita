---
name: add-game-content
description: Add a new Farm & Friends crop, perennial crop, animal, decoration, cosmetic, or other catalog item across authoritative data, API, frontend, Phaser assets, tests, and documentation.
---

# Add Game Content

Read the applicable `AGENTS.md`, inspect the closest existing content with the same lifecycle, and load only the matching reference below:

- Crops: [references/crop.md](references/crop.md)
- Perennial crops: [references/perennial-crop.md](references/perennial-crop.md)
- Animals: [references/animal.md](references/animal.md)
- Decorations: [references/decoration.md](references/decoration.md)

Confirm the smallest playable slice and product/economy decisions before implementation. Trace catalog data → persistence/configuration → API → inventory/shop/state → React → Phaser texture and visual state. Backend owns unlocks, ownership, prices, timing, rewards, quantities, and permissions; the client only presents confirmed state.

Reuse existing content schemas, asset conventions, UI patterns, migrations, and tests. Do not add a migration for derived or presentation-only state. Cover availability, authorization, purchase/use/placement or lifecycle rules, repeated requests, empty/loading/error states, mobile layout, missing textures, own/visitor behavior, and documentation. Report required art or unresolved design decisions instead of inventing them.
