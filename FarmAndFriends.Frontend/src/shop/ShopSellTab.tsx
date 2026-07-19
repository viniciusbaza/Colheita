import { useState } from 'react'
import type { Seed } from '../types/Farm'
import type { Inventory } from '../types/Inventory'
import { MAX_SHOP_QUANTITY, useShop } from './useShop'

type Props = {
  seeds: Seed[]
  loadingSeeds: boolean
  seedsError: string | null
  refreshSeeds: () => Promise<void>
  inventory: Inventory | null
  loading: boolean
  onSuccess: (message: string) => void
  onError: (message: string) => void
}

const numberFormatter = new Intl.NumberFormat('pt-BR')

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'Erro ao vender a colheita.'
}

function normalizeQuantity(value: string, available: number) {
  const parsed = Number(value)

  if (!Number.isFinite(parsed)) return 1
  return Math.min(
    available,
    MAX_SHOP_QUANTITY,
    Math.max(1, Math.floor(parsed)),
  )
}

export function ShopSellTab({
  seeds,
  loadingSeeds,
  seedsError,
  refreshSeeds,
  inventory,
  loading,
  onSuccess,
  onError,
}: Props) {
  const { sellCrop } = useShop()
  const [quantities, setQuantities] = useState<Record<string, number>>({})
  const [busyCropId, setBusyCropId] = useState<string | null>(null)

  const crops = (inventory?.items ?? []).filter(
    item => item.itemType === 'Crop' && item.quantity > 0,
  )

  function getSeedByCrop(cropId: string) {
    return seeds.find(seed => seed.cropId === cropId)
  }

  async function handleSell(cropId: string, quantity: number) {
    if (busyCropId) return

    const seed = getSeedByCrop(cropId)
    if (!seed) {
      onError('Não foi possível identificar esta colheita.')
      return
    }

    setBusyCropId(cropId)

    try {
      await sellCrop(cropId, quantity)
      onSuccess(`🪙 Você vendeu ${quantity} unidade(s) de ${seed.name}.`)
      setQuantities(current => ({ ...current, [cropId]: 1 }))
      window.dispatchEvent(new Event('inventory:changed'))
    } catch (error) {
      onError(getErrorMessage(error))
    } finally {
      setBusyCropId(null)
    }
  }

  if (loading || loadingSeeds) {
    return (
      <div className="flex h-56 items-center justify-center text-sm text-emerald-700">
        Carregando sua colheita...
      </div>
    )
  }

  if (seedsError) {
    return (
      <div className="py-12 text-center text-red-700">
        <p className="text-4xl">🧺</p>
        <p className="mt-3 font-semibold">Não foi possível carregar os preços.</p>
        <p className="mt-1 text-sm">{seedsError}</p>
        <button
          type="button"
          onClick={() => void refreshSeeds()}
          className="mt-3 rounded-lg bg-emerald-600 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-700"
        >
          Tentar novamente
        </button>
      </div>
    )
  }

  if (!inventory) {
    return (
      <div className="py-12 text-center text-red-700">
        <p className="text-4xl">🧺</p>
        <p className="mt-3 font-semibold">Não foi possível carregar o inventário.</p>
        <p className="mt-1 text-sm">Feche a loja e tente novamente.</p>
      </div>
    )
  }

  if (crops.length === 0) {
    return (
      <div className="py-12 text-center text-emerald-700">
        <p className="text-4xl">🌱</p>
        <p className="mt-3 font-semibold">Você ainda não tem colheitas para vender.</p>
        <p className="mt-1 text-sm">Plante, aguarde o crescimento e faça a colheita.</p>
      </div>
    )
  }

  return (
    <div className="grid gap-3 md:grid-cols-2">
      {crops.map(crop => {
        const seed = getSeedByCrop(crop.itemId)
        const quantity = Math.min(
          Math.min(crop.quantity, MAX_SHOP_QUANTITY),
          quantities[crop.itemId] ?? 1,
        )
        const totalCoins = (seed?.sellPrice ?? 0) * quantity
        const isBusy = busyCropId === crop.itemId

        return (
          <article
            key={crop.itemId}
            className="flex flex-col rounded-xl border border-transparent bg-white p-3 shadow-sm transition hover:border-amber-200"
          >
            <div className="flex items-start gap-3">
              <span
                className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-amber-100 text-2xl"
                aria-hidden="true"
              >
                {seed?.icon ?? '🧺'}
              </span>
              <div className="min-w-0 flex-1">
                <div className="flex items-start justify-between gap-2">
                  <h3 className="truncate font-semibold">
                    {seed?.name ?? 'Colheita desconhecida'}
                  </h3>
                  <span className="shrink-0 rounded-full bg-amber-100 px-2 py-1 text-xs font-bold text-amber-900">
                    ×{numberFormatter.format(crop.quantity)}
                  </span>
                </div>
                <p className="mt-1 text-sm font-semibold text-amber-700">
                  🪙 {numberFormatter.format(seed?.sellPrice ?? 0)} por unidade
                </p>
              </div>
            </div>

            <div className="mt-4 flex items-end gap-2">
              <label className="min-w-0 flex-1 text-xs font-semibold text-emerald-700">
                Quantidade
                <input
                  type="number"
                  min={1}
                  max={Math.min(crop.quantity, MAX_SHOP_QUANTITY)}
                  step={1}
                  value={quantity}
                  onChange={event => setQuantities(current => ({
                    ...current,
                    [crop.itemId]: normalizeQuantity(
                      event.target.value,
                      crop.quantity,
                    ),
                  }))}
                  disabled={isBusy}
                  aria-label={`Quantidade de ${seed?.name ?? 'colheita'} para vender`}
                  className="mt-1 w-full rounded-lg border border-emerald-300 bg-white px-3 py-2 text-base text-emerald-950 outline-none transition focus:border-emerald-600 focus:ring-2 focus:ring-emerald-200 disabled:opacity-60"
                />
              </label>
              <div className="pb-2 text-right text-xs text-emerald-600">
                <span className="block">Você recebe</span>
                <strong className="text-sm text-amber-800">
                  {numberFormatter.format(totalCoins)} 🪙
                </strong>
              </div>
            </div>

            <button
              type="button"
              onClick={() => void handleSell(crop.itemId, quantity)}
              disabled={!seed || busyCropId !== null}
              className="mt-3 rounded-lg bg-amber-500 px-3 py-2 text-sm font-semibold text-amber-950 transition hover:bg-amber-400 disabled:cursor-not-allowed disabled:bg-slate-300 disabled:text-slate-600"
            >
              {isBusy
                ? 'Vendendo...'
                : `Vender · ${numberFormatter.format(totalCoins)} 🪙`}
            </button>
          </article>
        )
      })}
    </div>
  )
}
