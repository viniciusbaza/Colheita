import { useInventory } from "../inventory/useInventory"
import { useSeeds } from '../seeds/useSeeds'

type Props = {
  onClose: () => void
}

export function InventoryPanel({ onClose }: Props) {
  const { inventory, loading } = useInventory()
  const { getSeedByItem } = useSeeds()

  if (loading || !inventory) return null

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center text-black bg-black/40"
      onClick={onClose}
    >
      <div
        className="bg-amber-100 rounded-lg p-4 w-80"
        onClick={e => e.stopPropagation()}
      >
        <h2 className="font-bold mb-2">🎒 Inventário</h2>

        {/* <p className="mb-2">🪙 {inventory.coins}</p> */}

        <div className="space-y-1">
          {inventory.items.map(item => {
            const catalogItem = getSeedByItem(item.itemType, item.itemId)
            return (
              <div
                key={`${item.itemType}-${item.itemId}`}
                className="flex justify-between bg-white/60 px-2 py-1 rounded"
              >
                <span>
                  {item.itemType === 'Seed' ? '🌱' : '🥕'} {' '}
                  {catalogItem?.name || 'Item Desconhecido'}
                </span>
                <span>x{item.quantity}</span>
              </div>
            )
          })}
        </div>

        <button
          onClick={onClose}
          className="mt-3 w-full bg-red-500 text-white rounded py-1"
        >
          Fechar
        </button>
      </div>
    </div>
  )
}
