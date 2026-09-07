# Farm events feed and floating HUD — execution plan

## Decision

Prototype approved by the user. Transform notifications into a seven-day farm
events feed with contextual social return, and replace the continuous green top
bar with protected floating HUD blocks. The feature creates no new reward,
streak, revenge mechanic, passive-visit log or reciprocity pressure.

Status: Implemented and validated locally on 2026-09-05. The recommendation to
hide a visit CTA for the farm already active remains an unapproved follow-up.

## Scope and ownership

- Backend: feed query, cursor, UTC window, read operations and recipient
  isolation.
- React: feed state, contextual-action policy, panels, HUD, visit navigation,
  logout confirmation and recovery feedback.
- Phaser: world camera and rendering; it consumes established modal, farm-sync
  and camera events without owning feed or authorization rules.
- Database: reuse existing notifications; the feed requires no migration.
- Review: QA, gameplay and architecture validate the completed vertical slice.

Implementation sequence: preserve the legacy endpoints, add and test the feed
contract, integrate paginated social state, build the Acontecimentos panel,
stabilize repeated/denied visits, replace the HUD surface and then validate
responsive layouts and documentation.

## Player outcome and guardrails

The feed answers who interacted, what happened and when. Stories remain after
being seen and are grouped by category, read state and day. Farm help and theft
remain separate; return visits are neutral navigation and do not reveal ready
crops or the current state of a friend's farm.

The visible visit CTA is intentionally **Visitar** because the actor's name is
already present in the event text. Compact actions inside event cards retain a
36-pixel minimum height by explicit product decision. HUD shortcuts retain
larger touch targets.

## Backend contract

```text
GET   /notifications/feed?cursor&take=50
PATCH /notifications/feed/read-all
PATCH /notifications/{notificationId}/read
```

The server calculates a seven-day UTC window and orders by
`CreatedAt DESC, Id DESC`. The opaque cursor contains both values. The response
is `{ items, nextCursor, windowStartUtc, unreadCount }`. Reading changes
`ReadAt` only. Existing notification endpoints remain compatible.

No migration is required for the feed. Existing notifications already persist
actor, type, message, references, creation and read state. Authorization remains
on `GET /farms/{farmId}`; frontend friendship state controls whether a CTA is
presented but never grants access.

## Frontend and visit state

The current social polling loads the first page and merges additional pages by
notification ID. A testable policy maps an event plus the current accepted
friends projection to either a visit, the friend-request area or no action.

Requesting the farm already active is ignored by the visit state machine, so it
does not restart loading. A `403` during a visit invalidates the denied session,
restores the cached own farm while an authoritative refresh runs and displays
in-game feedback.

Follow-up recommendation, not implemented or approved in this cut: pass the
active farm ID into the contextual-action policy and hide the visit CTA when
the player is already at that destination. The story and read action would
remain available.

## Floating HUD

The full-width opaque strip is replaced by two translucent, bordered and
blurred blocks. The left block protects player/farm, currency and XP information;
the right block contains events, friends, inventory, shop and logout. Transparent
wrapper areas do not receive pointer events, while controls opt back in.

Mobile compacts the status block and keeps logout inside the compact layout.
Toast and visiting indicators avoid the HUD. Camera actions use a magnifier with
plus/minus symbols while retaining accessible labels. Logout requires an in-game
confirmation dialog.

## Acceptance and validation

Backend coverage includes authentication, recipient isolation, the exact
seven-day boundary, equal-timestamp pagination and individual/batch reads.
Frontend automation covers the five visitable types, absent actors/friendships,
page deduplication, the repeated-visit decision and mapping of denied-visit
recovery. Loading, empty, error, repeated-click and responsive presentation were
validated manually during implementation; mounted component coverage remains a
non-blocking future improvement.

No action in this feature changes coins, premium currency, XP, yield or existing
care, pest and theft rewards.

## Risks, rollout and rollback

The main product risks are excessive negative-event concentration, return visits
that lead directly to theft and reduced discovery of shop or inventory after the
HUD moves. The main technical risks are stale friendship projections, repeated
navigation, inaccessible compact controls and pagination duplication. Existing
authorization and economy endpoints remain unchanged and authoritative.

The feed and HUD require no migration. Deploy the API before or together with
the typed frontend and check the feed, HUD and visit recovery at supported
mobile and desktop sizes. If this working tree is deployed together with profile
personalization, its additive avatar migration follows that feature's separate
rollout contract.

Rollback may return the frontend to the legacy notification list and prior HUD
while leaving the additive feed endpoint in place. The legacy notification
endpoints remain compatible, so rollback does not require deleting player
history.

## Future measurement contract

No analytics infrastructure is added now. A future implementation should define
the privacy-reviewed events `events_feed_opened`, `events_feed_cta_shown`,
`events_feed_visit_started`, `friend_visit_completed`,
`friend_visit_meaningful_interaction`, `hud_shortcut_opened` and
`friendship_removed`. It must not send message text, farm contents or ready-crop
state.
