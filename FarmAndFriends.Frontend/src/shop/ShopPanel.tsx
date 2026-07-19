import { useEffect, useState } from 'react'
import { useInventory } from '../inventory/useInventory'
import { useSeeds } from '../seeds/useSeeds'
import { useUser } from '../user/useUser'
import { ShopBuyTab } from './ShopBuyTab'
import { ShopFeedback } from './ShopFeedback'
import { ShopSellTab } from './ShopSellTab'

type Props = {
  onClose: () => void
}

type ShopTab = 'buy' | 'sell'

type Feedback = {
  message: string
  type: 'success' | 'error'
}

const numberFormatter = new Intl.NumberFormat('pt-BR')

function tabButtonClass(active: boolean) {
  return `relative flex-1 rounded-lg px-3 py-2 text-sm font-semibold transition ${
    active
      ? 'bg-emerald-600 text-white shadow-sm'
      : 'text-emerald-700 hover:bg-emerald-100'
  }`
}

export function ShopPanel({ onClose }: Props) {
  const { inventory, loading } = useInventory()
  const {
    seeds,
    loading: loadingSeeds,
    error: seedsError,
    refreshSeeds,
  } = useSeeds()
  const { user, refreshUser } = useUser()
  const [tab, setTab] = useState<ShopTab>('buy')
  const [feedback, setFeedback] = useState<Feedback | null>(null)

  const cropUnits = (inventory?.items ?? [])
    .filter(item => item.itemType === 'Crop')
    .reduce((total, item) => total + item.quantity, 0)

  useEffect(() => {
    function closeOnEscape(event: KeyboardEvent) {
      if (event.key === 'Escape') onClose()
    }

    window.addEventListener('keydown', closeOnEscape)
    return () => window.removeEventListener('keydown', closeOnEscape)
  }, [onClose])

  useEffect(() => {
    if (!feedback) return

    const timeoutId = window.setTimeout(
      () => setFeedback(null),
      feedback.type === 'success' ? 2_500 : 3_500,
    )

    return () => window.clearTimeout(timeoutId)
  }, [feedback])

  function reportSuccess(message: string) {
    setFeedback({ message, type: 'success' })
    void refreshUser().catch(() => undefined)
  }

  function reportError(message: string) {
    setFeedback({ message, type: 'error' })
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-3 text-emerald-950"
      onClick={event => {
        if (event.target === event.currentTarget) onClose()
      }}
      onPointerDown={event => event.stopPropagation()}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="shop-panel-title"
        className="flex max-h-[88vh] w-full max-w-3xl flex-col overflow-hidden rounded-2xl bg-emerald-50 shadow-2xl"
        onClick={event => event.stopPropagation()}
      >
        <header className="flex items-center justify-between gap-3 bg-emerald-700 px-4 py-3 text-white">
          <div>
            <h2 id="shop-panel-title" className="text-lg font-bold">
              🏪 Loja da Fazenda
            </h2>
            <p className="text-xs text-emerald-100">
              Compre sementes e venda o resultado da sua colheita
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded-full px-3 py-1 text-xl hover:bg-white/15"
            aria-label="Fechar loja"
          >
            ×
          </button>
        </header>

        <section
          aria-label="Resumo do jogador"
          className="grid grid-cols-3 gap-2 border-b border-emerald-200 bg-white/70 p-3"
        >
          <div className="rounded-xl border border-amber-200 bg-amber-50 px-3 py-2">
            <p className="text-[11px] font-semibold uppercase tracking-wide text-amber-700 sm:text-xs">
              Moedas
            </p>
            <p className="mt-1 font-bold text-amber-900 sm:text-lg">
              🪙 {inventory ? numberFormatter.format(inventory.coins) : '—'}
            </p>
          </div>
          <div className="rounded-xl border border-pink-200 bg-pink-50 px-3 py-2">
            <p className="text-[11px] font-semibold uppercase tracking-wide text-pink-700 sm:text-xs">
              Premium
            </p>
            <p className="mt-1 font-bold text-pink-900 sm:text-lg">
              💎 {inventory ? numberFormatter.format(inventory.premiumCoins) : '—'}
            </p>
          </div>
          <div className="rounded-xl border border-emerald-200 bg-emerald-100 px-3 py-2">
            <p className="text-[11px] font-semibold uppercase tracking-wide text-emerald-700 sm:text-xs">
              Seu nível
            </p>
            <p className="mt-1 font-bold text-emerald-900 sm:text-lg">
              ⭐ {user?.level ?? '—'}
            </p>
          </div>
        </section>

        <nav
          aria-label="Ações da loja"
          className="flex gap-1 border-b border-emerald-200 bg-white/70 p-2"
        >
          <button
            type="button"
            onClick={() => setTab('buy')}
            className={tabButtonClass(tab === 'buy')}
          >
            🌱 Comprar sementes
          </button>
          <button
            type="button"
            onClick={() => setTab('sell')}
            className={tabButtonClass(tab === 'sell')}
          >
            🧺 Vender colheita
            {cropUnits > 0 && (
              <span
                className={`ml-1 inline-flex min-w-5 justify-center rounded-full px-1 text-xs ${
                  tab === 'sell'
                    ? 'bg-white/20 text-white'
                    : 'bg-amber-200 text-amber-900'
                }`}
              >
                {cropUnits > 99 ? '99+' : cropUnits}
              </span>
            )}
          </button>
        </nav>

        {feedback && (
          <div className="px-3 pt-3">
            <ShopFeedback message={feedback.message} type={feedback.type} />
          </div>
        )}

        <div className="min-h-72 flex-1 overflow-y-auto p-3">
          {tab === 'buy' ? (
            <ShopBuyTab
              seeds={seeds}
              loadingSeeds={loadingSeeds}
              seedsError={seedsError}
              refreshSeeds={refreshSeeds}
              coins={inventory?.coins ?? null}
              userLevel={user?.level ?? null}
              loadingWallet={loading}
              onSuccess={reportSuccess}
              onError={reportError}
            />
          ) : (
            <ShopSellTab
              seeds={seeds}
              loadingSeeds={loadingSeeds}
              seedsError={seedsError}
              refreshSeeds={refreshSeeds}
              inventory={inventory}
              loading={loading}
              onSuccess={reportSuccess}
              onError={reportError}
            />
          )}
        </div>
      </div>
    </div>
  )
}
