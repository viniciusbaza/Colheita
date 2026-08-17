import { useCallback, useEffect, useRef, useState } from 'react'
import type {
  LandPaymentCurrency,
  LandPurchaseResponse,
} from '../types/Farm'
import { purchaseLandPlot } from './landApi'
import {
  getLandPurchaseAttempt,
  type LandPurchaseAttempt,
} from './landPurchase'

type Options = {
  attemptStore?: LandPurchaseAttemptStore
  refreshFarm: (response: LandPurchaseResponse) => Promise<void>
  refreshInventory: () => Promise<void>
  onReconciliationStarted?: (response: LandPurchaseResponse) => void
  onReconciliationFailed?: (
    response: LandPurchaseResponse,
    message: string,
  ) => void
  onConfirmed: (response: LandPurchaseResponse) => void
  onPendingChange?: (pending: boolean) => void
}

export type LandPurchaseAttemptStore = {
  current: LandPurchaseAttempt | null
}

type UseLandPurchaseResult = {
  pending: boolean
  error: string | null
  prepareAttempt: (
    plotId: string,
    paymentCurrency: LandPaymentCurrency,
  ) => void
  purchase: (
    plotId: string,
    paymentCurrency: LandPaymentCurrency,
  ) => Promise<LandPurchaseResponse | null>
}

function getErrorMessage(error: unknown) {
  return error instanceof Error
    ? error.message
    : 'Não foi possível comprar este terreno agora.'
}

export function useLandPurchase({
  attemptStore,
  refreshFarm,
  refreshInventory,
  onReconciliationStarted,
  onReconciliationFailed,
  onConfirmed,
  onPendingChange,
}: Options): UseLandPurchaseResult {
  const localAttemptRef = useRef<LandPurchaseAttempt | null>(null)
  const attemptRef = attemptStore ?? localAttemptRef
  const pendingRef = useRef(false)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const setPurchasePending = useCallback((nextPending: boolean) => {
    pendingRef.current = nextPending
    setPending(nextPending)
    onPendingChange?.(nextPending)
  }, [onPendingChange])

  useEffect(() => () => {
    onPendingChange?.(false)
  }, [onPendingChange])

  const prepareAttempt = useCallback((
    plotId: string,
    paymentCurrency: LandPaymentCurrency,
  ) => {
    attemptRef.current = getLandPurchaseAttempt(
      attemptRef.current,
      plotId,
      paymentCurrency,
      () => crypto.randomUUID(),
    )
    setError(null)
  }, [attemptRef])

  const purchase = useCallback(async (
    plotId: string,
    paymentCurrency: LandPaymentCurrency,
  ) => {
    if (pendingRef.current) return null

    const attempt = getLandPurchaseAttempt(
      attemptRef.current,
      plotId,
      paymentCurrency,
      () => crypto.randomUUID(),
    )
    attemptRef.current = attempt
    setError(null)
    setPurchasePending(true)

    try {
      const response = await purchaseLandPlot(
        plotId,
        paymentCurrency,
        attempt.key,
      )

      onReconciliationStarted?.(response)

      try {
        await refreshFarm(response)
      } catch (refreshError) {
        const message = getErrorMessage(refreshError)
        setError(message)
        onReconciliationFailed?.(response, message)
        return null
      }

      if (attemptRef.current?.key === attempt.key) {
        attemptRef.current = null
      }

      onConfirmed(response)
      window.dispatchEvent(new Event('inventory:changed'))
      await Promise.allSettled([refreshInventory()])
      return response
    } catch (purchaseError) {
      setError(getErrorMessage(purchaseError))
      return null
    } finally {
      setPurchasePending(false)
    }
  }, [
    attemptRef,
    onConfirmed,
    onReconciliationFailed,
    onReconciliationStarted,
    refreshFarm,
    refreshInventory,
    setPurchasePending,
  ])

  return {
    pending,
    error,
    prepareAttempt,
    purchase,
  }
}
