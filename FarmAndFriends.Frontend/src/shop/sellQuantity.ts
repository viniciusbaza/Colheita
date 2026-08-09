import { MAX_SHOP_QUANTITY } from './shopLimits.ts'

const numberFormatter = new Intl.NumberFormat('pt-BR')

export function getDefaultSellQuantity(available: number) {
  if (!Number.isFinite(available) || available < 1) return 0

  return Math.min(Math.floor(available), MAX_SHOP_QUANTITY)
}

export function parseSellQuantity(value: string, available: number) {
  const maximum = getDefaultSellQuantity(available)
  const normalized = value.trim()

  if (!/^\d+$/.test(normalized)) return null

  const quantity = Number(normalized)
  if (!Number.isSafeInteger(quantity) || quantity < 1 || quantity > maximum) {
    return null
  }

  return quantity
}

export function getSellActionLabel(
  quantity: number | null,
  available: number,
) {
  if (quantity === null) return 'Informe uma quantidade válida'
  if (quantity === available) return 'Vender tudo'

  return `Vender ${numberFormatter.format(quantity)}`
}

export function isSellQuantityLimited(available: number) {
  return available > MAX_SHOP_QUANTITY
}
