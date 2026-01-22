import { useEffect, useState } from 'react'
import { authFetch } from '../api/http'
import { type Farm } from '../types/Farm'

type FarmSession = {
  mode: 'OWN' | 'VISITING'
  farmId: string
  ownerUserId: string
  ownerUsername?: string
}

export function useFarmInternal() {
  const [farm, setFarm] = useState<Farm | null>(null)
  const [session, setSession] = useState<FarmSession>({
    mode: 'OWN',
    farmId: 'my',
    ownerUserId: 'me'
  })
  const [loading, setLoading] = useState(true)
  const isVisiting = session.mode === 'VISITING'
  const canInteract = session.mode === 'OWN'

  async function fetchFarm() {  
    try {
      const endpoint =
        session.mode === 'OWN'
          ? '/farms/my'
          : `/farms/${session.farmId}`

      const data = await authFetch<Farm>(endpoint)
      const isNewFarm = !farm || farm.id !== data.id
      setFarm(data)

      window.dispatchEvent(
        new CustomEvent(
          isNewFarm ? 'farm:change' : 'farm:sync',
          { detail: data }
        )
      )
    } catch (err) {
      console.warn('Sessão expirada, redirecionando...')
      setFarm(null)
    } finally {
      setLoading(false)
    }
  }

  function visitFarm(
    farmId: string,
    ownerUserId: string,
    ownerUsername: string
  ) {
    setFarm(null)
    setLoading(true)
    setSession({
      mode: 'VISITING',
      farmId,
      ownerUserId,
      ownerUsername
    })
  }

  function returnToOwnFarm() {
    setFarm(null)
    setLoading(true)
    setSession({
      mode: 'OWN',
      farmId: 'my',
      ownerUserId: 'me'
    })
  }

  useEffect(() => {
    fetchFarm()
  }, [session.mode, session.farmId])

  useEffect(() => {
    if (!farm) return
    if (session.mode !== 'OWN') return

    const hasGrowing = farm.plots.some(
      p => p.seedId && !p.isReady
    )

    if (!hasGrowing) return

    const interval = setInterval(() => {
      fetchFarm()
    }, 30_000) // a cada 30 segundos

    return () => clearInterval(interval)
  }, [farm?.id, session.mode])

  return { 
    farm, 
    loading, 
    session, 
    isVisiting,
    canInteract, 
    visitFarm,
    returnToOwnFarm,
    refreshFarm: fetchFarm 
  }
}
