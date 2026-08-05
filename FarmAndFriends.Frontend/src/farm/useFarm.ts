import { useCallback, useEffect, useRef, useState } from 'react'
import { authFetch } from '../api/http'
import { type Farm } from '../types/Farm'
import { getNextFarmStateAt } from '../utils/time'
import {
  isCurrentFarmRequest,
  patchFarmPlot,
  type ConfirmedPlotPatch,
  type ConfirmedPlotPatchFactory,
} from './farmState'

const FARM_POLL_INTERVAL_MS = 30_000
const FARM_SYNC_BUFFER_MS = 150

type FarmSession = {
  mode: 'OWN' | 'VISITING'
  farmId: string
  ownerUserId: string
  ownerUsername?: string
}

function getSessionKey(session: FarmSession) {
  return `${session.mode}:${session.farmId}`
}

export function useFarmInternal() {
  const [farm, setFarm] = useState<Farm | null>(null)
  const farmRef = useRef<Farm | null>(null)
  const activeFarmIdRef = useRef<string | null>(null)
  const latestRequestIdRef = useRef(0)
  const initialSession: FarmSession = {
    mode: 'OWN',
    farmId: 'my',
    ownerUserId: 'me'
  }
  const [session, setSession] = useState<FarmSession>(initialSession)
  const activeSessionKeyRef = useRef(getSessionKey(initialSession))
  const [loading, setLoading] = useState(true)
  const isVisiting = session.mode === 'VISITING'
  const canInteract = session.mode === 'OWN'

  const fetchFarm = useCallback(async () => {
    const requestId = ++latestRequestIdRef.current
    const requestSessionKey = `${session.mode}:${session.farmId}`
    const isCurrentRequest = () => isCurrentFarmRequest(
      requestId,
      latestRequestIdRef.current,
      requestSessionKey,
      activeSessionKeyRef.current,
    )

    try {
      const endpoint =
        session.mode === 'OWN'
          ? '/farms/my'
          : `/farms/${session.farmId}`

      const data = await authFetch<Farm>(endpoint)
      if (!isCurrentRequest()) return

      const isNewFarm = activeFarmIdRef.current !== data.id
      activeFarmIdRef.current = data.id
      farmRef.current = data
      setFarm(data)

      window.dispatchEvent(
        new CustomEvent(
          isNewFarm ? 'farm:change' : 'farm:sync',
          { detail: data }
        )
      )
    } catch (error) {
      if (!isCurrentRequest()) return

      // authFetch already owns expired-session handling. Keeping the last
      // authoritative snapshot prevents React and Phaser from diverging when
      // a mutation succeeds but the follow-up synchronization is transiently
      // unavailable; polling will retry the same GET.
      console.warn('Não foi possível sincronizar a fazenda.', error)
    } finally {
      if (isCurrentRequest()) {
        setLoading(false)
      }
    }
  }, [session.farmId, session.mode])

  const patchPlot = useCallback((
    plotId: string,
    patchOrFactory: ConfirmedPlotPatch | ConfirmedPlotPatchFactory,
  ) => {
    const currentFarm = farmRef.current
    if (!currentFarm) return

    // A confirmed mutation is newer than every GET already in flight.
    latestRequestIdRef.current += 1
    const nextFarm = patchFarmPlot(currentFarm, plotId, patchOrFactory)
    if (nextFarm === currentFarm) return

    farmRef.current = nextFarm
    setFarm(nextFarm)
    window.dispatchEvent(
      new CustomEvent('farm:sync', { detail: nextFarm }),
    )
  }, [])

  function visitFarm(
    farmId: string,
    ownerUserId: string,
    ownerUsername: string
  ) {
    latestRequestIdRef.current += 1
    activeSessionKeyRef.current = `VISITING:${farmId}`
    activeFarmIdRef.current = null
    farmRef.current = null
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
    latestRequestIdRef.current += 1
    activeSessionKeyRef.current = 'OWN:my'
    activeFarmIdRef.current = null
    farmRef.current = null
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

  const nextFarmSyncAt = getNextFarmStateAt(
    farm?.plots ?? [],
    isVisiting,
    farm?.nextPestCheckAt ?? null,
  )

  useEffect(() => {
    const interval = setInterval(() => {
      void fetchFarm()
    }, FARM_POLL_INTERVAL_MS)

    const syncTimeout = nextFarmSyncAt === null
      ? null
      : setTimeout(() => {
          void fetchFarm()
        }, Math.max(0, nextFarmSyncAt - Date.now()) + FARM_SYNC_BUFFER_MS)

    return () => {
      clearInterval(interval)
      if (syncTimeout !== null) clearTimeout(syncTimeout)
    }
  }, [fetchFarm, nextFarmSyncAt])

  return { 
    farm, 
    loading, 
    session, 
    isVisiting,
    canInteract, 
    visitFarm,
    returnToOwnFarm,
    patchPlot,
    refreshFarm: fetchFarm
  }
}
