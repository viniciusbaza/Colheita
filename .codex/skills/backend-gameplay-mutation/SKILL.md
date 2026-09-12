---
name: backend-gameplay-mutation
description: Safely implement or review a Farm & Friends backend mutation that changes gameplay state, inventory, currency, XP, yield, rewards, ownership, or timing.
---

# Backend Gameplay Mutation

Read the root and API `AGENTS.md`, then trace controller → service/domain rule → EF Core persistence → response and related tests. Keep controllers transport-focused and reuse an existing service when it already owns the concept.

Before editing, write down the invariant being protected and the authoritative sources for identity, ownership, prices, rewards, quantities, timestamps, and permissions. Validate all of them server-side. Distinguish unauthenticated, forbidden, not found, invalid state, and conflict according to existing conventions.

For multi-record changes, choose and document the smallest safe transaction boundary. Analyze duplicate requests and concurrent operations against plots, inventory, currency, XP, and yield. Use one captured UTC server time per operation. Prevent negative balances, quantities, yield, and duplicate rewards. Create a migration only for a persistent model change; inspect generated migrations and data compatibility.

Update explicit DTO/API contracts and focused tests. Cover happy path plus authorization, ownership, missing/invalid state, insufficient resources, repeated requests, time boundaries, and concurrency when applicable. Verify resulting database state, build the affected project, run relevant tests, and inspect the final diff.
