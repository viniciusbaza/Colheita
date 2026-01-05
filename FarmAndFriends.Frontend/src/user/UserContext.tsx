import { createContext } from 'react'

export type User = {
  id: string
  username: string
  level: number
  currentXp: number
  xpToNextLevel: number
}

export type UserContextType = {
  user: User | null
  loading: boolean
  refreshUser: () => Promise<void>
}

export const UserContext = createContext<UserContextType | null>(null)
