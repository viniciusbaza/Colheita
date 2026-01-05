import { useUser } from '../user/useUser'
import { useAuth } from '../auth/useAuth'
import { useNavigate } from 'react-router-dom'


export function PlayerHUD() {
  const { user } = useUser()
  const { logout } = useAuth()
  const navigate = useNavigate()

  function handleLogout() {
    logout()
    navigate('/login')
  }

  if (!user) return null

  const xpPercent =
    (user.currentXp / user.xpToNextLevel) * 100

  return (
    <div className="flex items-center justify-between bg-green-900 p-4">
      <div>
        <p className="fixed top-0.5 left-0.5 text-[10px] font-mono text-white/40 uppercase tracking-wider select-none pointer-events-none">ID: {user.id}</p>
        {/* Player Info 
        <div className="mt-4 bg-white p-4 rounded shadow">
          <p>🆔 Id: {user.id}</p>
          <p>👤 Jogador: {user.username}</p>
          <p>⭐ Level: {user.level}</p>
          <p>✨ XP: {user.currentXp}</p>
          <p>⌛ XP para próximo nível: {user.xpToNextLevel}</p>
        </div>
        */}

        <p className="font-bold">👤 {user.username}</p>
        <p className="text-sm">⭐ Level {user.level}</p>

        <div className="w-48 bg-green-700 h-2 rounded mt-1">
          <div
            className="bg-yellow-400 h-2 rounded"
            style={{ width: `${xpPercent}%` }}
          />
        </div>

        <small className="opacity-80">
          {user.currentXp}/{user.xpToNextLevel} XP
        </small>
      </div>

      <button
        onClick={handleLogout}
        className="text-sm bg-red-500 text-white px-3 py-1 rounded hover:bg-red-600"
      >
         Sair
      </button>
    </div>
  )
}
