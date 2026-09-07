import assert from 'node:assert/strict'
import test from 'node:test'
import { getVisitRecoveryFeedback } from '../src/farm/visitRecovery.ts'

test('recovers a denied visit with a contextual in-game message', () => {
  assert.deepEqual(
    getVisitRecoveryFeedback({ status: 403 }, 'Ana'),
    {
      message: 'A visita a Ana não está mais disponível. Você voltou para sua fazenda.',
    },
  )
})

test('uses a generic message when the visited owner is unknown', () => {
  assert.deepEqual(
    getVisitRecoveryFeedback({ status: 403 }, '   '),
    {
      message: 'Essa visita não está mais disponível. Você voltou para sua fazenda.',
    },
  )
})

test('does not recover transient or unrelated request failures', () => {
  assert.equal(getVisitRecoveryFeedback({ status: 500 }, 'Ana'), null)
  assert.equal(getVisitRecoveryFeedback(new TypeError('offline'), 'Ana'), null)
  assert.equal(getVisitRecoveryFeedback(null, 'Ana'), null)
})
