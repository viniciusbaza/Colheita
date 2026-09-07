import { useCallback, useEffect, useRef, useState } from 'react'
import { authFetch } from '../api/http'
import { type Farm } from '../types/Farm'
import { getNextFarmStateAt } from '../utils/time'
import {
  getFarmVisitDecision,
  isCurrentFarmRequest,
  patchFarmName,
  patchFarmPlot,
  type ConfirmedPlotPatch,
  type ConfirmedPlotPatchFactory,
} from './farmState'
import {
  getVisitRecoveryFeedback,
  type VisitRecoveryFeedback,
} from './visitRecovery'

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
  const lastOwnFarmRef = useRef<Farm | null>(null)
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
  const [visitRecoveryFeedback, setVisitRecoveryFeedback] =
    useState<VisitRecoveryFeedback | null>(null)
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
      if (session.mode === 'OWN') {
        lastOwnFarmRef.current = data
      }
      setFarm(data)

      window.dispatchEvent(
        new CustomEvent(
          isNewFarm ? 'farm:change' : 'farm:sync',
          { detail: data }
        )
      )
    } catch (error) {
      if (!isCurrentRequest()) return

      const recoveryFeedback = session.mode === 'VISITING'
        ? getVisitRecoveryFeedback(error, session.ownerUsername)
        : null

      if (recoveryFeedback) {
        const lastOwnFarm = lastOwnFarmRef.current

        // Invalidate the denied visit before changing sessions. The OWN effect
        // performs the authoritative refresh; the last OWN snapshot keeps the
        // React/Phaser state coherent while that request is in flight and lets
        // the player see the recovery feedback immediately.
        latestRequestIdRef.current += 1
        activeSessionKeyRef.current = 'OWN:my'
        activeFarmIdRef.current = lastOwnFarm?.id ?? null
        farmRef.current = lastOwnFarm
        setFarm(lastOwnFarm)
        setVisitRecoveryFeedback(recoveryFeedback)
        setLoading(false)
        setSession({
          mode: 'OWN',
          farmId: 'my',
          ownerUserId: 'me',
        })
        return
      }

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
  }, [session.farmId, session.mode, session.ownerUsername])

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

  const updateOwnFarmName = useCallback((farmId: string, name: string) => {
    const nextOwnFarm = patchFarmName(lastOwnFarmRef.current, farmId, name)
    lastOwnFarmRef.current = nextOwnFarm

    // While visiting, only the cached own-farm snapshot changes. The visible
    // friend's farm must never inherit the authenticated player's new name.
    if (activeSessionKeyRef.current !== 'OWN:my') return

    // The confirmed mutation is newer than any own-farm GET already in flight.
    latestRequestIdRef.current += 1
    const nextFarm = patchFarmName(farmRef.current, farmId, name)
    if (nextFarm === farmRef.current) return
    farmRef.current = nextFarm
    setFarm(nextFarm)
    if (nextFarm) {
      window.dispatchEvent(new CustomEvent('farm:sync', { detail: nextFarm }))
    }
  }, [])

  function visitFarm(
    farmId: string,
    ownerUserId: string,
    ownerUsername: string
  ) {
    const decision = getFarmVisitDecision(
      session,
      farmId,
      farmRef.current?.id ?? null,
      loading,
    )

    if (decision === 'ignore') return

    activeSessionKeyRef.current = `VISITING:${farmId}`
    activeFarmIdRef.current = null
    // Keep the OWN snapshot only in lastOwnFarmRef for denied-visit recovery.
    // Rendering it under a VISITING session would misidentify the visible farm
    // if the first friend-farm request fails for a transient reason.
    farmRef.current = null
    setFarm(null)
    setVisitRecoveryFeedback(null)
    setLoading(true)

    if (decision === 'retry') {
      // The session fields remain unchanged during a retry, so its effect will
      // not run again. Fetch explicitly; fetchFarm invalidates older requests.
      void fetchFarm()
      return
    }

    latestRequestIdRef.current += 1
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

  const clearVisitRecoveryFeedback = useCallback(() => {
    setVisitRecoveryFeedback(null)
  }, [])

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
    visitRecoveryFeedback,
    clearVisitRecoveryFeedback,
    patchPlot,
    updateOwnFarmName,
    refreshFarm: fetchFarm
  }
}
