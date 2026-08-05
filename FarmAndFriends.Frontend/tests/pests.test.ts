import assert from 'node:assert/strict'
import test from 'node:test'
import { hasServerPestProtection } from '../src/utils/pests.ts'

test('protection availability follows server presence, not the browser clock', () => {
  assert.equal(
    hasServerPestProtection('2020-01-01T00:00:00.000Z'),
    true,
  )
  assert.equal(hasServerPestProtection(null), false)
})
