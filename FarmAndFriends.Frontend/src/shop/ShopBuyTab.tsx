import { useState } from 'react'
import type { Seed } from '../types/Farm'
import type { ShopItem } from '../types/Shop'
import { MAX_SHOP_QUANTITY, useShop } from './useShop'

type Props = {
  seeds: Seed[]
  loadingSeeds: boolean
  seedsError: string | null
  refreshSeeds: () => Promise<void>
  items: ShopItem[]
  loadingItems: boolean
  itemsError: string | null
  refreshItems: () => Promise<void>
  coins: number | null
  userLevel: number | null
  loadingWallet: boolean
  onSuccess: (message: string) => void
  onError: (message: string) => void
}

const numberFormatter = new Intl.NumberFormat('pt-BR')

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'Erro ao comprar sementes.'
}

function normalizeQuantity(value: string) {
  const parsed = Number(value)

  if (!Number.isFinite(parsed)) return 1
  return Math.min(MAX_SHOP_QUANTITY, Math.max(1, Math.floor(parsed)))
}

export function ShopBuyTab({
  seeds,
  loadingSeeds,
  seedsError,
  refreshSeeds,
  items,
  loadingItems,
  itemsError,
  refreshItems,
  coins,
  userLevel,
  loadingWallet,
  onSuccess,
  onError,
}: Props) {
  const { buyItem, buySeed } = useShop()
  const [quantities, setQuantities] = useState<Record<string, number>>({})
  const [busyProductId, setBusyProductId] = useState<string | null>(null)

  function getQuantity(seedId: string) {
    return quantities[seedId] ?? 1
  }

  function setQuantity(seedId: string, value: string) {
    setQuantities(current => ({
      ...current,
      [seedId]: normalizeQuantity(value),
    }))
  }

  async function handleBuySeed(seedId: string, quantity: number) {
    if (busyProductId) return

    const seed = seeds.find(item => item.id === seedId)
    if (!seed) {
      onError('Semente não encontrada.')
      return
    }

    setBusyProductId(seedId)

    try {
      await buySeed(seedId, quantity)
      onSuccess(`🌱 Você comprou ${quantity} semente(s) de ${seed.name}.`)
      setQuantities(current => ({ ...current, [seedId]: 1 }))
      window.dispatchEvent(new Event('inventory:changed'))
    } catch (error) {
      onError(getErrorMessage(error))
    } finally {
      setBusyProductId(null)
    }
  }

  async function handleBuyItem(item: ShopItem, quantity: number) {
    if (busyProductId) return

    setBusyProductId(item.id)

    try {
      await buyItem(item.id, quantity)
      onSuccess(`🛡️ Você comprou ${quantity} ${item.name}.`)
      setQuantities(current => ({ ...current, [item.id]: 1 }))
      window.dispatchEvent(new Event('inventory:changed'))
    } catch (error) {
      onError(
        error instanceof Error
          ? error.message
          : 'Erro ao comprar item.',
      )
    } finally {
      setBusyProductId(null)
    }
  }

  if (loadingSeeds || loadingItems) {
    return (
      <div className="flex h-56 items-center justify-center text-sm text-emerald-700">
        Carregando catálogo da loja...
      </div>
    )
  }

  const catalogError = seedsError ?? itemsError
  if (catalogError && seeds.length === 0 && items.length === 0) {
    return (
      <div className="py-12 text-center text-red-700">
        <p className="text-4xl">🌾</p>
        <p className="mt-3 font-semibold">Não foi possível carregar o catálogo.</p>
        <p className="mt-1 text-sm">{catalogError}</p>
        <button
          type="button"
          onClick={() => void Promise.all([refreshSeeds(), refreshItems()])}
          className="mt-3 rounded-lg bg-emerald-600 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-700"
        >
          Tentar novamente
        </button>
      </div>
    )
  }

  if (seeds.length === 0 && items.length === 0) {
    return (
      <div className="py-12 text-center text-emerald-700">
        <p className="text-4xl">🌾</p>
        <p className="mt-3 font-semibold">Nenhum produto disponível.</p>
        <p className="mt-1 text-sm">O catálogo da loja ainda está sendo preparado.</p>
      </div>
    )
  }

  return (
    <div className="grid gap-3 md:grid-cols-2">
      {items.map(item => {
        const quantity = getQuantity(item.id)
        const totalCost = item.buyPrice * quantity
        const isLocked = userLevel !== null && userLevel < item.minLevel
        const canAfford = coins !== null && coins >= totalCost
        const isBusy = busyProductId === item.id
        const actionDisabled =
          loadingWallet ||
          coins === null ||
          userLevel === null ||
          isLocked ||
          !canAfford ||
          busyProductId !== null

        let actionLabel = `Comprar · ${numberFormatter.format(totalCost)} 🪙`
        if (loadingWallet || coins === null || userLevel === null) {
          actionLabel = 'Carregando...'
        } else if (isLocked) {
          actionLabel = `Disponível no nível ${item.minLevel}`
        } else if (!canAfford) {
          actionLabel = 'Saldo insuficiente'
        } else if (isBusy) {
          actionLabel = 'Comprando...'
        }

        return (
          <article
            key={item.id}
            className={`flex flex-col rounded-xl border bg-white p-3 shadow-sm transition ${
              isLocked ? 'border-slate-200 opacity-75' : 'border-sky-200 hover:border-sky-400'
            }`}
          >
            <div className="flex items-start gap-3">
              <span
                className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-sky-100 text-2xl"
                aria-hidden="true"
              >
                {item.icon || '🛡️'}
              </span>
              <div className="min-w-0 flex-1">
                <div className="flex items-start justify-between gap-2">
                  <h3 className="font-semibold">{item.name}</h3>
                  <span className="shrink-0 rounded-full bg-sky-100 px-2 py-1 text-xs font-semibold text-sky-800">
                    Nível {item.minLevel}
                  </span>
                </div>
                <p className="mt-1 text-xs leading-relaxed text-slate-600">
                  {item.description}
                </p>
                {item.protectionDurationHours > 0 && (
                  <p className="mt-1 text-xs font-semibold text-sky-700">
                    🛡️ Proteção por {item.protectionDurationHours}h
                  </p>
                )}
                <p className="mt-1 text-sm font-semibold text-amber-700">
                  🪙 {numberFormatter.format(item.buyPrice)} por unidade
                </p>
              </div>
            </div>

            <div className="mt-4 flex items-end gap-2">
              <label className="min-w-0 flex-1 text-xs font-semibold text-emerald-700">
                Quantidade
                <input
                  type="number"
                  min={1}
                  max={MAX_SHOP_QUANTITY}
                  step={1}
                  value={quantity}
                  onChange={event => setQuantity(item.id, event.target.value)}
                  disabled={isBusy}
                  aria-label={`Quantidade de ${item.name}`}
                  className="mt-1 w-full rounded-lg border border-emerald-300 bg-white px-3 py-2 text-base text-emerald-950 outline-none transition focus:border-emerald-600 focus:ring-2 focus:ring-emerald-200 disabled:opacity-60"
                />
              </label>
              <div className="pb-2 text-right text-xs text-emerald-600">
                <span className="block">Total</span>
                <strong className="text-sm text-amber-800">
                  {numberFormatter.format(totalCost)} 🪙
                </strong>
              </div>
            </div>

            <button
              type="button"
              onClick={() => void handleBuyItem(item, quantity)}
              disabled={actionDisabled}
              className="mt-3 rounded-lg bg-sky-600 px-3 py-2 text-sm font-semibold text-white transition hover:bg-sky-700 disabled:cursor-not-allowed disabled:bg-slate-300 disabled:text-slate-600"
            >
              {actionLabel}
            </button>
          </article>
        )
      })}

      {seeds.map(seed => {
        const quantity = getQuantity(seed.id)
        const totalCost = seed.buyPrice * quantity
        const isLocked = userLevel !== null && userLevel < seed.minLevel
        const canAfford = coins !== null && coins >= totalCost
        const isBusy = busyProductId === seed.id
        const actionDisabled =
          loadingWallet ||
          coins === null ||
          userLevel === null ||
          isLocked ||
          !canAfford ||
          busyProductId !== null

        let actionLabel = `Comprar · ${numberFormatter.format(totalCost)} 🪙`
        if (loadingWallet || coins === null || userLevel === null) {
          actionLabel = 'Carregando...'
        } else if (isLocked) {
          actionLabel = `Disponível no nível ${seed.minLevel}`
        } else if (!canAfford) {
          actionLabel = 'Saldo insuficiente'
        } else if (isBusy) {
          actionLabel = 'Comprando...'
        }

        return (
          <article
            key={seed.id}
            className={`flex flex-col rounded-xl border bg-white p-3 shadow-sm transition ${
              isLocked ? 'border-slate-200 opacity-75' : 'border-transparent hover:border-emerald-200'
            }`}
          >
            <div className="flex items-start gap-3">
              <span
                className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-emerald-100 text-2xl"
                aria-hidden="true"
              >
                {seed.icon || '🌱'}
              </span>
              <div className="min-w-0 flex-1">
                <div className="flex items-start justify-between gap-2">
                  <h3 className="truncate font-semibold">{seed.name}</h3>
                  <span className="shrink-0 rounded-full bg-emerald-100 px-2 py-1 text-xs font-semibold text-emerald-800">
                    Nível {seed.minLevel}
                  </span>
                </div>
                <p className="mt-1 text-sm font-semibold text-amber-700">
                  🪙 {numberFormatter.format(seed.buyPrice)} por semente
                </p>
              </div>
            </div>

            <div className="mt-4 flex items-end gap-2">
              <label className="min-w-0 flex-1 text-xs font-semibold text-emerald-700">
                Quantidade
                <input
                  type="number"
                  min={1}
                  max={MAX_SHOP_QUANTITY}
                  step={1}
                  value={quantity}
                  onChange={event => setQuantity(seed.id, event.target.value)}
                  disabled={isBusy}
                  aria-label={`Quantidade de sementes de ${seed.name}`}
                  className="mt-1 w-full rounded-lg border border-emerald-300 bg-white px-3 py-2 text-base text-emerald-950 outline-none transition focus:border-emerald-600 focus:ring-2 focus:ring-emerald-200 disabled:opacity-60"
                />
              </label>
              <div className="pb-2 text-right text-xs text-emerald-600">
                <span className="block">Total</span>
                <strong className="text-sm text-amber-800">
                  {numberFormatter.format(totalCost)} 🪙
                </strong>
              </div>
            </div>

            <button
              type="button"
              onClick={() => void handleBuySeed(seed.id, quantity)}
              disabled={actionDisabled}
              className="mt-3 rounded-lg bg-emerald-600 px-3 py-2 text-sm font-semibold text-white transition hover:bg-emerald-700 disabled:cursor-not-allowed disabled:bg-slate-300 disabled:text-slate-600"
            >
              {actionLabel}
            </button>
          </article>
        )
      })}
    </div>
  )
}
