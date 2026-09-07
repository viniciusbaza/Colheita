import assert from 'node:assert/strict'
import test from 'node:test'
import {
  advanceAsyncSession,
  isAsyncSessionCurrent,
  type AsyncSession,
} from '../src/social/asyncSession.ts'

test('invalidates a pending refresh when the authenticated token changes', () => {
  const accountA: AsyncSession = { token: 'token-a', generation: 0 }
  const pendingRefresh = accountA
  const accountB = advanceAsyncSession(accountA, 'token-b')

  assert.equal(isAsyncSessionCurrent(accountB, pendingRefresh), false)
  assert.equal(accountB.generation, 1)
})

test('invalidates a pending load-more request on logout', () => {
  const authenticated: AsyncSession = { token: 'token-a', generation: 3 }
  const pendingLoadMore = authenticated
  const loggedOut = advanceAsyncSession(authenticated, null)

  assert.equal(isAsyncSessionCurrent(loggedOut, pendingLoadMore), false)
  assert.equal(loggedOut.generation, 4)
})

test('keeps the same session while the token is unchanged', () => {
  const current: AsyncSession = { token: 'token-a', generation: 2 }

  assert.equal(advanceAsyncSession(current, 'token-a'), current)
})
