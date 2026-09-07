import type { SavedProfile, User } from './UserContext'

export type UserRevisions = { profile: number; xp: number }

export function applySavedProfile(user: User, profile: SavedProfile): User {
  return {
    ...user,
    avatarId: profile.avatarId,
    farmId: profile.farmId,
    farmName: profile.farmName,
  }
}

export function mergeUserRefresh(
  current: User | null,
  fetched: User,
  captured: UserRevisions,
  latest: UserRevisions,
): User {
  if (!current || current.id !== fetched.id) return fetched
  return {
    ...fetched,
    ...(captured.profile !== latest.profile ? {
      avatarId: current.avatarId,
      farmId: current.farmId,
      farmName: current.farmName,
    } : {}),
    ...(captured.xp !== latest.xp ? {
      level: current.level,
      currentXp: current.currentXp,
      xpToNextLevel: current.xpToNextLevel,
    } : {}),
  }
}
