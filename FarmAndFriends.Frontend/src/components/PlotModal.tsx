import { useCallback, useEffect, useRef, useState } from 'react'
import type { KeyboardEvent as ReactKeyboardEvent } from 'react'
import { authFetch } from '../api/http'
import { hasFarmExpansion, getLandPurchaseSuccessMessage } from '../land/landPurchase'
import type { LandPurchaseAttemptStore } from '../land/useLandPurchase'
import { useFarm } from '../farm/useFarmContext'
import {
  confirmedPestRemovalPatch,
  confirmedTheftPatch,
  getPestRemovalAttempt,
} from '../farm/farmState'
import type { PestRemovalAttempt } from '../farm/farmState'
import { useInventory } from '../inventory/useInventory'
import { useSeeds } from '../seeds/useSeeds'
import { useShopItems } from '../shop/useShopItems'
import type {
  ApplyPestProtectionResponse,
  CareResponse,
  HarvestResponse,
  LandPurchaseResponse,
  PestActionResponse,
  PlantResponse,
  PlotPestRemoveDone,
  StealResponse,
} from '../types/Farm'
import { useUser } from '../user/useUser'
import { hasServerPestProtection } from '../utils/pests'
import { formatCountdown, formatTimeRemaining } from '../utils/time'
import { LandPurchasePanel } from './LandPurchasePanel'

const NATURAL_REPELLENT_ID = 'natural_repellent'

type Props = {
  plotId: string
  onClose: () => void
  landPurchaseAttemptStore: LandPurchaseAttemptStore
  onBusyChange?: (busy: boolean) => void
  onLandPurchaseFeedback?: (feedback: LandPurchaseFeedback) => void
}

export type LandPurchaseFeedback = {
  type: 'pending' | 'success' | 'error'
  message: string
}

type PendingAction =
  | 'plant'
  | 'harvest'
  | 'steal'
  | 'care'
  | 'remove-pest'
  | 'protect'

type Feedback = {
  type: 'success' | 'error'
  message: string
}

function errorMessage(error: unknown, fallback: string) {
  return error instanceof Error ? error.message : fallback
}

