import type {
  LandOffer,
  LandPaymentCurrency,
  LandPurchaseResponse,
} from '../types/Farm'

export const LAND_PAYMENT_CURRENCIES = [
  'coins',
  'premiumCoins',
] as const satisfies readonly LandPaymentCurrency[]

export type LandPurchaseAttempt = {
  plotId: string
  paymentCurrency: LandPaymentCurrency
  key: string
}

export type LandPurchaseOptionState = {
  enabled: boolean
  reason: 'available' | 'level' | 'data-unavailable' | 'insufficient-balance'
  price: number
  balance: number | null
  missingAmount: number
}

export function isLandPaymentCurrency(
  value: string,
): value is LandPaymentCurrency {
  return LAND_PAYMENT_CURRENCIES.some(currency => currency === value)
}

export function getLandPurchaseAttempt(
  currentAttempt: LandPurchaseAttempt | null,
  plotId: string,
  paymentCurrency: LandPaymentCurrency,
  createKey: () => string,
): LandPurchaseAttempt {
  if (
    currentAttempt?.plotId === plotId
    && currentAttempt.paymentCurrency === paymentCurrency
  ) {
    return currentAttempt
  }

  return {
    plotId,
    paymentCurrency,
    key: createKey(),
  }
}

export function getLandPurchaseOptionState(
  offer: LandOffer,
  paymentCurrency: LandPaymentCurrency,
  balance: number | null,
  playerLevel: number | null,
): LandPurchaseOptionState {
  const price = offer.prices[paymentCurrency]

  if (playerLevel === null || balance === null) {
    return {
      enabled: false,
      reason: 'data-unavailable',
      price,
      balance,
      missingAmount: 0,
    }
  }

  if (playerLevel < offer.minLevel) {
    return {
      enabled: false,
      reason: 'level',
      price,
      balance,
      missingAmount: 0,
    }
  }

  if (balance < price) {
    return {
      enabled: false,
      reason: 'insufficient-balance',
      price,
      balance,
      missingAmount: price - balance,
    }
  }

  return {
    enabled: true,
    reason: 'available',
    price,
    balance,
    missingAmount: 0,
  }
}

export function getLandPurchaseProgressValue(offer: LandOffer): number {
  const maximum = Math.max(1, offer.maxPlots)
  return Math.min(maximum, Math.max(0, offer.plotNumber - 1))
}

export function hasFarmExpansion(response: LandPurchaseResponse): boolean {
  return response.expandedTo !== null
}

export function getLandPurchaseSuccessMessage(
  response: LandPurchaseResponse,
): string {
  if (response.replayed) {
    return `Compra do lote ${response.plotNumber} já confirmada. Resultado recuperado.`
  }

  if (response.expandedTo) {
    return `Fazenda ampliada para ${response.expandedTo.columns} × ${response.expandedTo.rows}! ${response.addedPlotCount} novos lotes foram adicionados.`
  }

  return `Lote ${response.plotNumber} liberado com sucesso!`
}
