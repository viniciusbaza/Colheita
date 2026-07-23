import { gridToIso } from './iso/isoUtils.ts'

export const FARM_TILE_WIDTH = 64
export const FARM_TILE_HEIGHT = 32
export const FARM_PLOT_WIDTH = 70.4
export const FARM_PLOT_HEIGHT = 35.2
export const FARM_INITIAL_GRID_WIDTH =
  FARM_PLOT_WIDTH + FARM_TILE_WIDTH * 2
export const FARM_INITIAL_GRID_HEIGHT =
  FARM_PLOT_HEIGHT + FARM_TILE_HEIGHT * 2

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

/**
 * Centraliza qualquer grade isométrica no eixo X e mantém sua borda superior
 * no mesmo ponto visual. Assim, expansões crescem em direção ao primeiro
 * plano/portão sem depender de uma matriz fixa como 3x3.
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

  const originX = -(rawLeft + rawRight) / 2
  const originY = -FARM_PLOT_HEIGHT - rawTop
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
