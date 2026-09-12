---
name: plan-feature
description: Plan a Farm & Friends gameplay, social, economy, or cross-layer feature before implementation, including repository discovery, product decisions, contracts, persistence, testing, rollout, and acceptance criteria.
---

# Plan a Farm & Friends Feature

Create an implementation-ready ExecPlan. Do not modify application code while using this skill.

## Discovery

Read the root `AGENTS.md` and every applicable nested `AGENTS.md`. Inspect the real implementation end to end: entities, services, controllers, DTOs, database configuration, React state and API clients, Phaser scenes/events, tests, and existing documentation. Do not invent files, abstractions, or current behavior.

State what is confirmed, what is inferred, and what remains unknown.

## Product and scope gate

For a new or changed mechanic, evaluate product pillars, social interaction and abuse risks, economy/progression impact, player return value, owner/visitor outcomes, smallest playable version, success metrics, and final verdict (`Prototype`, `Backlog`, or `Reject`). Use the relevant product agents when decisions are unresolved. Do not hide unresolved product decisions inside technical choices.

## Impact and invariants

Classify only applicable areas: `DOMAIN`, `DATABASE`, `API`, `AUTHORIZATION`, `FRONTEND_STATE`, `FRONTEND_UI`, `PHASER_WORLD`, `REACT_PHASER_INTEGRATION`, `TESTING`, `OBSERVABILITY`, and `DOCUMENTATION`.

Preserve backend authority over identity, ownership, permissions, currencies, inventory, XP, yield, rewards, and timing. React owns application state and UI; Phaser owns the interactive world and presentation.

## Plan requirements

For changed API contracts, specify route, method, authentication, request, response, errors, compatibility, and every consumer. For React–Phaser events, specify name, sender, receiver, typed payload, semantic category (`intent`, `confirmed-result`, `state-sync`, `ui-control`, or `visual-only`), firing condition, and cleanup.

For persistence, identify entities, relationships, compatibility with existing data, migration need, rollback, and whether the value is authoritative or derived. Order implementation by dependency, then include tests, documentation, rollout, rollback, risks, and acceptance criteria. Tests must verify resulting state, not only status codes.

## Output

Use these sections: `Summary`, `Current State`, `Invariants`, `Approved Decisions`, `Pending Decisions`, `Impact by Area`, `Contracts`, `Persistence`, `Implementation Sequence`, `Test Strategy`, `Risks`, `Rollout and Rollback`, `Acceptance Criteria`, `Responsible Agents`, and `Status`.

Status must be one of: `READY FOR IMPLEMENTATION`, `NEEDS DECISION`, `BLOCKED`, or `INVESTIGATION INCOMPLETE`.
