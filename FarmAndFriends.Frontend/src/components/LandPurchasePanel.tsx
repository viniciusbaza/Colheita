import { useState } from 'react'
import type {
  LandOffer,
  LandPaymentCurrency,
  LandPurchaseResponse,
} from '../types/Farm'
import {
  getLandPurchaseProgressValue,
  getLandPurchaseOptionState,
  LAND_PAYMENT_CURRENCIES,
} from '../land/landPurchase'
import { useLandPurchase } from '../land/useLandPurchase'
import type { LandPurchaseAttemptStore } from '../land/useLandPurchase'

const numberFormatter = new Intl.NumberFormat('pt-BR')

type Props = {
  offer: LandOffer
  coins: number | null
  premiumCoins: number | null
  playerLevel: number | null
  attemptStore: LandPurchaseAttemptStore
  refreshFarm: (response: LandPurchaseResponse) => Promise<void>
  refreshInventory: () => Promise<void>
  onReconciliationStarted: (response: LandPurchaseResponse) => void
  onReconciliationFailed: (
    response: LandPurchaseResponse,
    message: string,
  ) => void
  onConfirmed: (response: LandPurchaseResponse) => void
  onPendingChange: (pending: boolean) => void
}

const currencyPresentation: Record<
  LandPaymentCurrency,
  { icon: string; label: string; balanceLabel: string }
> = {
  coins: {
    icon: '🪙',
    label: 'Moedas',
    balanceLabel: 'moedas',
  },
  premiumCoins: {
    icon: '💵',
    label: 'Notas',
    balanceLabel: 'notas',
  },
}

function optionStatus(
  currency: LandPaymentCurrency,
  state: ReturnType<typeof getLandPurchaseOptionState>,
  minLevel: number,
  playerLevel: number | null,
) {
  if (state.reason === 'data-unavailable') {
    return 'Consultando seu saldo e nível.'
  }

  if (state.reason === 'level') {
    return `Requer nível ${minLevel}; seu nível atual é ${playerLevel}.`
  }

  if (state.reason === 'insufficient-balance') {
    return `Faltam ${numberFormatter.format(state.missingAmount)} ${currencyPresentation[currency].balanceLabel}.`
  }

  return 'Saldo e nível suficientes.'
}

