# Profile personalization — execution plan

Approved by the user: five free farm-animal portraits (cow, hen, pig, sheep, horse), profile editor opened through the HUD, same image in the accepted-friends list. Farm name editing. No image upload, economy or notifications changes.

## Frozen contract

- Stable IDs: `avatar-1` through `avatar-5`, in animal order above. Default `avatar-1`.
- Persist required `User.AvatarId`, maximum 32 characters, with an additive migration and default for existing accounts. No database reset.
- Add `avatarId`, `farmId` and `farmName` to `GET /users/me`, and add `avatarId` to the accepted `GET /friends` projection.
- Authenticated `PATCH /users/me/avatar` accepts `{ avatarId }` and returns `200 { avatarId }` as a compatibility endpoint. Invalid selection is 400, invalid/missing identity is 401. User is derived from JWT only. Repeated selection is idempotent.
- Authenticated `PATCH /users/me/profile` accepts `{ avatarId, farmName }` and returns `{ avatarId, farmId, farmName }`. Avatar and the trimmed, required farm name commit together; the avatar-only endpoint remains compatible.
- Backend allowlist is authoritative; frontend catalog only resolves known IDs to bundled art and labels, falling back to cow for older responses.

## Ownership and sequence

1. Primary: artwork and this plan, contract freeze.
2. Backend engineer: entity/configuration, DTOs, user/friend controllers, migration and backend tests.
3. Frontend engineer: profile catalog/renderer/modal, user state/API, HUD, friends list and frontend tests. Preserve existing dirty work.
4. Primary: integration, documentation and validation; no world logic change. Existing `ui:modal` event uses `{ source: 'profile', open }` and must clean up on close/unmount.
5. Read-only QA, architecture and gameplay reviews; fix confirmed findings before handoff.

## Acceptance and validation

Selection and farm-name edits are provisional until saved; cancel/close/Escape discard both drafts. Save errors remain recoverable and repeated clicks do not duplicate requests. A confirmed save closes the modal immediately without another completion action. The confirmed profile update preserves concurrent XP. Old-session and stale-fetch responses cannot overwrite the latest profile.

`UserContext` owns the authenticated profile and own-farm identity summary.
`FarmContext` owns the farm snapshot currently rendered, which may be a friend's
farm. The confirmed profile flow coordinates both projections; during a visit,
it updates the cached own farm without renaming the visible destination.

HUD displays the signed-in user's avatar even during visits. Mobile at 320/360/390 pixels and low landscape retains usable targets, contrast, no overlap and no invisible canvas blocker. Modal restores focus, traps keyboard navigation and blocks Phaser only while open.

Backend checks: all five valid choices; invalid/null/missing; farm-name normalization and length; authentication/isolation; repeated save; unchanged XP/economy; atomic profile update; own/friends projection; migration compatibility. Frontend checks: catalog uniqueness/fallback, both drafts, save/cancel/error/retry, session races and coordinated profile/own-farm state updates. Run frontend tests/lint/build and focused backend tests; report database-dependent skips honestly.

## Status

Completed on 2026-09-04. Five original portraits are bundled in
`FarmAndFriends.Frontend/src/assets/avatars`; their README records the exact
generation prompts and provenance. No gameplay or economy rule changes.

The additive migration was applied to the local development database without
resetting data. Other environments must apply the normal migration workflow.

## Validation results

- Initial avatar slice: 93/93 frontend tests, global lint and production build
  passed. The final combined profile result is recorded in the extension below.
  Build retains the existing large JavaScript chunk warning.
- Backend implementation validation: 20/20 focused avatar and baseline checks
  passed against isolated PostgreSQL, with no skips. Broader suite: 127 passed,
  87 database-dependent skips, zero failures. Backend build and EF model/migration
  consistency checks passed. These backend results are from the implementation
  session; the final resumption did not rerun the database suite.
- QA, architecture and gameplay reviews found a potentially indefinite pending
  save. Fixed with a 15-second deadline plus abort and recoverable feedback;
  regression tests cover a transport that ignores abort. Final QA recheck found
  no remaining blocking defect.
- Browser checks using local synthetic accounts: save, cancel, reload and next
  session persistence; own avatar during a visit; friend's different saved
  avatar in the list; Escape and focus return; focus containment; logout
  confirmation; repeated visit through the feed keeps the current farm usable.
- Responsive checks: 320x568, 360x640, 390x844, 640x360 and 1366x768. Profile
  content scrolls in low landscape while footer actions remain visible. Mobile
  friend actions use a second row so the avatar does not squeeze the name.

## Remaining non-blocking limitations

- The five original PNGs total approximately 8.74 MB. Smaller delivery variants
  remain a performance improvement for a later pass; original artwork is kept.
- Frontend automated tests cover policies, reducers and async helpers, not a
  mounted React/Provider or a browser network-timeout test. Manual browser
  checks complement, but do not replace, that missing automation.
- Coordination between `UserProvider` and `FarmProvider` currently happens in
  `ProfilePanel`. A future structural refinement may move it into a dedicated
  application hook while preserving the two distinct read-model responsibilities.
- No commit, deployment, upload feature or analytics infrastructure was added.

## Farm-name profile extension — 2026-09-04

The profile modal now edits the existing farm name together with the avatar.
The authenticated `PATCH /users/me/profile` validates both values and commits
them atomically; `GET /users/me` includes the owned farm identity. Registration
and profile editing share the same trimmed, required, 30-character name rule.
No migration was added.

On successful confirmation the modal closes immediately, with no separate
"Concluir" action. Errors keep both drafts. Shared user and own-farm state update
immediately, invalidate older own-farm reads, and never rename the active friend
farm during a visit.

Final extension validation: backend build passed; 24/24 profile contract tests
and 1/1 isolated PostgreSQL integration test passed, including atomic rollback
and unchanged XP/currency. Frontend finished with 95/95 tests, lint and build
passing. Browser checks covered mobile layout, combined save, automatic close,
immediate HUD update, edit while visiting, isolation of the friend's farm, and
restoration of the synthetic account's original profile. QA found two state
synchronization races; both were fixed in the implementation review. The
remaining provider-coordination improvement is documented as a future structural
refinement, not an unresolved persistence or authorization defect.
