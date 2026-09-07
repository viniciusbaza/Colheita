import assert from 'node:assert/strict'
import test from 'node:test'
import { withAvatarSaveDeadline } from '../src/api/avatarSaveDeadline.ts'

test('avatar save returns a confirmed response before its deadline', async () => {
  const response = await withAvatarSaveDeadline(async () => ({ avatarId: 'avatar-2' }))
  assert.deepEqual(response, { avatarId: 'avatar-2' })
})

test('a hung avatar save is aborted and releases the pending operation', async () => {
  let requestSignal: AbortSignal | undefined
  await assert.rejects(withAvatarSaveDeadline(signal => {
    requestSignal = signal
    return new Promise<never>(() => undefined)
  }, 5), /confirmar o salvamento/)
  assert.equal(requestSignal?.aborted, true)
})

test('an avatar save failure remains recoverable without waiting for the deadline', async () => {
  await assert.rejects(withAvatarSaveDeadline(async () => {
    throw new Error('offline')
  }), /offline/)
  assert.equal(await withAvatarSaveDeadline(async () => 'avatar-3'), 'avatar-3')
})
