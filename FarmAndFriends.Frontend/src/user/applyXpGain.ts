import { xpToNextLevel } from '../rules/levelProgression.ts'
import type { User } from './UserContext'

export function applyXpGain(user: User, xpGained: number): User {
  let currentXp = user.currentXp + xpGained
  let level = user.level
  let nextLevelXp = xpToNextLevel(level)

  while (currentXp >= nextLevelXp) {
    currentXp -= nextLevelXp
    level += 1
    nextLevelXp = xpToNextLevel(level)
  }

  return {
    ...user,
    level,
    currentXp,
    xpToNextLevel: nextLevelXp,
  }
}
