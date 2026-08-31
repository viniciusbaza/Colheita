import type { Plot } from '../types/Farm.ts'

export type CropVisualStage = 'sprout' | 'mature' | 'ready'

export type CropStageVisual = {
  texture: string
  displayHeight: number
  groundOffsetX: number
  groundOffsetY: number
}

type CropVisualProfile = {
  sprout: CropStageVisual
  mature: CropStageVisual
  ready: CropStageVisual
}

export type ResolvedCropVisual = CropStageVisual & {
  stage: CropVisualStage
}

type CropVisualPlot = Pick<
  Plot,
  | 'unlocked'
  | 'seedId'
  | 'plantedAt'
  | 'readyAt'
  | 'isReady'
  | 'currentHarvestCycle'
>

export type ResolvedPlotVisual = {
  groundTexture: 'plot-locked' | 'plot-growing' | 'plot'
  crop: ResolvedCropVisual | null
}

const SPROUT_STAGE_THRESHOLD = 0.25
const MATURE_STAGE_THRESHOLD = 0.5

export const CROP_VISUALS: Readonly<Record<string, CropVisualProfile>> = {
  carrot: {
    sprout: { texture: 'crop-sprout-carrot', displayHeight: 28, groundOffsetX: -2, groundOffsetY: 8 },
    mature: { texture: 'crop-mature-carrot', displayHeight: 31, groundOffsetX: 0, groundOffsetY: 6 },
    ready: { texture: 'crop-ready-carrot', displayHeight: 33, groundOffsetX: 0, groundOffsetY: 9 },
  },
  corn: {
    sprout: { texture: 'crop-sprout-corn', displayHeight: 30, groundOffsetX: 0, groundOffsetY: 10 },
    mature: { texture: 'crop-mature-corn', displayHeight: 40, groundOffsetX: 0, groundOffsetY: 11 },
    ready: { texture: 'crop-ready-corn', displayHeight: 42, groundOffsetX: 0, groundOffsetY: 9 },
  },
  pumpkin: {
    sprout: { texture: 'crop-sprout-pumpkin', displayHeight: 31, groundOffsetX: -3, groundOffsetY: 10 },
    mature: { texture: 'crop-mature-pumpkin', displayHeight: 35, groundOffsetX: 0, groundOffsetY: 7 },
    ready: { texture: 'crop-ready-pumpkin', displayHeight: 31, groundOffsetX: 0, groundOffsetY: 7 },
  },
  tomato: {
    sprout: { texture: 'crop-sprout-tomato', displayHeight: 27, groundOffsetX: 0, groundOffsetY: 14 },
    mature: { texture: 'crop-mature-tomato', displayHeight: 46, groundOffsetX: 0, groundOffsetY: 12 },
    ready: { texture: 'crop-ready-tomato', displayHeight: 44, groundOffsetX: 0, groundOffsetY: 10 },
  },
  apple_tree: {
    sprout: { texture: 'crop-sprout-apple-tree', displayHeight: 42, groundOffsetX: -2, groundOffsetY: 16 },
    mature: { texture: 'crop-mature-apple-tree', displayHeight: 82, groundOffsetX: -3, groundOffsetY: 14 },
    ready: { texture: 'crop-ready-apple-tree', displayHeight: 84, groundOffsetX: -3, groundOffsetY: 14 },
  },
}

export const MAX_CROP_DISPLAY_HEIGHT = Math.max(
  ...Object.values(CROP_VISUALS).flatMap(profile => [
    profile.sprout.displayHeight,
    profile.mature.displayHeight,
    profile.ready.displayHeight,
  ]),
)

export const MAX_CROP_GROUND_OFFSET = Math.max(
  ...Object.values(CROP_VISUALS).flatMap(profile => [
    profile.sprout.groundOffsetY,
    profile.mature.groundOffsetY,
    profile.ready.groundOffsetY,
  ]),
)

function getInitialGrowthTimeline(plot: CropVisualPlot) {
  if (!plot.plantedAt || !plot.readyAt) return null

  const plantedAt = new Date(plot.plantedAt).getTime()
  const readyAt = new Date(plot.readyAt).getTime()
  const total = readyAt - plantedAt

  if (
    !Number.isFinite(plantedAt)
    || !Number.isFinite(readyAt)
    || !Number.isFinite(total)
    || total <= 0
  ) {
    return null
  }

  return { plantedAt, total }
}

function getGrowthProgress(plot: CropVisualPlot, now: number) {
  const timeline = getInitialGrowthTimeline(plot)
  if (!timeline) return 0

  return Math.min(
    Math.max((now - timeline.plantedAt) / timeline.total, 0),
    1,
  )
}

/**
 * Returns the next display-only boundary for the initial crop growth.
 * Readiness is deliberately excluded: only a later authoritative farm sync may
 * set `isReady` and reveal the ready visual.
 */
export function getNextCropVisualTransitionAt(
  plot: CropVisualPlot,
  now = Date.now(),
): number | null {
  if (
    !plot.unlocked
    || !plot.seedId
    || plot.isReady
    || (plot.currentHarvestCycle ?? 1) > 1
  ) {
    return null
  }

  const timeline = getInitialGrowthTimeline(plot)
  if (!timeline) return null

  const sproutAt = timeline.plantedAt
    + timeline.total * SPROUT_STAGE_THRESHOLD
  const matureAt = timeline.plantedAt
    + timeline.total * MATURE_STAGE_THRESHOLD

  if (now < sproutAt) return sproutAt
  if (now < matureAt) return matureAt
  return null
}

export function resolvePlotVisual(
  plot: CropVisualPlot,
  now = Date.now(),
): ResolvedPlotVisual {
  if (!plot.unlocked) {
    return { groundTexture: 'plot-locked', crop: null }
  }

  if (!plot.seedId) {
    return { groundTexture: 'plot', crop: null }
  }

  const profile = CROP_VISUALS[plot.seedId]

  if (plot.isReady) {
    return {
      groundTexture: 'plot',
      crop: profile ? { ...profile.ready, stage: 'ready' } : null,
    }
  }

  if ((plot.currentHarvestCycle ?? 1) > 1) {
    return {
      groundTexture: 'plot',
      crop: profile ? { ...profile.mature, stage: 'mature' } : null,
    }
  }

  const growthProgress = getGrowthProgress(plot, now)
  if (growthProgress < SPROUT_STAGE_THRESHOLD) {
    return { groundTexture: 'plot-growing', crop: null }
  }

  if (growthProgress < MATURE_STAGE_THRESHOLD) {
    return {
      groundTexture: 'plot',
      crop: profile ? { ...profile.sprout, stage: 'sprout' } : null,
    }
  }

  return {
    groundTexture: 'plot',
    crop: profile ? { ...profile.mature, stage: 'mature' } : null,
  }
}

export function resolvePlotGroundTexture(
  plot: CropVisualPlot,
  now = Date.now(),
) {
  return resolvePlotVisual(plot, now).groundTexture
}

export function resolveCropVisual(
  plot: CropVisualPlot,
  now = Date.now(),
): ResolvedCropVisual | null {
  return resolvePlotVisual(plot, now).crop
}
