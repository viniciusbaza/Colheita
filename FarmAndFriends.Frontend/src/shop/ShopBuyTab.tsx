import { useSeeds } from '../seeds/useSeeds'
import { useShop } from './useShop'
import { useInventory } from '../inventory/useInventory'
import { useState } from 'react'

type Props = {
  onSuccess: (msg: string) => void
  onError: (msg: string) => void
}

export function ShopBuyTab({ onSuccess, onError }: Props) {
  const { seeds } = useSeeds()
  const { buySeed } = useShop()
  const { refreshInventory } = useInventory()

  const [quantities, setQuantities] = useState<Record<string, number>>({})

  function getQuantity(id: string) {
    return quantities[id] ?? 1
  }

  function setQuantity(id: string, value: number) {
    setQuantities(prev => ({
        ...prev,
        [id]: value
    }))
  }

  async function handleBuy(seedId: string, quantity: number) {
    try {
      await buySeed(seedId, quantity)
      
      onSuccess(`🌱 Você comprou ${quantity} semente(s) de ${seeds.find(s => s.id === seedId)?.name ?? 'desconhecida'}.`)
      // RESET SÓ DESSE ITEM
      setQuantities(prev => ({
        ...prev,
         [seedId]: 1
      }))
      refreshInventory()
      window.dispatchEvent(new Event('inventory:changed'))
    } catch (err: any) {
      onError(err.message ?? 'Erro ao comprar')
      setTimeout(() => onError(null as any), 3000)
    }
  }

  return (
    <div className="space-y-2">
      {seeds.map(seed => {
        const quantity = getQuantity(seed.id)

        return (
            <div
            key={seed.id}
            className="flex justify-between items-center bg-white/70 px-2 py-1 rounded"
            >
            <div>
                🌱 {seed.name}
                <div className="text-xs opacity-70">
                {seed.buyPrice} 🪙 | lvl {seed.minLevel}
                </div>
            </div>

            <div className="flex items-center gap-2">
                <input
                type="number"
                min={1}
                value={quantity}
                onChange={e => setQuantity(seed.id, +e.target.value)}
                className="w-14 rounded px-1"
                />

                <button
                onClick={() => handleBuy(seed.id, quantity)}
                className="bg-green-600 text-white rounded px-2"
                >
                Comprar
                </button>
            </div>
            </div>
        )
        })}
    </div>
  )
}
