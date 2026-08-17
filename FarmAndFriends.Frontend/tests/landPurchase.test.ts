import assert from 'node:assert/strict'
import test from 'node:test'
import type {
  LandOffer,
  LandPurchaseResponse,
} from '../src/types/Farm.ts'
import {
  getLandPurchaseAttempt,
  getLandPurchaseOptionState,
  getLandPurchaseProgressValue,
  getLandPurchaseSuccessMessage,
  hasFarmExpansion,
  isLandPaymentCurrency,
} from '../src/land/landPurchase.ts'

const offer: LandOffer = {
  plotId: 'plot-7',
  plotNumber: 7,
  maxPlots: 28,
  minLevel: 4,
  prices: {
    coins: 1_500,
    premiumCoins: 12,
  },
  expandsTo: null,
}

test('aceita somente as moedas previstas pelo contrato de seleção', () => {
  assert.equal(isLandPaymentCurrency('coins'), true)
  assert.equal(isLandPaymentCurrency('premiumCoins'), true)
  assert.equal(isLandPaymentCurrency('gems'), false)
})


test('preserva a chave no retry e troca ao mudar lote ou moeda', () => {
  let nextKey = 0
  const createKey = () => `key-${++nextKey}`

  const first = getLandPurchaseAttempt(null, 'plot-7', 'coins', createKey)
  const retry = getLandPurchaseAttempt(first, 'plot-7', 'coins', createKey)
  const otherCurrency = getLandPurchaseAttempt(
    retry,
    'plot-7',
    'premiumCoins',
    createKey,
  )
  const otherPlot = getLandPurchaseAttempt(
    otherCurrency,
    'plot-8',
    'premiumCoins',
    createKey,
  )

  assert.equal(retry, first)
  assert.equal(retry.key, 'key-1')
  assert.equal(otherCurrency.key, 'key-2')
  assert.equal(otherPlot.key, 'key-3')
})

test('mantém a tentativa para recuperar um POST confirmado após falha de sync', () => {
  const attemptStore = {
    current: getLandPurchaseAttempt(null, 'plot-9', 'coins', () => 'key-9'),
  }

  // Uma falha de reconciliação não limpa o store compartilhado.
  const retry = getLandPurchaseAttempt(
    attemptStore.current,
    'plot-9',
    'coins',
    () => 'unexpected-new-key',
  )

  assert.equal(retry, attemptStore.current)
  assert.equal(retry.key, 'key-9')
})

test('avalia nível e saldo por rota sem liberar premium abaixo do nível', () => {
  const lowLevelCoins = getLandPurchaseOptionState(offer, 'coins', 5_000, 3)
  const lowLevelPremium = getLandPurchaseOptionState(
    offer,
    'premiumCoins',
    100,
    3,
  )
  const insufficientCoins = getLandPurchaseOptionState(
    offer,
    'coins',
    1_000,
    4,
  )
  const affordablePremium = getLandPurchaseOptionState(
    offer,
    'premiumCoins',
    12,
    4,
  )

  assert.deepEqual(
    [lowLevelCoins.reason, lowLevelPremium.reason],
    ['level', 'level'],
  )
  assert.equal(lowLevelCoins.enabled, false)
  assert.equal(lowLevelPremium.enabled, false)
  assert.equal(insufficientCoins.reason, 'insufficient-balance')
  assert.equal(insufficientCoins.missingAmount, 500)
  assert.equal(affordablePremium.enabled, true)
  assert.equal(affordablePremium.reason, 'available')
})

test('mantém cada rota bloqueada enquanto o saldo autoritativo não chegou', () => {
  const unavailable = getLandPurchaseOptionState(offer, 'coins', null, 10)

  assert.equal(unavailable.enabled, false)
  assert.equal(unavailable.reason, 'data-unavailable')
})

test('conta somente lotes já liberados no progresso da oferta', () => {
  assert.equal(getLandPurchaseProgressValue(offer), 6)
  assert.equal(getLandPurchaseProgressValue({
    ...offer,
    plotNumber: 28,
  }), 27)
})

test('identifica expansão pela presença de dimensões autoritativas', () => {
  const response: LandPurchaseResponse = {
    completionId: 'completion-1',
    plotId: 'plot-7',
    plotNumber: 7,
    paymentCurrency: 'coins',
    amountSpent: 1_500,
    addedPlotCount: 19,
    expandedTo: { columns: 7, rows: 4 },
    replayed: false,
  }

  assert.equal(hasFarmExpansion(response), true)
  assert.equal(
    hasFarmExpansion({
      ...response,
      expandedTo: { columns: 4, rows: 7 },
    }),
    true,
  )
  assert.equal(
    hasFarmExpansion({
      ...response,
      expandedTo: { columns: 8, rows: 5 },
    }),
    true,
  )
  assert.equal(
    hasFarmExpansion({ ...response, expandedTo: null }),
    false,
  )
  assert.equal(
    getLandPurchaseSuccessMessage(response),
    'Fazenda ampliada para 7 × 4! 19 novos lotes foram adicionados.',
  )
  assert.equal(
    getLandPurchaseSuccessMessage({ ...response, replayed: true }),
    'Compra do lote 7 já confirmada. Resultado recuperado.',
  )
})
