import { useEffect, useState } from 'react'
import { UserContext } from './UserContext'
import type { User } from './UserContext'
import { authFetch } from '../api/http'
import { useAuth } from '../auth/useAuth'

export function UserProvider({ children }: { children: React.ReactNode }) {
  const { token } = useAuth()
  const [user, setUser] = useState<User | null>(null)
  const [loading, setLoading] = useState(true)

  async function refreshUser() {
    if (!token) {
      setUser(null)
      setLoading(false)
      return
    }

    try {
      setLoading(true)
      const data = await authFetch<User>('/users/me')
      setUser(data)
    } catch (err) {
      console.error('Erro ao carregar usuário:', err)
      setUser(null)
    } finally {
      setLoading(false)
    }
    console.log('USER PROVIDER → user:', user)
  }

  useEffect(() => {
    refreshUser()
  }, [token])

  return (
    <UserContext.Provider value={{ user, loading, refreshUser }}>
      {children}
    </UserContext.Provider>
  )
}
