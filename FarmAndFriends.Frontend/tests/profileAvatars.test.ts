import assert from 'node:assert/strict'
import test from 'node:test'
import { AVATARS, getAvatar } from '../src/profile/avatarCatalog.ts'
import {
  canSaveProfile,
  createProfileEditor,
  getTrimmedFarmName,
  profileEditorReducer,
} from '../src/profile/profileEditor.ts'
import { applySavedProfile, mergeUserRefresh } from '../src/user/userUpdates.ts'
import { advanceAsyncSession, isAsyncSessionCurrent } from '../src/social/asyncSession.ts'

const player = {
  id: 'owner',
  username: 'Jogador',
  avatarId: 'avatar-1',
  farmId: 'farm-owner',
  farmName: 'Fazenda Feliz',
  level: 2,
  currentXp: 12,
  xpToNextLevel: 40,
}

const initialEditor = () => createProfileEditor({
  avatarId: player.avatarId,
  farmName: player.farmName,
})

test('five stable unique IDs map to five distinct farm animals', () => {
  assert.deepEqual(AVATARS.map(avatar => avatar.id), ['avatar-1', 'avatar-2', 'avatar-3', 'avatar-4', 'avatar-5'])
  assert.equal(new Set(AVATARS.map(avatar => avatar.asset)).size, 5)
  for (const avatar of AVATARS) assert.equal(getAvatar(avatar.id), avatar)
})

test('missing, old, external and unknown avatar IDs use cow presentation fallback', () => {
  for (const id of [undefined, null, '', 'https://example.com/avatar.png', 'avatar-6']) {
    assert.equal(getAvatar(id).id, 'avatar-1')
  }
})

test('avatar and farm name changes remain drafts and cancel restores both', () => {
  const avatarChanged = profileEditorReducer(initialEditor(), { type: 'select', avatarId: 'avatar-4' })
  const changed = profileEditorReducer(avatarChanged, { type: 'edit-farm-name', farmName: 'Sítio da Serra' })
  assert.equal(changed.savedAvatar, 'avatar-1')
  assert.equal(changed.savedFarmName, 'Fazenda Feliz')
  assert.equal(changed.draftAvatar, 'avatar-4')
  assert.equal(changed.draftFarmName, 'Sítio da Serra')
  assert.deepEqual(profileEditorReducer(changed, { type: 'cancel' }), initialEditor())
  assert.equal(canSaveProfile(initialEditor()), false)
})

test('farm name is trimmed for validation and save eligibility', () => {
  const unchanged = profileEditorReducer(initialEditor(), {
    type: 'edit-farm-name',
    farmName: '  Fazenda Feliz  ',
  })
  assert.equal(getTrimmedFarmName(unchanged), 'Fazenda Feliz')
  assert.equal(canSaveProfile(unchanged), false)

  const changed = profileEditorReducer(initialEditor(), {
    type: 'edit-farm-name',
    farmName: '  Recanto Verde  ',
  })
  assert.equal(getTrimmedFarmName(changed), 'Recanto Verde')
  assert.equal(canSaveProfile(changed), true)

  const empty = profileEditorReducer(initialEditor(), {
    type: 'edit-farm-name',
    farmName: '   ',
  })
  assert.equal(canSaveProfile(empty), false)
})

test('pending profile save locks repeated saves, selection, editing and cancellation', () => {
  const selected = profileEditorReducer(initialEditor(), { type: 'select', avatarId: 'avatar-5' })
  const saving = profileEditorReducer(selected, { type: 'save' })
  assert.equal(saving.saving, true)
  assert.equal(canSaveProfile(saving), false)
  assert.equal(profileEditorReducer(saving, { type: 'save' }), saving)
  assert.equal(profileEditorReducer(saving, { type: 'select', avatarId: 'avatar-2' }), saving)
  assert.equal(profileEditorReducer(saving, { type: 'edit-farm-name', farmName: 'Outro nome' }), saving)
  assert.equal(profileEditorReducer(saving, { type: 'cancel' }), saving)
})

