import assert from 'node:assert/strict'
import test from 'node:test'
import {
  getDefaultSellQuantity,
  getSellActionLabel,
  isSellQuantityLimited,
  parseSellQuantity,
} from '../src/shop/sellQuantity.ts'

test('uses the full stock as the default up to the transaction limit', () => {
  assert.equal(getDefaultSellQuantity(1), 1)
  assert.equal(getDefaultSellQuantity(9_999), 9_999)
  assert.equal(getDefaultSellQuantity(10_000), 10_000)
})

test('caps the default at 10,000 without treating a partial sale as selling all', () => {
  assert.equal(getDefaultSellQuantity(10_001), 10_000)
  assert.equal(getDefaultSellQuantity(15_000), 10_000)
  assert.equal(isSellQuantityLimited(10_000), false)
  assert.equal(isSellQuantityLimited(10_001), true)
  assert.equal(getSellActionLabel(10_000, 10_000), 'Vender tudo')
  assert.equal(getSellActionLabel(10_000, 10_001), 'Vender 10.000')
})

test('accepts an edited whole quantity within stock and transaction limits', () => {
  assert.equal(parseSellQuantity('1', 15_000), 1)
  assert.equal(parseSellQuantity(' 250 ', 15_000), 250)
  assert.equal(parseSellQuantity('9999', 10_000), 9_999)
  assert.equal(getSellActionLabel(250, 15_000), 'Vender 250')
})

test('rejects empty, fractional, non-numeric, and out-of-range edits', () => {
  assert.equal(parseSellQuantity('', 15_000), null)
  assert.equal(parseSellQuantity('1.5', 15_000), null)
  assert.equal(parseSellQuantity('abc', 15_000), null)
  assert.equal(parseSellQuantity('0', 15_000), null)
  assert.equal(parseSellQuantity('-1', 15_000), null)
  assert.equal(parseSellQuantity('10001', 15_000), null)
  assert.equal(parseSellQuantity('2', 1), null)
  assert.equal(getSellActionLabel(null, 15_000), 'Informe uma quantidade válida')
})
