import assert from 'node:assert/strict'
import test from 'node:test'
import type { User } from '../src/user/UserContext.tsx'
import { applyXpGain } from '../src/user/applyXpGain.ts'

function createUser(overrides: Partial<User> = {}): User {
  return {
    id: 'user-id',
    username: 'farmer',
    farmId: 'farm-id',
    farmName: 'Fazenda Feliz',
    level: 1,
    currentXp: 10,
    xpToNextLevel: 100,
    ...overrides,
  }
}

test('applies gained XP without changing level', () => {
  const updatedUser = applyXpGain(createUser(), 45)

  assert.equal(updatedUser.level, 1)
  assert.equal(updatedUser.currentXp, 55)
  assert.equal(updatedUser.xpToNextLevel, 100)
})

test('carries excess XP across levels', () => {
  const updatedUser = applyXpGain(
    createUser({ currentXp: 90 }),
    400,
  )

  assert.equal(updatedUser.level, 3)
  assert.equal(updatedUser.currentXp, 140)
  assert.equal(updatedUser.xpToNextLevel, 500)
})