export function PlotModal({
  plotId,
  onClose,
  landPurchaseAttemptStore,
  onBusyChange,
  onLandPurchaseFeedback,
}: Props) {
  const {
    farm,
    refreshFarm,
    patchPlot,
    isVisiting,
    canInteract,
  } = useFarm()
  const { getSeed } = useSeeds()
  const { inventory, refreshInventory } = useInventory()
  const { getItem } = useShopItems()
  const { user, addXp, refreshUser } = useUser()
  const dialogRef = useRef<HTMLDivElement>(null)
  const closeButtonRef = useRef<HTMLButtonElement>(null)
  const pestRemovalAttemptRef = useRef<PestRemovalAttempt | null>(null)
  const actionBusyRef = useRef(false)

  const [pendingAction, setPendingAction] = useState<PendingAction | null>(null)
  const [feedback, setFeedback] = useState<Feedback | null>(null)
  const [confirmProtection, setConfirmProtection] = useState(false)
  const [landPurchasePending, setLandPurchasePending] = useState(false)
  const [currentTime, setCurrentTime] = useState(Date.now)
  const handleLandPurchasePending = useCallback((pending: boolean) => {
    setLandPurchasePending(pending)
    onBusyChange?.(pending)
  }, [onBusyChange])

  const plot = farm?.plots.find(candidate => candidate.id === plotId)
  const farmId = farm?.id
  const landOffer = farm?.landOffer ?? null
  const care = plot?.care
  const nextCareAt = care?.nextCareAt
  const careCycleEndsAt = care?.careCycleEndsAt
  const canCareNow = Boolean(care?.canCare)
  const careOpportunityId = canCareNow
    ? care?.opportunityId ?? null
    : null
  const seedCatalog = getSeed(plot?.seedId ?? undefined)
  const repellent = getItem(NATURAL_REPELLENT_ID)
  const repellentQuantity = inventory?.items.find(
    item => item.itemType === 'Item' && item.itemId === NATURAL_REPELLENT_ID,
  )?.quantity ?? 0

  const readyAt = plot?.readyAt
  const protectedUntil = plot?.protectedUntil
  const isProtected = hasServerPestProtection(protectedUntil ?? null)
  const pestConsumesAt = plot?.pest?.status === 'active'
    ? plot.pest.consumesAt
    : null
  const timeLeft = readyAt
    ? formatTimeRemaining(readyAt, currentTime)
    : null
  const pestTimeLeft = pestConsumesAt
    ? formatCountdown(pestConsumesAt, currentTime)
    : null
  const protectionTimeLeft = isProtected && protectedUntil
    ? formatCountdown(protectedUntil, currentTime)
    : null
  const nextCareAtTime = nextCareAt
    ? new Date(nextCareAt).getTime()
    : Number.NaN
  const careTimeLeft = Number.isFinite(nextCareAtTime)
    && nextCareAtTime > currentTime
    ? formatTimeRemaining(nextCareAt!, currentTime)
    : null
  const careCycleEndsAtTime = careCycleEndsAt
    ? new Date(careCycleEndsAt).getTime()
    : Number.NaN
  const careCycleTimeLeft = Number.isFinite(careCycleEndsAtTime)
    && careCycleEndsAtTime > currentTime
    ? formatTimeRemaining(careCycleEndsAt!, currentTime)
    : null
  const seeds = canInteract
    ? inventory?.items.filter(
        item => item.itemType === 'Seed' && item.quantity > 0,
      ) ?? []
    : []
  const canOfferProtection = Boolean(
    plot?.unlocked
      && !isProtected
      && repellent !== undefined
      && repellentQuantity > 0,
  )
  const actionBusy = pendingAction !== null || landPurchasePending
  actionBusyRef.current = actionBusy

  useEffect(() => {
    if (
      !readyAt
      && !nextCareAt
      && !careCycleEndsAt
      && !pestConsumesAt
      && !protectedUntil
    ) {
      return
    }

    const interval = setInterval(() => setCurrentTime(Date.now()), 1000)
    return () => clearInterval(interval)
  }, [
    careCycleEndsAt,
    nextCareAt,
    pestConsumesAt,
    protectedUntil,
    readyAt,
  ])

  useEffect(() => {
    const previouslyFocused = document.activeElement
    closeButtonRef.current?.focus()

    function closeOnEscape(event: KeyboardEvent) {
      if (event.key === 'Escape' && !actionBusyRef.current) onClose()
    }

    window.addEventListener('keydown', closeOnEscape)
    return () => {
      window.removeEventListener('keydown', closeOnEscape)
      if (previouslyFocused instanceof HTMLElement) {
        previouslyFocused.focus()
      }
    }
  }, [onClose])

  if (!plot) return null

  function keepFocusInside(event: ReactKeyboardEvent<HTMLDivElement>) {
    if (event.key !== 'Tab') return

    const focusable = dialogRef.current?.querySelectorAll<HTMLElement>(
      'button:not(:disabled), input:not(:disabled), [tabindex]:not([tabindex="-1"])',
    )
    if (!focusable || focusable.length === 0) return

    const first = focusable[0]
    const last = focusable[focusable.length - 1]
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault()
      last.focus()
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault()
      first.focus()
    }
  }

  async function handlePlant(seedId: string) {
    if (plot?.unlocked !== true || isVisiting || pendingAction) return

    setFeedback(null)
    setPendingAction('plant')

    try {
      const data = await authFetch<PlantResponse>(`/plots/${plotId}/plant`, {
        method: 'POST',
        body: JSON.stringify({ seedId }),
      })

      window.dispatchEvent(
        new CustomEvent('plot:plant:done', {
          detail: { plotId, xpGained: data.xpGained },
        }),
      )
      addXp(data.xpGained)
      await Promise.all([refreshFarm(), refreshInventory()])
      onClose()
    } catch (error) {
      setFeedback({
        type: 'error',
        message: errorMessage(error, 'Não foi possível plantar agora.'),
      })
    } finally {
      setPendingAction(null)
    }
  }

  async function handleHarvest() {
    if (plot?.unlocked !== true || isVisiting || pendingAction) return

    setFeedback(null)
    setPendingAction('harvest')

    try {
      const data = await authFetch<HarvestResponse>(
        `/plots/${plotId}/harvest`,
        { method: 'POST' },
      )

      window.dispatchEvent(
        new CustomEvent('plot:harvest:done', {
          detail: { plotId, xpGained: data.xpGained },
        }),
      )
      window.dispatchEvent(new Event('inventory:changed'))
      addXp(data.xpGained)
      await Promise.all([refreshFarm(), refreshInventory()])
      onClose()
    } catch (error) {
      setFeedback({
        type: 'error',
        message: errorMessage(error, 'Não foi possível colher agora.'),
      })
      await refreshFarm()
    } finally {
      setPendingAction(null)
    }
  }

  async function handleSteal() {
    if (
      plot?.unlocked !== true
      || !isVisiting
      || !farmId
      || pendingAction
    ) {
      return
    }

    setFeedback(null)
    setPendingAction('steal')

    try {
      const data = await authFetch<StealResponse>(
        `/farms/${farmId}/plots/${plotId}/steal`,
        { method: 'POST' },
      )

      window.dispatchEvent(
        new CustomEvent('plot:steal:done', {
          detail: {
            plotId,
            stolen: data.stolen,
            remainingYield: data.ownerWillReceive,
            xpGained: data.xpGained,
            pestCancelled: data.pestCancelled,
          },
        }),
      )
      patchPlot(
        plotId,
        currentPlot => confirmedTheftPatch(currentPlot, data),
      )
      addXp(data.xpGained)
      await refreshFarm()
      onClose()
    } catch (error) {
      const message = errorMessage(error, 'Erro ao roubar')
      setFeedback({ type: 'error', message })
      window.dispatchEvent(
        new CustomEvent('plot:steal:failed', {
          detail: { plotId, reason: message },
        }),
      )
      await refreshFarm()
    } finally {
      setPendingAction(null)
    }
  }

  async function handleCare() {
    if (
      !isVisiting
      || plot?.unlocked !== true
      || !farmId
      || !careOpportunityId
      || pendingAction
    ) {
      return
    }

    setFeedback(null)
    setPendingAction('care')

    try {
      const data = await authFetch<CareResponse>(
        `/farms/${farmId}/plots/${plotId}/care`,
        {
          method: 'POST',
          headers: { 'Idempotency-Key': crypto.randomUUID() },
          body: JSON.stringify({ careOpportunityId }),
        },
      )

      window.dispatchEvent(
        new CustomEvent('plot:care:done', {
          detail: {
            plotId,
            coinsGained: data.coinsGained,
            xpGained: data.xpGained,
            caredByUsername: data.caredByUsername,
          },
        }),
      )
      window.dispatchEvent(new Event('inventory:changed'))
      addXp(data.xpGained)
      await refreshFarm()
      onClose()
    } catch (error) {
      setFeedback({
        type: 'error',
        message: errorMessage(error, 'Não foi possível deixar o cuidado.'),
      })
      await refreshFarm()
    } finally {
      setPendingAction(null)
    }
  }

  async function handleRemovePest() {
    const pest = plot?.pest
    if (plot?.unlocked !== true || !farmId || pendingAction || !pest?.canRemove) {
      return
    }

    setFeedback(null)
    setPendingAction('remove-pest')
    const pestOccurrenceId = pest.occurrenceId

    if (pestOccurrenceId === null) {
      setFeedback({
        type: 'success',
        message: '🐛 Só um instante: estamos sincronizando esta lagarta.',
      })
      await refreshFarm()
      setPendingAction(null)
      return
    }

    const attempt = getPestRemovalAttempt(
      pestRemovalAttemptRef.current,
      pestOccurrenceId,
      () => crypto.randomUUID(),
    )
    pestRemovalAttemptRef.current = attempt

    try {
      const data = await authFetch<PestActionResponse>(
        `/farms/${farmId}/plots/${plotId}/pest/remove`,
        {
          method: 'POST',
          headers: { 'Idempotency-Key': attempt.key },
          body: JSON.stringify({ pestOccurrenceId }),
        },
      )

      if (pestRemovalAttemptRef.current?.key === attempt.key) {
        pestRemovalAttemptRef.current = null
      }
      patchPlot(
        plotId,
        currentPlot => confirmedPestRemovalPatch(currentPlot, data),
      )
      if (!data.replayed) {
        addXp(data.xpGained)
      }

      window.dispatchEvent(new Event('inventory:changed'))
      await Promise.allSettled([
        refreshFarm(),
        refreshInventory(),
        refreshUser(),
      ])

      if (data.replayed) {
        setFeedback({
          type: 'success',
          message: '🐛 Remoção já confirmada. Resultado recuperado.',
        })
        return
      }

      const removeDone: PlotPestRemoveDone = {
        plotId,
        pestOccurrenceId: data.pestOccurrenceId,
        coinsGained: data.coinsGained,
        xpGained: data.xpGained,
      }
      onClose()
      window.requestAnimationFrame(() => {
        window.dispatchEvent(
          new CustomEvent<PlotPestRemoveDone>('plot:pest:remove:done', {
            detail: removeDone,
          }),
        )
      })
    } catch (error) {
      setFeedback({
        type: 'error',
        message: errorMessage(error, 'Não foi possível remover a lagarta.'),
      })
      window.dispatchEvent(new Event('inventory:changed'))
      await Promise.allSettled([
        refreshFarm(),
        refreshInventory(),
        refreshUser(),
      ])
    } finally {
      setPendingAction(null)
    }
  }

  async function handleApplyProtection() {
    if (!farmId || pendingAction || !canOfferProtection) return

    setFeedback(null)
    setPendingAction('protect')

    try {
      const data = await authFetch<ApplyPestProtectionResponse>(
        `/farms/${farmId}/plots/${plotId}/pest/protection`,
        { method: 'POST' },
      )
      patchPlot(plotId, {
        pest: data.pest,
        protectedUntil: data.protectedUntil,
        remainingYield: data.remainingYield,
      })
      await Promise.all([refreshFarm(), refreshInventory()])
      window.dispatchEvent(new Event('inventory:changed'))
      setConfirmProtection(false)
      setFeedback({
        type: 'success',
        message: data.pest?.status === 'cancelledByProtection'
          ? `🛡️ ${repellent?.name ?? 'Repelente Natural'} aplicado: a lagarta foi espantada.`
          : `🛡️ ${repellent?.name ?? 'Repelente Natural'} aplicado ao lote.`,
      })
      setCurrentTime(Date.now())
    } catch (error) {
      setFeedback({
        type: 'error',
        message: errorMessage(error, 'Não foi possível proteger este lote.'),
      })
      await Promise.all([refreshFarm(), refreshInventory()])
    } finally {
      setPendingAction(null)
    }
  }

  function handleLandPurchaseConfirmed(response: LandPurchaseResponse) {
    const message = getLandPurchaseSuccessMessage(response)

    if (hasFarmExpansion(response)) {
      onLandPurchaseFeedback?.({ type: 'success', message })
      return
    }

    setFeedback({ type: 'success', message })
  }

  function handleLandPurchaseReconciliationStarted(
    response: LandPurchaseResponse,
  ) {
    if (!hasFarmExpansion(response)) return

    onLandPurchaseFeedback?.({
      type: 'pending',
      message: `Atualizando a fazenda com o lote ${response.plotNumber}...`,
    })
    onClose()
  }

  function handleLandPurchaseReconciliationFailed(
    response: LandPurchaseResponse,
    message: string,
  ) {
    if (!hasFarmExpansion(response)) return
    onLandPurchaseFeedback?.({ type: 'error', message })
  }

  async function reconcileLandPurchase(
    response: LandPurchaseResponse,
  ) {
    let reconciled = false

    function onFarmSynchronized(event: Event) {
      if (!(event instanceof CustomEvent)) return

      const detail: unknown = event.detail
      if (!detail || typeof detail !== 'object' || !('plots' in detail)) return
      if (!Array.isArray(detail.plots)) return

      reconciled = detail.plots.some((candidate: unknown) => (
        candidate !== null
        && typeof candidate === 'object'
        && 'id' in candidate
        && candidate.id === response.plotId
        && 'unlocked' in candidate
        && candidate.unlocked === true
      ))
    }

    window.addEventListener('farm:sync', onFarmSynchronized)
    window.addEventListener('farm:change', onFarmSynchronized)

    try {
      await refreshFarm()
      if (!reconciled) {
        throw new Error(
          'A compra foi recebida, mas n\u00e3o foi poss\u00edvel atualizar a fazenda. Tente novamente pela placa.',
        )
      }
    } finally {
      window.removeEventListener('farm:sync', onFarmSynchronized)
      window.removeEventListener('farm:change', onFarmSynchronized)
    }
  }

  const activePest = plot.pest?.status === 'active'

  return (
    <div
      ref={dialogRef}
      role="dialog"
      aria-modal="true"
      aria-labelledby="plot-modal-title"
      aria-busy={actionBusy}
      className="plot-modal w-72 max-w-[calc(100vw-2rem)]"
      onPointerDown={event => event.stopPropagation()}
      onKeyDown={keepFocusInside}
    >
      <div className="mb-2 flex items-start justify-between gap-3">
        <h2 id="plot-modal-title" className="font-bold text-emerald-900">
          Detalhes do lote ({plot.x + 1},{plot.y + 1})
        </h2>
        <button
          ref={closeButtonRef}
          type="button"
          onClick={() => {
            if (!actionBusy) onClose()
          }}
          disabled={actionBusy}
          className="-mr-1 -mt-1 rounded-full px-2 py-1 text-lg leading-none text-slate-600 hover:bg-slate-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-emerald-700"
          aria-label="Fechar detalhes do lote"
        >
          ×
        </button>
      </div>

      {!plot.unlocked && (
        <p className="text-sm text-red-600">🔒 Terreno bloqueado</p>
      )}

      {!isVisiting && landOffer?.plotId === plotId && (
        <LandPurchasePanel
          key={landOffer.plotId}
          offer={landOffer}
          coins={inventory?.coins ?? null}
          premiumCoins={inventory?.premiumCoins ?? null}
          playerLevel={user?.level ?? null}
          attemptStore={landPurchaseAttemptStore}
          refreshFarm={reconcileLandPurchase}
          refreshInventory={refreshInventory}
          onReconciliationStarted={handleLandPurchaseReconciliationStarted}
          onReconciliationFailed={handleLandPurchaseReconciliationFailed}
          onConfirmed={handleLandPurchaseConfirmed}
          onPendingChange={handleLandPurchasePending}
        />
      )}

      {plot.unlocked && isProtected && protectionTimeLeft && (
        <div className="mb-2 rounded-lg border border-sky-200 bg-sky-50 px-3 py-2 text-sm font-semibold text-sky-800">
          🛡️ Protegido contra pragas por {protectionTimeLeft}
        </div>
      )}

      {canInteract && plot.unlocked && !plot.seedId && (
        <>
          <p className="mb-2 font-semibold">🌱 Plantar:</p>
          {seeds.length === 0 && (
            <p className="text-sm text-gray-500">Você não tem sementes.</p>
          )}
          <div className="flex flex-wrap gap-2">
            {seeds.map(seed => (
              <button
                key={seed.itemId}
                type="button"
                className="rounded-lg border bg-green-100 px-3 py-1 text-sm hover:bg-green-200 disabled:cursor-wait disabled:opacity-60"
                onClick={() => void handlePlant(seed.itemId)}
                disabled={actionBusy}
              >
                🌱 {getSeed(seed.itemId)?.name} ×{seed.quantity}
              </button>
            ))}
          </div>
        </>
      )}

      {isVisiting && plot.unlocked && !plot.seedId && !canOfferProtection && (
        <p className="text-sm text-gray-500">Este lote está vazio.</p>
      )}

      {plot.unlocked && plot.seedId && seedCatalog && (
        <>
          <p className="mb-2 text-xs">{seedCatalog.icon} {seedCatalog.name}</p>
          {activePest && (
            <section
              aria-label="Lagarta ativa"
              className="mb-2 rounded-lg border border-amber-300 bg-amber-50 px-3 py-2"
            >
              <p className="font-bold text-amber-900">🐛 Lagarta encontrada!</p>
              <p className="mt-1 text-sm text-amber-800">
                Ela comerá a produção
                {pestTimeLeft ? ` em ${pestTimeLeft}` : ''}.
              </p>
              {plot.pest?.canRemove && (
                <button
                  type="button"
                  className="mt-2 w-full rounded-lg border border-amber-400 bg-white px-3 py-2 text-sm font-semibold text-amber-900 hover:bg-amber-100 disabled:cursor-wait disabled:opacity-60"
                  onClick={() => void handleRemovePest()}
                  disabled={actionBusy}
                >
                  {pendingAction === 'remove-pest'
                    ? 'Removendo...'
                    : 'Remover lagarta'}
                </button>
              )}
            </section>
          )}

          {plot.pest?.status === 'consumed' && (
            <p className="mb-2 rounded-lg border border-orange-200 bg-orange-50 px-3 py-2 text-sm text-orange-800">
              Uma lagarta comeu {plot.pest.consumedAmount ?? 1} {' '}
              {(seedCatalog?.name ?? 'sua cultura').toLocaleLowerCase('pt-BR')}.
            </p>
          )}

          {!isVisiting && (plot.care?.caregiverCount ?? 0) > 0 && (
            <p className="mt-1 text-xs font-semibold text-sky-700">
              💧 Regada por{' '}
              {plot.care!.caregivers
                .map(caregiver => caregiver.username)
                .join(', ')}
              {plot.care!.caregiverCount > plot.care!.caregivers.length
                ? ` e +${plot.care!.caregiverCount
                    - plot.care!.caregivers.length}`
                : ''}
            </p>
          )}
          {isVisiting
            && plot.care?.viewerCared
            && !canCareNow
            && careTimeLeft && (
              <p className="mt-1 text-xs font-semibold text-sky-700">
                💧 Regue novamente em {careTimeLeft}.
              </p>
            )}
          {isVisiting
            && canCareNow
            && plot.care?.rewardAvailable === false
            && careCycleTimeLeft && (
              <p className="mt-1 max-w-52 text-[11px] leading-snug text-amber-700">
                💧 Você regou recentemente. Regue novamente em {careCycleTimeLeft}.
              </p>
            )}

          <div className="plot-actions">
            {plot.isReady ? (
              isVisiting ? (
                <button
                  type="button"
                  className="plot-action steal"
                  onClick={() => void handleSteal()}
                  disabled={actionBusy}
                >
                  {pendingAction === 'steal' ? 'Roubando...' : '😈 Roubar'}
                </button>
              ) : (
                <button
                  type="button"
                  className="plot-action harvest"
                  onClick={() => void handleHarvest()}
                  disabled={actionBusy}
                >
                  {pendingAction === 'harvest' ? 'Colhendo...' : '✂️ Colher'}
                </button>
              )
            ) : (
              <>
                {isVisiting
                  && canCareNow
                  && plot.care?.rewardAvailable === true && (
                    <button
                      type="button"
                      className="plot-action care"
                      onClick={() => void handleCare()}
                      disabled={actionBusy}
                    >
                      {pendingAction === 'care' ? '💧 Regando...' : '💧 Regar'}
                    </button>
                  )}
                <div className="plot-action disabled">
                  ⏳ Pronto em {timeLeft}
                </div>
              </>
            )}
          </div>
        </>
      )}

      {canOfferProtection && (
        <section className="mt-3 border-t border-slate-200 pt-3">
          {!confirmProtection ? (
            <button
              type="button"
              className="w-full rounded-lg border border-sky-300 bg-sky-50 px-3 py-2 text-sm font-semibold text-sky-800 hover:bg-sky-100 disabled:opacity-60"
              onClick={() => setConfirmProtection(true)}
              disabled={actionBusy}
            >
              🛡️ Aplicar {repellent?.name ?? 'Repelente Natural'} ({repellentQuantity})
            </button>
          ) : (
            <div className="rounded-lg border border-sky-300 bg-sky-50 p-2">
              <p className="text-sm text-sky-900">
                Usar 1 {repellent?.name ?? 'Repelente Natural'} neste lote?
              </p>
              {isVisiting && (
                <p className="mt-1 text-xs text-sky-700">
                  O item sairá do seu inventário e protegerá a fazenda visitada.
                </p>
              )}
              <div className="mt-2 flex gap-2">
                <button
                  type="button"
                  className="flex-1 rounded-md bg-sky-700 px-2 py-1.5 text-sm font-semibold text-white hover:bg-sky-800 disabled:cursor-wait disabled:opacity-60"
                  onClick={() => void handleApplyProtection()}
                  disabled={actionBusy}
                >
                  {pendingAction === 'protect' ? 'Aplicando...' : 'Confirmar'}
                </button>
                <button
                  type="button"
                  className="rounded-md border border-sky-300 bg-white px-2 py-1.5 text-sm text-sky-800 hover:bg-sky-100 disabled:opacity-60"
                  onClick={() => setConfirmProtection(false)}
                  disabled={actionBusy}
                >
                  Cancelar
                </button>
              </div>
            </div>
          )}
        </section>
      )}

      {feedback && (
        <div
          role={feedback.type === 'error' ? 'alert' : 'status'}
          aria-live="polite"
          className={`mt-2 rounded-lg border px-3 py-2 text-sm ${
            feedback.type === 'error'
              ? 'border-red-300 bg-red-100 text-red-700'
              : 'border-emerald-300 bg-emerald-50 text-emerald-800'
          }`}
        >
          {feedback.message}
        </div>
      )}
    </div>
  )
}
