import { useState } from 'react'
import { AuthContext } from './AuthContext'

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [token, setToken] = useState<string | null>(
    () => localStorage.getItem('access_token')
  )

  function login(newToken: string) {
    localStorage.setItem('access_token', newToken)
    setToken(newToken)
  }

  function logout() {
    localStorage.removeItem('access_token')
    setToken(null)
    window.location.href = '/login'
  }

  return (
    <AuthContext.Provider
      value={{
        token,
        isAuthenticated: !!token,
        login,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  )
}
