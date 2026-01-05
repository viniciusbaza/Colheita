/* export default function Game() {
  return (
    <div className="h-screen flex items-center justify-center text-2xl">
      🌱 Farm And Friends — Game Loading...
    </div>
  )
} */
import { useUser } from '../user/useUser'
import { useAuth } from '../auth/useAuth'
import { useNavigate } from 'react-router-dom'

export default function Game() {
  const { user, loading } = useUser()
  const { logout } = useAuth()
  const navigate = useNavigate()

  function handleLogout() {
    logout()
    navigate('/login')
  }

  if (loading) return <p>Carregando jogador...</p>
  if (!user) return <p>Erro ao carregar jogador</p>

  return (
    <div className="min-h-screen bg-green-100 p-4">
      {/* Header */}
      <div className="flex justify-between items-center bg-white p-4 rounded shadow">
        <h1 className="text-xl font-bold">🌾 Farm And Friends</h1>

        <button
          onClick={handleLogout}
          className="text-sm bg-red-500 text-white px-3 py-1 rounded hover:bg-red-600"
        >
          Sair
        </button>
      </div>

      {/* Player Info */}
      <div className="mt-4 bg-white p-4 rounded shadow">
        <p>🆔 Id: {user.id}</p>
        <p>👤 Jogador: {user.username}</p>
        <p>⭐ Level: {user.level}</p>
        <p>✨ XP: {user.currentXp}</p>
        <p>⌛ XP para próximo nível: {user.xpToNextLevel}</p>
      </div>
    </div>
  )
}
