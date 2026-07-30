# `docs/decisions/README.md`

# Farm & Friends — Architecture and Product Decisions

This directory contains Architecture Decision Records and other durable project
decisions.

A decision record explains:

* the problem;
* the context;
* the options considered;
* the selected option;
* the reasons;
* the consequences.

Decision records should explain why a choice was made.

Current behavior belongs in:

* `docs/gameplay/` for gameplay rules;
* `docs/architecture/` for current technical structure.

## When to create a decision record

Create an ADR when a decision:

* affects multiple parts of the system;
* establishes a long-term technical boundary;
* changes a major gameplay principle;
* changes persistence or API strategy;
* introduces an important dependency;
* resolves a meaningful trade-off;
* is likely to be questioned later;
* constrains future implementation.

Do not create an ADR for:

* renaming a local variable;
* fixing a typo;
* adjusting CSS spacing;
* adding a small test;
* routine implementation details;
* temporary debugging changes.

## File naming

Use:

```text
ADR-0001-short-description.md
ADR-0002-short-description.md
ADR-0003-short-description.md
```

Use lowercase words separated by hyphens after the number.

Examples:

```text
ADR-0001-backend-authority.md
ADR-0002-react-phaser-boundary.md
ADR-0003-server-side-growth-time.md
ADR-0004-protected-owner-yield.md
```

## Decision status

Use one of:

* `Proposed`;
* `Accepted`;
* `Superseded`;
* `Rejected`;
* `Deprecated`.

A superseded decision must link to the newer ADR.

Do not delete an accepted ADR merely because the decision changed.

Preserving the old record explains the project's history.

## ADR template

```markdown
# ADR-NNNN — Decision title

Status: Proposed

Date: YYYY-MM-DD

Decision owners:

- Name or role

## Context

Describe the problem and why a decision is required.

## Decision drivers

List the most important requirements or constraints.

## Options considered

### Option A — Name

Describe the option.

Advantages:

- advantage;

Disadvantages:

- disadvantage.

### Option B — Name

Describe the option.

Advantages:

- advantage;

Disadvantages:

- disadvantage.

## Decision

Describe the selected option clearly.

## Consequences

### Positive

- positive consequence.

### Negative

- cost, limitation or trade-off.

### Neutral or operational

- process or implementation consequence.

## Implementation guidance

Describe boundaries that future work must follow.

Do not turn this section into a complete implementation plan.

## Validation

Explain how the project will verify that the decision is being followed.

## Related documents

- `docs/gameplay/...`
- `docs/architecture/...`
- related ADRs
```
