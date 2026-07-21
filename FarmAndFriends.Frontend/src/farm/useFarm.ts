import { useCallback, useEffect, useRef, useState } from 'react'
import { authFetch } from '../api/http'
import { type Farm } from '../types/Farm'
import { getNextPlotReadyAt } from '../utils/time'

const FARM_POLL_INTERVAL_MS = 30_000
const READY_SYNC_BUFFER_MS = 150

type FarmSession = {
  mode: 'OWN' | 'VISITING'
  farmId: string
  ownerUserId: string
  ownerUsername?: string
}

export function useFarmInternal() {
  const [farm, setFarm] = useState<Farm | null>(null)
  const activeFarmIdRef = useRef<string | null>(null)
  const [session, setSession] = useState<FarmSession>({
    mode: 'OWN',
    farmId: 'my',
    ownerUserId: 'me'
  })
  const [loading, setLoading] = useState(true)
  const isVisiting = session.mode === 'VISITING'
  const canInteract = session.mode === 'OWN'

  const fetchFarm = useCallback(async () => {
    try {
      const endpoint =
        session.mode === 'OWN'
          ? '/farms/my'
          : `/farms/${session.farmId}`

      const data = await authFetch<Farm>(endpoint)
      const isNewFarm = activeFarmIdRef.current !== data.id
      activeFarmIdRef.current = data.id
      setFarm(data)

      window.dispatchEvent(
        new CustomEvent(
          isNewFarm ? 'farm:change' : 'farm:sync',
          { detail: data }
        )
      )
    } catch {
      console.warn('Sessão expirada, redirecionando...')
      setFarm(null)
    } finally {
      setLoading(false)
    }
  }, [session.farmId, session.mode])

  function visitFarm(
    farmId: string,
    ownerUserId: string,
    ownerUsername: string
  ) {
    activeFarmIdRef.current = null
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
    activeFarmIdRef.current = null
    setFarm(null)
    setLoading(true)
    setSession({
      mode: 'OWN',
      farmId: 'my',
      ownerUserId: 'me'
    })
  }

  useEffect(() => {
    void fetchFarm()
  }, [fetchFarm])

  const nextPlotReadyAt = getNextPlotReadyAt(farm?.plots ?? [])

  useEffect(() => {
    if (nextPlotReadyAt === null) return

    const interval = setInterval(() => {
      void fetchFarm()
    }, FARM_POLL_INTERVAL_MS)

    const readyTimeout = setTimeout(() => {
      void fetchFarm()
    }, Math.max(0, nextPlotReadyAt - Date.now()) + READY_SYNC_BUFFER_MS)

    return () => {
      clearInterval(interval)
      clearTimeout(readyTimeout)
    }
  }, [fetchFarm, nextPlotReadyAt])

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
