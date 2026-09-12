---
name: react-phaser-integration
description: Design or modify the Farm & Friends React–Phaser bridge, including intents, confirmed results, farm synchronization, UI control, typed payloads, lifecycle cleanup, and visual feedback.
---

# React–Phaser Integration

Read the frontend and closest game-world `AGENTS.md`. Inspect the established bridge, current event names, senders, receivers, payloads, scene lifecycle, plot collections, texture loading, and synchronization path before introducing anything.

Assign ownership explicitly: React controls API calls, application state, HUD, panels, modals, navigation, visit session, and messaging; Phaser controls world objects, input, positioning, animations, and effects. Backend state remains authoritative. Phaser must not authorize actions, calculate rewards, call gameplay APIs directly, or use local timers to grant outcomes.

For each new or changed event, document name, sender, receiver, typed payload with stable IDs, semantic category (`intent`, `confirmed-result`, `state-sync`, `ui-control`, or `visual-only`), firing condition, and cleanup. Do not use intent as proof of success. Update authoritative React state before visual confirmation where possible.

Make synchronization idempotent: repeated snapshots must not duplicate plots, listeners, badges, glows, tweens, or rewards. Respect modal input blocking, own/visitor modes, scene restart, plot removal, failed requests, and pending-action guards. Stop timers/tweens and remove listeners when objects or scenes are replaced. Validate initial load, repeated sync, action success/failure, modal open/close, and returning from a friend farm.
