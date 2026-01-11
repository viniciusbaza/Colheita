import { useFarm } from '../farm/useFarm'
import type { HarvestResponse } from '../types/Farm'
import { authFetch } from '../api/http'
import { useInventory } from '../inventory/useInventory'
import { useEffect, useState } from 'react'
import { formatTimeRemaining } from '../utils/time'

type Props = {
  plotId: String
  onClose: () => void
}

export function PlotModal({ plotId, onClose }: Props) {
  const { farm, refreshFarm } = useFarm()
  const { inventory, refreshInventory } = useInventory()

  const [timeLeft, setTimeLeft] = useState<string | null>(null)

  const seeds = inventory?.items.filter(
    i => i.itemType === 'Seed' && i.quantity > 0
  ) ?? []

  const plot = farm?.plots.find(p => p.id === plotId)

  useEffect(() => {
    const readyAt = plot?.readyAt
    if (!readyAt) {
      setTimeLeft(null)
      return
    } 

    function updateTime() {
      setTimeLeft(formatTimeRemaining(readyAt as string))
    }

    updateTime()
    const interval = setInterval(updateTime, 1000)

    return () => clearInterval(interval)
  }, [plot?.readyAt])

  if (!plot) return null

  async function handlePlant(seedId: string) {
    try {
      await authFetch(
        `/plots/${plotId}/plant`,
        {
          method: 'POST',
          body: JSON.stringify({ seedId }),
        }
      )

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
    try {
      const data = await authFetch<HarvestResponse>(
        `/plots/${plotId}/harvest`,
        { method: 'POST' }
      )

      // avisa o Phaser COM DADOS PRONTOS
      window.dispatchEvent(
        new CustomEvent('plot:harvest:done', {
          detail: {
            plotId: plotId,
            xpGained: data.xpGained
          }
        })
      )

      // Fecha o modal
      onClose()

      await refreshFarm()
    } catch (err) {
      alert((err as Error).message)
    }
  }
  return (
    <div className="plot-modal">
      <h2 className="text-xs font-bold mb-1">🌱 Plot ({plot.x}, {plot.y})</h2>
      <p className="text-xs">ID: {plot.id}</p>
      {!plot.unlocked && <p className="text-red-600 text-sm">🔒 Plot bloqueado</p>}

      {plot.unlocked && !plot.seedId && (
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
                🌱 {seed.itemId} x{seed.quantity}
              </button>
            ))}
          </div>  
        </>
      )}

      {plot.seedId && (
        <>
          <p className="text-xs">🌾 {plot.seedId}</p>
          <div className="plot-actions">
            {plot.isReady ? (
              <div 
                className='plot-action harvest'
                onClick={handleHarvest}>✂️ Colher</div>
            ) : (
              <div className="plot-action disabled">⏳ {timeLeft && `Pronto em ${timeLeft}`}</div>
            )}
          </div>
        </>
      )}
      <div className="plot-action mt-2" onClick={onClose}>Fechar</div>
    </div>
  )
}