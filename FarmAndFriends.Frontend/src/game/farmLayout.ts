import { gridToIso } from './iso/isoUtils.ts'

export const FARM_TILE_WIDTH = 64
export const FARM_TILE_HEIGHT = 32
export const FARM_PLOT_WIDTH = 70.4
export const FARM_PLOT_HEIGHT = 35.2
export const FARM_INITIAL_GRID_WIDTH =
  FARM_PLOT_WIDTH + FARM_TILE_WIDTH * 2
export const FARM_INITIAL_GRID_HEIGHT =
  FARM_PLOT_HEIGHT + FARM_TILE_HEIGHT * 2

const FARM_EXPANDED_GRID_COLUMNS = 7
const FARM_EXPANDED_GRID_ROWS = 4
const FARM_EXPANDED_GRID_OFFSET_X = -FARM_TILE_WIDTH
const FARM_EXPANDED_GRID_OFFSET_Y = -FARM_TILE_HEIGHT

type GridPosition = {
  x: number
  y: number
}

export type FarmPlotLayout = {
  originX: number
  originY: number
  bounds: {
    left: number
    top: number
    right: number
    bottom: number
    width: number
    height: number
    centerX: number
    centerY: number
  }
}

export function calculateFarmEnvironmentExpansion(
  bounds: FarmPlotLayout['bounds'],
) {
  const plotExpansion = Math.max(
    bounds.width / FARM_INITIAL_GRID_WIDTH,
    bounds.height / FARM_INITIAL_GRID_HEIGHT,
  )

  // Uma expansão parcial preserva o tamanho dos plots, abre espaço ao redor
  // de grades maiores e evita esticar demais a ilustração do cenário.
  return Math.min(1.5, Math.max(1, 1 + (plotExpansion - 1) * 0.5))
}

function isCanonicalExpandedGrid(plots: readonly GridPosition[]) {
  if (
    plots.length !== FARM_EXPANDED_GRID_COLUMNS * FARM_EXPANDED_GRID_ROWS
  ) {
    return false
  }

  const coordinates = new Set(plots.map((plot) => `${plot.x}:${plot.y}`))

  for (let y = 0; y < FARM_EXPANDED_GRID_ROWS; y += 1) {
    for (let x = 0; x < FARM_EXPANDED_GRID_COLUMNS; x += 1) {
      if (!coordinates.has(`${x}:${y}`)) return false
    }
  }

  return true
}

/**
 * Mantém a grade inicial na ancoragem atual. A grade canônica 7x4 recebe um
 * deslocamento visual de um tile para cima e para a esquerda, afastando sua
 * extremidade direita da porteira sem alterar coordenadas autoritativas.
 */
export function calculateFarmPlotLayout(
  plots: readonly GridPosition[],
): FarmPlotLayout {
  if (plots.length === 0) {
    return {
      originX: 0,
      originY: 0,
      bounds: {
        left: -FARM_PLOT_WIDTH / 2,
        top: -FARM_PLOT_HEIGHT,
        right: FARM_PLOT_WIDTH / 2,
        bottom: 0,
        width: FARM_PLOT_WIDTH,
        height: FARM_PLOT_HEIGHT,
        centerX: 0,
        centerY: -FARM_PLOT_HEIGHT / 2,
      },
    }
  }

  let rawLeft = Number.POSITIVE_INFINITY
  let rawTop = Number.POSITIVE_INFINITY
  let rawRight = Number.NEGATIVE_INFINITY
  let rawBottom = Number.NEGATIVE_INFINITY

  for (const plot of plots) {
    const { isoX, isoY } = gridToIso(
      plot.x,
      plot.y,
      FARM_TILE_WIDTH,
      FARM_TILE_HEIGHT,
    )

    rawLeft = Math.min(rawLeft, isoX - FARM_PLOT_WIDTH / 2)
    rawRight = Math.max(rawRight, isoX + FARM_PLOT_WIDTH / 2)
    rawTop = Math.min(rawTop, isoY - FARM_PLOT_HEIGHT)
    rawBottom = Math.max(rawBottom, isoY)
  }

  const expandedGrid = isCanonicalExpandedGrid(plots)
  const originX = expandedGrid ? FARM_EXPANDED_GRID_OFFSET_X : 0
  const originY =
    -FARM_PLOT_HEIGHT
    - rawTop
    + (expandedGrid ? FARM_EXPANDED_GRID_OFFSET_Y : 0)
  const left = rawLeft + originX
  const top = rawTop + originY
  const right = rawRight + originX
  const bottom = rawBottom + originY

  return {
    originX,
    originY,
    bounds: {
      left,
      top,
      right,
      bottom,
      width: right - left,
      height: bottom - top,
      centerX: (left + right) / 2,
      centerY: (top + bottom) / 2,
    },
  }
}
