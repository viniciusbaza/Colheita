export type PlotPlantDone = {
  plotId: string
  xpGained: number
}

export type PlotStealDone = {
  plotId: string
  stolen: number
  remainingYield: number
  xpGained: number
}

export type PlotStealFailed = {
  plotId: string
  reason: string
}

export type PlotHarvestDone = {
  plotId: string
  xpGained: number
}

export type StealResponse = {
  plotId: string
  stolen: number
  ownerWillReceive: number
  xpGained: number
}

export type PlantResponse = {
  id: string
  seed: string
  plantedAt: string
  readyAt: string
  xpGained: number
}

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
  ownerUserId: string
  ownerUsername: string
  plots: Plot[]
}

export type Seed = {
  id: string
  name: string
  icon: string
  buyPrice: number
  sellPrice: number
  growTime: string // "HH:mm:ss"
  theftChancePercent: number
  minLevel: number
  cropId: string
  cropAmount: number
}