test('failed save keeps both drafts for retry; confirmed save adopts server values', () => {
  const avatarChanged = profileEditorReducer(initialEditor(), { type: 'select', avatarId: 'avatar-3' })
  const changed = profileEditorReducer(avatarChanged, { type: 'edit-farm-name', farmName: 'Recanto Verde' })
  const saving = profileEditorReducer(changed, { type: 'save' })
  const failed = profileEditorReducer(saving, { type: 'failed', message: 'Sem rede' })
  assert.equal(failed.draftAvatar, 'avatar-3')
  assert.equal(failed.draftFarmName, 'Recanto Verde')
  assert.equal(failed.savedAvatar, 'avatar-1')
  assert.equal(failed.savedFarmName, 'Fazenda Feliz')
  assert.equal(canSaveProfile(failed), true)

  const retry = profileEditorReducer(failed, { type: 'save' })
  assert.equal(retry.error, null)
  const saved = profileEditorReducer(retry, {
    type: 'saved',
    avatarId: 'avatar-3',
    farmName: 'Recanto Verde',
  })
  assert.equal(saved.savedAvatar, 'avatar-3')
  assert.equal(saved.savedFarmName, 'Recanto Verde')
  assert.equal(canSaveProfile(saved), false)
})

test('applying confirmed profile preserves concurrent XP and all unrelated user fields', () => {
  const withXp = { ...player, currentXp: 26 }
  assert.deepEqual(applySavedProfile(withXp, {
    avatarId: 'avatar-5',
    farmId: 'farm-owner',
    farmName: 'Sítio do Sol',
  }), {
    ...withXp,
    avatarId: 'avatar-5',
    farmName: 'Sítio do Sol',
  })
  assert.equal(withXp.avatarId, 'avatar-1')
  assert.equal(withXp.farmName, 'Fazenda Feliz')
})

test('fetch started before save cannot revert the confirmed profile', () => {
  const saved = applySavedProfile(player, {
    avatarId: 'avatar-4',
    farmId: 'farm-owner',
    farmName: 'Vale das Ovelhas',
  })
  const merged = mergeUserRefresh(
    saved,
    player,
    { profile: 0, xp: 0 },
    { profile: 1, xp: 0 },
  )
  assert.equal(merged.avatarId, 'avatar-4')
  assert.equal(merged.farmName, 'Vale das Ovelhas')
})

test('fetch crossing XP and profile updates preserves both confirmed changes', () => {
  const updated = {
    ...player,
    currentXp: 30,
    avatarId: 'avatar-2',
    farmName: 'Granja Dourada',
  }
  assert.deepEqual(
    mergeUserRefresh(updated, player, { profile: 0, xp: 0 }, { profile: 1, xp: 1 }),
    updated,
  )
})

test('fresh fetch supplies authoritative fields without copying old identity state', () => {
  const incoming = {
    ...player,
    currentXp: 31,
    avatarId: 'avatar-3',
    farmName: 'Chácara Nova',
  }
  const revision = { profile: 1, xp: 1 }
  assert.deepEqual(mergeUserRefresh(player, incoming, revision, revision), incoming)
  assert.deepEqual(mergeUserRefresh(null, incoming, revision, revision), incoming)
  const differentPlayer = { ...incoming, id: 'friend', farmId: 'farm-friend' }
  assert.deepEqual(
    mergeUserRefresh(player, differentPlayer, { profile: 0, xp: 0 }, revision),
    differentPlayer,
  )
})

test('save and refresh session markers become stale on logout, account swap and same-token re-login', () => {
  const session = { token: 'account-a', generation: 0 }
  const logout = advanceAsyncSession(session, null)
  const swapped = advanceAsyncSession(session, 'account-b')
  const relogin = advanceAsyncSession(logout, 'account-a')
  for (const next of [logout, swapped, relogin]) {
    assert.equal(isAsyncSessionCurrent(next, session), false)
  }
})
