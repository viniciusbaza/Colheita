import { useInventory } from '../inventory/useInventory'
import { useSeeds } from '../seeds/useSeeds'
import { useShop } from './useShop'
import { useState } from 'react'

type Props = {
  onSuccess: (msg: string) => void
  onError: (msg: string) => void
}

export function ShopSellTab({ onSuccess, onError }: Props) {
  const { inventory, refreshInventory } = useInventory()
  const { getSeedByItem } = useSeeds()
  const { sellCrop } = useShop()
  const [quantities, setQuantities] = useState<Record<string, number>>({})

  if (!inventory) return null

  const crops = inventory.items.filter(i => i.itemType === 'Crop')

  async function handleSell(cropId: string, quantity: number) {
    try {
      await sellCrop(cropId, quantity)

      onSuccess(`🪙 Você vendeu ${quantity} unidade(s) de ${getSeedByItem('Crop', cropId)?.name ?? 'desconhecida'}.`)
      // RESET SÓ DESSE ITEM
      setQuantities(prev => ({
        ...prev,
         [cropId]: 1
      }))
      refreshInventory()
      window.dispatchEvent(new Event('inventory:changed'))
    } catch (err: any) {
      onError(err.message ?? 'Erro ao vender')
      setTimeout(() => onError(null as any), 3000)
    }
  }

  return (
    <div className="space-y-2">
      {crops.map(crop => {
        const seed = getSeedByItem('Crop', crop.itemId)
        const quantity = quantities[crop.itemId] ?? 1

        return (
          <div
            key={crop.itemId}
            className="flex justify-between items-center bg-white/70 px-2 py-1 rounded"
          >
            <div>
              {seed?.icon ?? '🛍️'} {seed?.name ?? 'Crop'}
              <div className="text-xs opacity-70">
                x{crop.quantity}
              </div>
            </div>

            <div className="flex items-center gap-2">
              <input
                type="number"
                min={1}
                max={crop.quantity}
                value={quantity}
                onChange={e =>
                    setQuantities(prev => ({
                        ...prev,
                        [crop.itemId]: +e.target.value
                    }))
                }
                className="w-14 rounded px-1"
              />
              <button
                onClick={() => handleSell(crop.itemId, quantity)}
                className="bg-amber-600 text-white rounded px-2"
              >
                Vender
              </button>
            </div>
          </div>
        )
      })}
    </div>
  )
}
