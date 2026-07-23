import assert from 'node:assert/strict'
import test from 'node:test'
import {
  calculateFarmEnvironmentExpansion,
  calculateFarmPlotLayout,
} from '../src/game/farmLayout.ts'

function createGrid(columns: number, rows: number) {
  return Array.from({ length: columns * rows }, (_, index) => ({
    x: index % columns,
    y: Math.floor(index / columns),
  }))
}

function closeTo(actual: number, expected: number) {
  assert.ok(
    Math.abs(actual - expected) < 0.001,
    `esperava ${expected}, recebeu ${actual}`,
  )
}

test('preserves the current 3x3 plot placement', () => {
  const layout = calculateFarmPlotLayout(createGrid(3, 3))

  closeTo(layout.originX, 0)
  closeTo(layout.originY, 0)
  closeTo(layout.bounds.width, 198.4)
  closeTo(layout.bounds.height, 99.2)
  closeTo(calculateFarmEnvironmentExpansion(layout.bounds), 1)
})

test('centers a future 4x7 grid and grows it toward the gate', () => {
  const layout = calculateFarmPlotLayout(createGrid(4, 7))

  closeTo(layout.originX, 48)
  closeTo(layout.originY, 0)
  closeTo(layout.bounds.centerX, 0)
  closeTo(layout.bounds.top, -35.2)
  closeTo(layout.bounds.width, 358.4)
  closeTo(layout.bounds.height, 179.2)
  closeTo(calculateFarmEnvironmentExpansion(layout.bounds), 1.4032258)
})

test('uses real sparse coordinates instead of assuming a rectangular matrix', () => {
  const layout = calculateFarmPlotLayout([
    { x: 5, y: 2 },
    { x: 8, y: 2 },
    { x: 6, y: 6 },
  ])

  closeTo(layout.bounds.centerX, 0)
  closeTo(layout.bounds.top, -35.2)
  assert.ok(layout.bounds.width > 0)
  assert.ok(layout.bounds.height > 0)
})
