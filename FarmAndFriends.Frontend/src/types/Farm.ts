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

export type PlotCareDone = {
  plotId: string
  coinsGained: number
  xpGained: number
  caredByUsername: string
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

export type CareResponse = {
  completionId: string
  plotId: string
  careOpportunityId: string
  caredAt: string
  nextCareAt: string
  caredByUserId: string
  caredByUsername: string
  coinsGained: number
  xpGained: number
  coins: number
  careCycleId: string
  careCycleEndsAt: string
  cycleRewardGranted: boolean
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

export type PlotCare = {
  opportunityId: string
  status: 'available' | 'cooldown' | 'observed'
  caredAt: string | null
  nextCareAt: string | null
  caredByUserId: string | null
  caredByUsername: string | null
  canCare: boolean
  rewardAvailable: boolean
  viewerCared: boolean
  careCycleEndsAt: string | null
  caregiverCount: number
  caregivers: PlotCaregiver[]
}

export type PlotCaregiver = {
  userId: string
  username: string
  caredAt: string
}

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
  care?: PlotCare | null
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
