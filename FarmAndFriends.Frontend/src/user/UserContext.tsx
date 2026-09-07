import { createContext } from 'react'
import type { AvatarId } from '../profile/avatarCatalog'

export type User = {
  id: string
  username: string
  avatarId?: string | null
  farmId: string
  farmName: string
  level: number
  currentXp: number
  xpToNextLevel: number
}

export type SaveProfileInput = {
  avatarId: AvatarId
  farmName: string
}

export type SavedProfile = {
  avatarId: string
  farmId: string
  farmName: string
}

export type UserContextType = {
  user: User | null
  loading: boolean
  refreshUser: () => Promise<void>
  addXp: (xpGained: number) => void
  saveProfile: (profile: SaveProfileInput) => Promise<SavedProfile>
}

export const UserContext = createContext<UserContextType | null>(null)
