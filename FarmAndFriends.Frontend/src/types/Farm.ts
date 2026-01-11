export type HarvestResponse = {
  id: string
  crop: string
  amount: number
  inventoryTotal: number
  xpGained: number
}

type PlotVisualState =
  | 'empty'
  | 'planted'
  | 'growing'
  | 'ready'
  | 'harvesting'

export type Plot = {
  id: string
  x: number
  y: number
  unlocked: boolean
  seedId?: string | null
  plantedAt?: string | null
  isReady: boolean
  readyAt?: string | null
  remainingYield: number
  state: PlotVisualState
  showXp?: number
}

export type Farm = {
  id: string
  name: string
  plots: Plot[]
}
