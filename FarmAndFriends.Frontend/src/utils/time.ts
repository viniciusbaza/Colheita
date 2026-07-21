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

export function parseTimeSpanToSeconds(time: string) {
  const [h, m, s] = time.split(':').map(Number)
  return h * 3600 + m * 60 + s
}
