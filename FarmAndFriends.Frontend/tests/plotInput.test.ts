import assert from 'node:assert/strict'
import test from 'node:test'
import { canReceivePlotInput } from '../src/game/plotInput.ts'

test('owner can interact with unlocked and locked plots', () => {
  assert.equal(canReceivePlotInput({ unlocked: true }, false), true)
  assert.equal(canReceivePlotInput({ unlocked: false }, false), true)
})

test('visitor can interact only with unlocked plots', () => {
  assert.equal(canReceivePlotInput({ unlocked: true }, true), true)
  assert.equal(canReceivePlotInput({ unlocked: false }, true), false)
})
