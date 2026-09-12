---
name: change-api-contract
description: Change a Farm & Friends API contract while keeping ASP.NET Core responses, TypeScript clients, React state, and Phaser consumers synchronized and compatible.
---

# Change an API Contract

Read the root, backend, frontend, and Phaser guidance that applies. Locate the route, controller, DTOs, service result, serialization configuration, API client, TypeScript types, hooks/contexts, components, event bridge, and tests before changing anything. Search all consumers of every added, removed, renamed, or retyped field.

Document route, method, authentication, request, response, error statuses, compatibility strategy, and affected consumers. Prefer additive changes. Never silently change the meaning of an existing field or expose EF entities/internal data. Keep authorization and authoritative calculations on the server.

Update the backend contract and implementation, frontend types and API function, state consumers, UI, Phaser integration when applicable, and tests as one coordinated change. For React–Phaser changes document event name, sender, receiver, payload, semantic category, timing, and cleanup. Verify serialization with realistic responses, rejected operations, loading/error states, own/visitor views, and the repository's configured build/tests.
