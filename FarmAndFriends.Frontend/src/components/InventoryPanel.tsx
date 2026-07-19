import { useEffect, useState } from 'react'
import { useInventory } from '../inventory/useInventory'
import { useSeeds } from '../seeds/useSeeds'
import type { InventoryItem } from '../types/Inventory'

type Props = {
  onClose: () => void
}

type InventoryFilter = 'all' | InventoryItem['itemType']

const numberFormatter = new Intl.NumberFormat('pt-BR')

function filterButtonClass(active: boolean) {
  return `relative flex-1 rounded-lg px-2 py-2 text-sm font-semibold transition ${
    active
      ? 'bg-emerald-600 text-white shadow-sm'
      : 'text-emerald-700 hover:bg-emerald-100'
  }`
}

export function InventoryPanel({ onClose }: Props) {
  const { inventory, loading, refreshInventory } = useInventory()
  const { getSeedByItem } = useSeeds()
  const [filter, setFilter] = useState<InventoryFilter>('all')

  useEffect(() => {
    function closeOnEscape(event: KeyboardEvent) {
      if (event.key === 'Escape') onClose()
    }

    window.addEventListener('keydown', closeOnEscape)
    return () => window.removeEventListener('keydown', closeOnEscape)
  }, [onClose])

  const items = (inventory?.items ?? []).filter(item => item.quantity > 0)
  const seedUnits = items
    .filter(item => item.itemType === 'Seed')
    .reduce((total, item) => total + item.quantity, 0)
  const cropUnits = items
    .filter(item => item.itemType === 'Crop')
    .reduce((total, item) => total + item.quantity, 0)
  const otherUnits = items
    .filter(item => item.itemType === 'Item')
    .reduce((total, item) => total + item.quantity, 0)
  const visibleItems = items
    .filter(item => filter === 'all' || item.itemType === filter)
    .sort((left, right) => {
      const leftName = left.itemType === 'Item'
        ? left.itemId
        : (getSeedByItem(left.itemType, left.itemId)?.name ?? left.itemId)
      const rightName = right.itemType === 'Item'
        ? right.itemId
        : (getSeedByItem(right.itemType, right.itemId)?.name ?? right.itemId)
      return leftName.localeCompare(rightName, 'pt-BR')
    })

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-3 text-emerald-950"
      onClick={onClose}
      onPointerDown={event => event.stopPropagation()}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="inventory-panel-title"
        className="flex max-h-[88vh] w-full max-w-2xl flex-col overflow-hidden rounded-2xl bg-emerald-50 shadow-2xl"
        onClick={event => event.stopPropagation()}
      >
        <header className="flex items-center justify-between gap-3 bg-emerald-700 px-4 py-3 text-white">
          <div>
            <h2 id="inventory-panel-title" className="text-lg font-bold">
              🎒 Inventário
            </h2>
            <p className="text-xs text-emerald-100">
              Confira tudo o que você guardou na fazenda
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded-full px-3 py-1 text-xl hover:bg-white/15"
            aria-label="Fechar inventário"
          >
            ×
          </button>
        </header>

        <section
          aria-label="Saldos"
          className="grid grid-cols-2 gap-2 border-b border-emerald-200 bg-white/70 p-3"
        >
          <div className="rounded-xl border border-amber-200 bg-amber-50 px-3 py-2">
            <p className="text-xs font-semibold uppercase tracking-wide text-amber-700">
              Moedas
            </p>
            <p className="mt-1 text-lg font-bold text-amber-900">
              🪙 {inventory ? numberFormatter.format(inventory.coins) : '—'}
            </p>
          </div>
          <div className="rounded-xl border border-pink-200 bg-pink-50 px-3 py-2">
            <p className="text-xs font-semibold uppercase tracking-wide text-pink-700">
              Premium
            </p>
            <p className="mt-1 text-lg font-bold text-pink-900">
              💎 {inventory ? numberFormatter.format(inventory.premiumCoins) : '—'}
            </p>
          </div>
        </section>

        <nav
          aria-label="Filtrar inventário"
          className="grid grid-cols-2 gap-1 border-b border-emerald-200 bg-white/70 p-2 sm:grid-cols-4"
        >
          <button
            type="button"
            className={filterButtonClass(filter === 'all')}
            onClick={() => setFilter('all')}
          >
            Tudo ({numberFormatter.format(seedUnits + cropUnits + otherUnits)})
          </button>
          <button
            type="button"
            className={filterButtonClass(filter === 'Seed')}
            onClick={() => setFilter('Seed')}
          >
            Sementes ({numberFormatter.format(seedUnits)})
          </button>
          <button
            type="button"
            className={filterButtonClass(filter === 'Crop')}
            onClick={() => setFilter('Crop')}
          >
            Colheitas ({numberFormatter.format(cropUnits)})
          </button>
          <button
            type="button"
            className={filterButtonClass(filter === 'Item')}
            onClick={() => setFilter('Item')}
          >
            Outros ({numberFormatter.format(otherUnits)})
          </button>
        </nav>

        <div className="min-h-64 flex-1 overflow-y-auto p-3">
          {loading ? (
            <div className="flex h-56 items-center justify-center text-sm text-emerald-700">
              Carregando inventário...
            </div>
          ) : !inventory ? (
            <div className="py-12 text-center text-emerald-700">
              <p className="text-4xl">🧺</p>
              <p className="mt-3 font-semibold">Não foi possível carregar o inventário.</p>
              <button
                type="button"
                onClick={() => void refreshInventory().catch(() => undefined)}
                className="mt-3 rounded-lg bg-emerald-600 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-700"
              >
                Tentar novamente
              </button>
            </div>
          ) : visibleItems.length === 0 ? (
            <div className="py-12 text-center text-emerald-700">
              <p className="text-4xl">🌱</p>
              <p className="mt-3 font-semibold">
                {filter === 'all'
                  ? 'Seu inventário ainda está vazio.'
                  : 'Nenhum item nesta categoria.'}
              </p>
              <p className="mt-1 text-sm">
                Compre sementes ou colha sua plantação para encher este espaço.
              </p>
            </div>
          ) : (
            <div className="grid gap-2 sm:grid-cols-2">
              {visibleItems.map(item => {
                const catalogItem = item.itemType === 'Item'
                  ? undefined
                  : getSeedByItem(item.itemType, item.itemId)
                const isSeed = item.itemType === 'Seed'
                const isCrop = item.itemType === 'Crop'

                return (
                  <article
                    key={`${item.itemType}-${item.itemId}`}
                    className="flex items-center gap-3 rounded-xl border border-transparent bg-white p-3 shadow-sm transition hover:border-emerald-200"
                  >
                    <span
                      className={`flex h-12 w-12 shrink-0 items-center justify-center rounded-full text-2xl ${
                        isSeed
                          ? 'bg-emerald-100'
                          : isCrop
                            ? 'bg-amber-100'
                            : 'bg-sky-100'
                      }`}
                      aria-hidden="true"
                    >
                      {isSeed ? '🌱' : isCrop ? (catalogItem?.icon ?? '🥕') : '📦'}
                    </span>
                    <div className="min-w-0 flex-1">
                      <h3 className="truncate font-semibold">
                        {catalogItem?.name ?? item.itemId}
                      </h3>
                      <p className="text-xs text-emerald-600">
                        {isSeed
                          ? 'Semente para plantio'
                          : isCrop
                            ? 'Colheita pronta para venda'
                            : 'Item especial'}
                      </p>
                    </div>
                    <span className="rounded-full bg-emerald-100 px-3 py-1 text-sm font-bold text-emerald-800">
                      ×{numberFormatter.format(item.quantity)}
                    </span>
                  </article>
                )
              })}
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
