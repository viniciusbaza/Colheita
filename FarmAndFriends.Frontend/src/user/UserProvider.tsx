import { useCallback, useEffect, useState } from 'react'
import { UserContext } from './UserContext'
import type { User } from './UserContext'
import { authFetch } from '../api/http'
import { useAuth } from '../auth/useAuth'

export function UserProvider({ children }: { children: React.ReactNode }) {
  const { token } = useAuth()
  const [user, setUser] = useState<User | null>(null)
  const [loading, setLoading] = useState(true)

  const loadUser = useCallback(async (showInitialLoading: boolean) => {
    if (!token) {
      setUser(null)
      setLoading(false)
      return
    }

    if (showInitialLoading) {
      setLoading(true)
    }

    try {
      const data = await authFetch<User>('/users/me')
      setUser(data)
    } catch (err) {
      console.error('Erro ao carregar usuário:', err)
      if (showInitialLoading) {
        setUser(null)
      }
      throw err
    } finally {
      if (showInitialLoading) {
        setLoading(false)
      }
    }
  }, [token])

  const refreshUser = useCallback(() => {
    return loadUser(false)
  }, [loadUser])

  useEffect(() => {
    void loadUser(true).catch(() => undefined)
  }, [loadUser])

  return (
    <UserContext.Provider value={{ user, loading, refreshUser }}>
      {children}
    </UserContext.Provider>
  )
}
