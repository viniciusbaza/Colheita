export type PlotPlantDone = {
  plotId: string
  xpGained: number
}

export type PlotStealDone = {
  plotId: string
  stolen: number
  remainingYield: number
  xpGained: number
  pestCancelled: boolean
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

export type PlotPestRemoveDone = {
  plotId: string
  pestOccurrenceId: string
  coinsGained: number
  xpGained: number
}

export type PlotHarvestDone = {
  plotId: string
  xpGained: number
}

export type LandPaymentCurrency = 'coins' | 'premiumCoins'

export type FarmDimensions = {
  columns: number
  rows: number
}

export type LandOffer = {
  plotId: string
  plotNumber: number
  maxPlots: number
  minLevel: number
  prices: Record<LandPaymentCurrency, number>
  expandsTo: FarmDimensions | null
}

export type LandPurchaseRequest = {
  paymentCurrency: LandPaymentCurrency
}

export type LandPurchaseResponse = {
  completionId: string
  plotId: string
  plotNumber: number
  paymentCurrency: LandPaymentCurrency
  amountSpent: number
  addedPlotCount: number
  expandedTo: FarmDimensions | null
  replayed: boolean
}

export type StealResponse = {
  plotId: string
  stolen: number
  ownerWillReceive: number
  xpGained: number
  pestCancelled: boolean
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

export type PestStatus =
  | 'none'
  | 'scheduled'
  | 'active'
  | 'removed'
  | 'consumed'
  | 'cancelledByTheft'
  | 'cancelledByHarvest'
  | 'cancelledByProtection'

export type PlotPest = {
  type: 'caterpillar'
  status: PestStatus
  scheduledAt: string | null
  appearsAt: string | null
  appearedAt: string | null
  consumesAt: string | null
  resolvedAt: string | null
  consumedAmount: number
  canRemove: boolean
  occurrenceId: string | null
}

type PestMutationResponse = {
  plotId: string
  pest: PlotPest | null
  status: PestStatus
  remainingYield: number | null
}

export type PestActionResponse = PestMutationResponse & {
  completionId: string
  pestOccurrenceId: string
  coinsGained: number
  xpGained: number
  coins: number
  rewardGranted: boolean
  replayed: boolean
}

export type ApplyPestProtectionResponse = PestMutationResponse & {
  itemId: string
  remainingItemQuantity: number
  protectedUntil: string
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
  remainingYield: number | null
  state: PlotVisualState
  showXp?: number
  care?: PlotCare | null
  protectedUntil: string | null
  pest: PlotPest | null
}

export type Farm = {
  id: string
  name: string
  ownerUserId: string
  ownerUsername: string
  nextPestCheckAt: string | null
  landOffer: LandOffer | null
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
