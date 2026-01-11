import { useUser } from '../user/useUser'
import { useAuth } from '../auth/useAuth'
import { useNavigate } from 'react-router-dom'
import { useFarm } from '../farm/useFarm'
import { useEffect, useState } from 'react'
import { xpToNextLevel } from '../rules/levelProgression'

export function PlayerHUD() {
  const { user } = useUser()
  const { farm } = useFarm()
  const { logout } = useAuth()
  const navigate = useNavigate()

  function handleLogout() {
    logout()
    navigate('/login')
  }

  if (!user) return null

  const [displayUser, setDisplayUser] = useState(user)

  useEffect(() => {
    setDisplayUser(user)
  }, [user])

  useEffect(() => {
    function onHarvest(e: Event) {
      const { xpGained } = (e as CustomEvent<{ xpGained: number }>).detail

      setDisplayUser(prev => {
        if (!prev) return prev

        let newXp = prev.currentXp + xpGained
        let newLevel = prev.level
        let xpToNext = xpToNextLevel(newLevel)

        // ⬆️ level up
        while (newXp >= xpToNext) {
          newXp -= xpToNext
          newLevel += 1
          xpToNext = xpToNextLevel(newLevel) // mesma regra do backend
        }

        return {
          ...prev,
          level: newLevel,
          currentXp: newXp,
          xpToNextLevel: xpToNext,
        }
      })
    }

    window.addEventListener('plot:harvest:done', onHarvest)
    return () =>
      window.removeEventListener('plot:harvest:done', onHarvest)
  }, [])

  const xpPercent = (displayUser.currentXp / displayUser.xpToNextLevel) * 100

  return (
    <div className="fixed top-0 left-0 right-0 z-50 flex items-center justify-between bg-green-900 p-4 pointer-events-auto">
      <div className='hidden sm:block'>
        <p className="fixed top-0.5 left-0.5 text-[10px] font-mono text-white/40 tracking-wider select-none pointer-events-none">ID: {user.id}</p>
        <p className="text-sm">🌾 Fazenda: {farm?.name}</p>
        <p className="font-bold">👤 {displayUser.username}</p>
      </div>
      <div>
        <p className="text-sm">⭐ Level {displayUser.level}</p>

        <div className="w-32 sm:w-48 bg-green-700 h-2 rounded mt-1 overflow-hidden">
          <div
            className="bg-yellow-400 h-2 rounded transition-all duration-500 ease-out"
            style={{ width: `${xpPercent}%` }}
          />
        </div>

        <small className="opacity-80">
          {displayUser.currentXp}/{displayUser.xpToNextLevel} XP
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
