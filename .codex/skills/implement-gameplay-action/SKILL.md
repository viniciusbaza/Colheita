---
name: implement-gameplay-action
description: Implement an authoritative Farm & Friends gameplay action crossing backend, React, and optionally Phaser, such as planting, harvesting, stealing, helping, buying, selling, claiming, or collecting.
---

# Implement a Gameplay Action

Use the chain: player intent → React → API → server validation → domain result → transaction → response → authoritative React state → Phaser synchronization → feedback.

Read the applicable `AGENTS.md` files first and trace an existing action with similar semantics. Reuse established services, contracts, API clients, state hooks, event bridge, scene collections, and feedback patterns. Stabilize the contract and ownership of each file before editing.

## Mandatory server checks

Derive identity from authentication and validate ownership, visit/friendship permission, current resource state, server time, action limits, and all reward inputs on the server. Preserve inventory, currency, XP, yield, and timing invariants. Define the transaction boundary and protect duplicate or concurrent requests with the project's established idempotency, transaction, conditional-update, or concurrency strategy. Never trust client-supplied price, reward, quantity, timestamp, permission, or readiness.

## Client flow

React sends intent and owns the request, loading state, errors, and authoritative application state. Phaser must not call gameplay APIs directly or treat an intent as success. Apply confirmed results or refreshed farm state before visual feedback. Keep harvesting, stealing, and helping visually distinct. Clean up listeners and temporary effects; prevent duplicate clicks while a mutation is pending.

## Verification

Add focused tests for success, rejection, authorization, ownership, repeated requests, boundary/time cases, concurrency where relevant, and resulting persisted state. Update TypeScript contracts and consumers. Verify own-farm and visiting-farm behavior, failure recovery, and synchronization. Run only repository-configured checks and report any blocker exactly.
