import { useUser } from '../user/useUser'
import { useAuth } from '../auth/useAuth'
import { useNavigate } from 'react-router-dom'
import { useFarm } from '../farm/FarmContext'
import { useEffect, useState } from 'react'
import { xpToNextLevel } from '../rules/levelProgression'
import { FriendsPanel } from './FriendsPanel'

export function PlayerHUD() {
  const { user } = useUser()
  const { farm, isVisiting, visitFarm, returnToOwnFarm, session } = useFarm()
  const { logout } = useAuth()
  const navigate = useNavigate()
  const [showFriends, setShowFriends] = useState(false) 

  const mockFriends = [
    {
      userId: '2f6fe179-232d-4c60-b54e-6b52fca32a60',
      username: 'tst',
      farmId: 'f2405b6f-4604-4a60-8f3b-4565bcd2d894',
      farmName: 'Fazenda do Rato',
      avatarUrl: ''
    },
    {
      userId: 'b1ca7432-971f-4abc-a3db-9200e8c93323',
      username: 'usr',
      farmId: '9079ef3d-98e0-41db-9972-f8dfa3611e1a',
      farmName: 'Fazenda Feliz',
      avatarUrl: ''
    },
    {
      userId: '3',
      username: 'Beltrano',
      farmId: '',
      farmName: 'Sunny Farm',
      avatarUrl: ''
    },
    {
      userId: '4',
      username: 'Mimi',
      farmId: '',
      farmName: 'Cantinho Verde',
      avatarUrl: ''
    }
  ]

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

      {isVisiting && (
        <div className="fixed top-24 left-1/2 -translate-x-1/2 z-40
                        bg-amber-100 border border-amber-300
                        rounded-full px-3 py-1 shadow
                        flex items-center gap-3">
          <span className="text-sm text-amber-900">
            👀 Visitando {farm?.name} de <strong>{session.ownerUsername}</strong>
          </span>

          <button
            onClick={returnToOwnFarm}
            className="text-sm bg-amber-500 text-white
                      px-3 py-1 rounded-full
                      hover:bg-amber-600 transition"
          >
            ⬅ Voltar
          </button>
        </div>
      )}

      <div className="fixed top-7 right-20 z-40">
        {/* DEV ONLY — será movido para o menu radial */}
        <button
          onClick={() => setShowFriends(true)}
          className="rounded-full bg-emerald-600 px-4 py-1 text-white shadow hover:bg-emerald-700"
        >
          👥
        </button>
      </div>

      {showFriends && (
        <FriendsPanel
          friends={mockFriends}
          onClose={() => setShowFriends(false)}
          onVisitFriend={(friend) => {
            visitFarm(
              friend.farmId,
              friend.userId,
              friend.username
            )
          }}
        />
      )}

      <button
        onClick={handleLogout}
        className="text-sm bg-red-500 text-white px-3 py-1 rounded-full hover:bg-red-600"
      >
         Sair
      </button>
    </div>
  )
}
