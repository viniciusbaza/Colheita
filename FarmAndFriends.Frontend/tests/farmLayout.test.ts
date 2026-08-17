import assert from 'node:assert/strict'
import test from 'node:test'
import {
  calculateFarmEnvironmentExpansion,
  calculateFarmPlotLayout,
  FARM_TILE_HEIGHT,
  FARM_TILE_WIDTH,
} from '../src/game/farmLayout.ts'
import { gridToIso } from '../src/game/iso/isoUtils.ts'

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

test('keeps the 3x3 anchor and moves every 7x4 plot one tile up-left', () => {
  const initialPlots = createGrid(3, 3)
  const initialLayout = calculateFarmPlotLayout(initialPlots)
  const expandedPlots = createGrid(7, 4)
  const expandedLayout = calculateFarmPlotLayout(expandedPlots)

  closeTo(initialLayout.originX, 0)
  closeTo(initialLayout.originY, 0)

  for (const plot of expandedPlots) {
    const { isoX, isoY } = gridToIso(
      plot.x,
      plot.y,
      FARM_TILE_WIDTH,
      FARM_TILE_HEIGHT,
    )

    closeTo(expandedLayout.originX + isoX, isoX - FARM_TILE_WIDTH)
    closeTo(expandedLayout.originY + isoY, isoY - FARM_TILE_HEIGHT)
  }

  closeTo(expandedLayout.originX, -64)
  closeTo(expandedLayout.originY, -32)
  closeTo(expandedLayout.bounds.centerX, -16)
  closeTo(expandedLayout.bounds.centerY, 22.4)
  closeTo(expandedLayout.bounds.top, -67.2)
  closeTo(expandedLayout.bounds.bottom, 112)
  closeTo(expandedLayout.bounds.width, 358.4)
  closeTo(expandedLayout.bounds.height, 179.2)
  closeTo(
    calculateFarmEnvironmentExpansion(expandedLayout.bounds),
    1.4032258,
  )
})

test('does not apply the 7x4 visual anchor to incomplete layouts', () => {
  const incompleteExpandedGrid = createGrid(7, 4).slice(0, -1)
  const layout = calculateFarmPlotLayout(incompleteExpandedGrid)

  closeTo(layout.originX, 0)
  closeTo(layout.originY, 0)
})

test('uses real sparse coordinates instead of assuming a rectangular matrix', () => {
  const layout = calculateFarmPlotLayout([
    { x: 5, y: 2 },
    { x: 8, y: 2 },
    { x: 6, y: 6 },
  ])

  closeTo(layout.bounds.centerX, 96)
  closeTo(layout.bounds.top, -35.2)
  assert.ok(layout.bounds.width > 0)
  assert.ok(layout.bounds.height > 0)
})
