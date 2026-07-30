export function formatTimeRemaining(readyAt: string, now = Date.now()) {
  const readyTime = new Date(readyAt).getTime()

  const diffMs = readyTime - now

  if (diffMs <= 0) return '✅'

  const totalSeconds = Math.floor(diffMs / 1000)
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60

  if (minutes > 0) {
    return `${minutes}m ${seconds}s`
  }

  return `${seconds}s`
}

type PlotReadySchedule = {
  seedId?: string | null
  isReady: boolean
  readyAt?: string | null
}

type PlotCareSchedule = PlotReadySchedule & {
  care?: {
    canCare: boolean
    rewardAvailable: boolean
    nextCareAt?: string | null
    careCycleEndsAt?: string | null
  } | null
}

export function getNextPlotReadyAt(
  plots: readonly PlotReadySchedule[],
): number | null {
  let nextReadyAt: number | null = null

  for (const plot of plots) {
    if (!plot.seedId || plot.isReady || !plot.readyAt) continue

    const readyAt = new Date(plot.readyAt).getTime()
    if (!Number.isFinite(readyAt)) continue

    nextReadyAt = nextReadyAt === null
      ? readyAt
      : Math.min(nextReadyAt, readyAt)
  }

  return nextReadyAt
}

export function getNextCareAt(
  plots: readonly PlotCareSchedule[],
  now = Date.now(),
): number | null {
  let nextCareAt: number | null = null

  for (const plot of plots) {
    if (
      !plot.seedId
      || plot.isReady
      || !plot.care
    ) {
      continue
    }

    const careDeadline = plot.care.canCare
      ? plot.care.rewardAvailable
        ? null
        : plot.care.careCycleEndsAt
      : plot.care.nextCareAt

    if (!careDeadline) continue

    const careAt = new Date(careDeadline).getTime()
    const readyAt = plot.readyAt
      ? new Date(plot.readyAt).getTime()
      : Number.NaN

    if (
      !Number.isFinite(careAt)
      || careAt <= now
      || (Number.isFinite(readyAt) && careAt >= readyAt)
    ) {
      continue
    }

    nextCareAt = nextCareAt === null
      ? careAt
      : Math.min(nextCareAt, careAt)
  }

  return nextCareAt
}

export function parseTimeSpanToSeconds(time: string) {
  const [h, m, s] = time.split(':').map(Number)
  return h * 3600 + m * 60 + s
}