export function LandPurchasePanel({
  offer,
  coins,
  premiumCoins,
  playerLevel,
  attemptStore,
  refreshFarm,
  refreshInventory,
  onReconciliationStarted,
  onReconciliationFailed,
  onConfirmed,
  onPendingChange,
}: Props) {
  const [selectedCurrency, setSelectedCurrency] = useState<
    LandPaymentCurrency | null
  >(null)
  const {
    pending,
    error,
    prepareAttempt,
    purchase,
  } = useLandPurchase({
    attemptStore,
    refreshFarm,
    refreshInventory,
    onReconciliationStarted,
    onReconciliationFailed,
    onConfirmed,
    onPendingChange,
  })

  const balances: Record<LandPaymentCurrency, number | null> = {
    coins,
    premiumCoins,
  }
  const optionStates: Record<
    LandPaymentCurrency,
    ReturnType<typeof getLandPurchaseOptionState>
  > = {
    coins: getLandPurchaseOptionState(
      offer,
      'coins',
      balances.coins,
      playerLevel,
    ),
    premiumCoins: getLandPurchaseOptionState(
      offer,
      'premiumCoins',
      balances.premiumCoins,
      playerLevel,
    ),
  }
  const selectedOption = selectedCurrency
    ? optionStates[selectedCurrency]
    : null
  const progressMax = Math.max(1, offer.maxPlots)
  const progressValue = getLandPurchaseProgressValue(offer)

  function selectCurrency(currency: LandPaymentCurrency) {
    if (!optionStates[currency].enabled || pending) return

    prepareAttempt(offer.plotId, currency)
    setSelectedCurrency(currency)
  }

  async function handlePurchase() {
    if (!selectedCurrency || !selectedOption?.enabled || pending) return
    await purchase(offer.plotId, selectedCurrency)
  }

  return (
    <section
      aria-labelledby="land-purchase-title"
      aria-busy={pending}
      className="mt-3 rounded-xl border border-amber-300 bg-amber-50 p-3 text-amber-950 shadow-sm"
    >
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="text-xs font-bold uppercase tracking-wide text-amber-700">
            Expansão da fazenda
          </p>
          <h3 id="land-purchase-title" className="font-bold">
            Liberar lote {offer.plotNumber}
          </h3>
        </div>
        <span className="rounded-full bg-white px-2 py-1 text-xs font-bold text-amber-800">
          {progressValue}/{offer.maxPlots} liberados
        </span>
      </div>

      <div
        role="progressbar"
        aria-label={`Progresso da expansão: ${progressValue} de ${offer.maxPlots} lotes liberados`}
        aria-valuemin={0}
        aria-valuemax={progressMax}
        aria-valuenow={progressValue}
        className="mt-2 h-2 overflow-hidden rounded-full bg-amber-200"
      >
        <div
          className="h-full rounded-full bg-emerald-600"
          style={{ width: `${(progressValue / progressMax) * 100}%` }}
        />
      </div>

      <p className="mt-2 text-xs text-amber-800">
        Nível necessário: <strong>{offer.minLevel}</strong>
        {playerLevel !== null && <> · Seu nível: <strong>{playerLevel}</strong></>}
      </p>

      <fieldset className="mt-3 space-y-2" disabled={pending}>
        <legend className="mb-2 text-sm font-semibold">
          Escolha como pagar
        </legend>
        {LAND_PAYMENT_CURRENCIES.map(currency => {
          const presentation = currencyPresentation[currency]
          const state = optionStates[currency]
          const inputId = `land-payment-${currency}`
          const statusId = `${inputId}-status`

          return (
            <label
              key={currency}
              htmlFor={inputId}
              className={`block rounded-lg border p-2 transition ${
                selectedCurrency === currency
                  ? 'border-emerald-600 bg-emerald-50 ring-2 ring-emerald-200'
                  : state.enabled
                    ? 'cursor-pointer border-amber-200 bg-white hover:border-amber-400'
                    : 'cursor-not-allowed border-slate-200 bg-slate-100 text-slate-500'
              }`}
            >
              <span className="flex items-start gap-2">
                <input
                  id={inputId}
                  type="radio"
                  name="land-payment-currency"
                  value={currency}
                  checked={selectedCurrency === currency}
                  onChange={() => selectCurrency(currency)}
                  disabled={!state.enabled || pending}
                  aria-describedby={statusId}
                  className="mt-1 accent-emerald-700"
                />
                <span className="min-w-0 flex-1">
                  <span className="flex justify-between gap-2 text-sm font-bold">
                    <span>{presentation.icon} {presentation.label}</span>
                    <span>{numberFormatter.format(state.price)}</span>
                  </span>
                  <span className="block text-xs">
                    Seu saldo: {state.balance === null
                      ? '—'
                      : numberFormatter.format(state.balance)}
                  </span>
                  <span
                    id={statusId}
                    className={`mt-1 block text-xs ${
                      state.enabled ? 'text-emerald-700' : 'text-slate-600'
                    }`}
                  >
                    {optionStatus(currency, state, offer.minLevel, playerLevel)}
                  </span>
                </span>
              </span>
            </label>
          )
        })}
      </fieldset>

      {offer.expandsTo && (
        <p className="mt-3 rounded-md bg-amber-100 px-2 py-1.5 text-xs font-semibold text-amber-900">
          Isso vai ampliar a fazenda para {offer.expandsTo.columns} × {offer.expandsTo.rows}.
        </p>
      )}

      <button
        type="button"
        onClick={() => void handlePurchase()}
        disabled={!selectedCurrency || !selectedOption?.enabled || pending}
        className="mt-3 w-full rounded-lg bg-emerald-700 px-3 py-2 text-sm font-bold text-white hover:bg-emerald-800 disabled:cursor-not-allowed disabled:bg-slate-300"
      >
        {pending ? 'Comprando...' : 'Comprar'}
      </button>

      {error && (
        <p
          role="alert"
          className="mt-3 rounded-lg border border-red-300 bg-red-100 px-3 py-2 text-sm text-red-700"
        >
          {error}
        </p>
      )}
    </section>
  )
}
