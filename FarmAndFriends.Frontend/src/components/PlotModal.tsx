import { useFarm } from '../farm/FarmContext'
import type { HarvestResponse, PlantResponse, StealResponse } from '../types/Farm'
import { authFetch } from '../api/http'
import { useInventory } from '../inventory/useInventory'
import { useEffect, useState } from 'react'
import { formatTimeRemaining } from '../utils/time'
import { useSeeds } from '../seeds/useSeeds'
import { useUser } from '../user/useUser'

type Props = {
  plotId: string
  onClose: () => void
}

export function PlotModal({ plotId, onClose }: Props) {
  const { farm, refreshFarm, isVisiting, canInteract } = useFarm()
  const { getSeed } = useSeeds()
  const { inventory, refreshInventory } = useInventory()
  const { addXp } = useUser()

  const [stealError, setStealError] = useState<string | null>(null)
  const [currentTime, setCurrentTime] = useState(Date.now)

  const seeds = canInteract 
  ? inventory?.items.filter(i => i.itemType === 'Seed' && i.quantity > 0) ?? []
  : []

  const plot = farm?.plots.find(p => p.id === plotId)
  const farmId = farm?.id

  const seedCatalog = getSeed(plot?.seedId ?? undefined)
  const readyAt = plot?.readyAt
  const timeLeft = readyAt
    ? formatTimeRemaining(readyAt, currentTime)
    : null

  useEffect(() => {
    if (!readyAt) return

    const interval = setInterval(() => setCurrentTime(Date.now()), 1000)

    return () => clearInterval(interval)
  }, [readyAt])

  if (!plot) return null

  async function handlePlant(seedId: string) {
    if (isVisiting) return

    try {
      const data = await authFetch<PlantResponse>(
        `/plots/${plotId}/plant`,
        {
          method: 'POST',
          body: JSON.stringify({ seedId }),
        }
      )

      window.dispatchEvent(
        new CustomEvent('plot:plant:done', {
          detail: {
            plotId,
            xpGained: data.xpGained,
          },
        }),
      )

      addXp(data.xpGained)

      onClose()

      // 🔄 sincroniza tudo
      await Promise.all([
        refreshFarm(),
        refreshInventory(),
      ])
    } catch (err) {
      alert((err as Error).message)
    }
  }

  async function handleHarvest() {
    if (isVisiting) return

    try {
      const data = await authFetch<HarvestResponse>(
        `/plots/${plotId}/harvest`,
        { method: 'POST' }
      )

      // Avisa o Phaser COM DADOS PRONTOS
      window.dispatchEvent(
        new CustomEvent('plot:harvest:done', {
          detail: {
            plotId: plotId,
            xpGained: data.xpGained
          }
        })
      )
      addXp(data.xpGained)

      // Fecha o modal
      onClose()

      await refreshFarm()
    } catch (err) {
      alert((err as Error).message)
    }
  }

  async function handleSteal() {
    if (!isVisiting || !farmId) return

    setStealError(null)

    try {
      const data = await authFetch<StealResponse>(
        `/farms/${farmId}/plots/${plotId}/steal`,
        { method: 'POST' }
      )

      window.dispatchEvent(
        new CustomEvent('plot:steal:done', {
          detail: {
            plotId,
            stolen: data.stolen,
            remainingYield: data.ownerWillReceive,
            xpGained: data.xpGained
          }
        })
      )

      addXp(data.xpGained)

      onClose()
      await refreshFarm()
    } catch (err) {
      const message =
        err instanceof Error
          ? err.message
          : 'Erro ao roubar'

      setStealError(message)

      window.dispatchEvent(
        new CustomEvent('plot:steal:failed', {
          detail: {
            plotId,
            reason: message
          }
        })
      )
    }
  }
  return (
    <div className="plot-modal" onPointerDown={(e) => e.stopPropagation()}>
      
      {!plot.unlocked && <p className="text-red-600 text-sm">🔒 Terreno bloqueado</p>}

      {canInteract && plot.unlocked && !plot.seedId && (
        <>
          <p className='font-semibold mb-2'>🌱 Plantar:</p>
          {seeds.length === 0 && (
            <p className="text-gray-500 text-sm">
              Você não tem sementes.
            </p>
          )}
          <div className="flex gap-2 flex-wrap">
            {seeds?.map(seed => (
              <button
                key={seed.itemId}
                className="px-3 py-1 rounded-lg
                bg-green-100 hover:bg-green-200
                border text-sm"
                onClick={() => handlePlant(seed.itemId)}
              >
                🌱 {getSeed(seed.itemId)?.name} x{seed.quantity}
              </button>
            ))}
          </div>  
        </>
      )}

      {isVisiting && plot.unlocked && !plot.seedId && (
        <p className="text-sm text-gray-500">
          ❌ Nada pra fazer aqui.
        </p>
      )}

      {plot.seedId && seedCatalog && (
        <>
          <p className="text-xs">{seedCatalog.icon} {seedCatalog.name}</p>
          <div className="plot-actions">
            {plot.isReady ? (
              isVisiting ? (
                <div
                  className="plot-action steal"
                  onClick={handleSteal}
                >😈 Roubar
                </div>
              ) : (
                <div 
                  className='plot-action harvest'
                  onClick={handleHarvest}
                >✂️ Colher
                </div>
              )
            ) : (
              <div className="plot-action disabled">
                ⏳ {`Pronto em ${timeLeft}`}
              </div>
            )}
          </div>
        </>
      )}

      {stealError && (
        <div className="
          mt-2 px-3 py-2 rounded-lg
          bg-red-100 border border-red-300
          text-sm text-red-700
        ">
          🚫 {stealError}
        </div>
      )}
    </div>
  )
}
