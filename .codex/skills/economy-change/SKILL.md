---
name: economy-change
description: Analyze or implement a Farm & Friends change affecting Coins, PremiumCoins, XP, prices, yield, rewards, timers, limits, level requirements, or production cycles.
---

# Economy Change

Use this skill before changing an economic number or rule. Read the applicable `AGENTS.md` and inspect the actual source and sink, current balance/inventory services, transaction boundaries, limits, timing, and tests. Ask the economy designer for unresolved product decisions; do not encode assumptions as implementation details.

Produce a compact impact analysis covering player value and progression, owner/visitor outcomes, sources and sinks, expected pacing, inflation, duplication and replay exploits, negative balances, concurrency, premium-currency interaction, and abuse/frustration risks. Keep premium currency aligned with cosmetics, expression, and convenience; do not add item-ledger scope unless explicitly requested.

Implement authoritative calculations on the backend. Use server time and trusted configuration, preserve idempotency and atomicity, and ensure rewards, XP, yield, inventory, and currency cannot be granted or consumed twice. Update API types and UI only to present confirmed results. Add tests for normal and boundary values, rejected/duplicate/concurrent operations, and resulting balances. Record assumptions, metrics, rollout, and rollback.
