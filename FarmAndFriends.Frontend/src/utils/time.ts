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

export function formatCountdown(deadline: string, now = Date.now()) {
  const deadlineTime = new Date(deadline).getTime()
  if (!Number.isFinite(deadlineTime)) return null

  const remainingSeconds = Math.max(
    0,
    Math.ceil((deadlineTime - now) / 1000),
  )
  const hours = Math.floor(remainingSeconds / 3600)
  const minutes = Math.floor((remainingSeconds % 3600) / 60)
  const seconds = remainingSeconds % 60

  const clock = [minutes, seconds]
    .map(value => String(value).padStart(2, '0'))

  return hours > 0
    ? [hours, ...clock].map(value => String(value).padStart(2, '0')).join(':')
    : clock.join(':')
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

type PlotPestSchedule = {
  protectedUntil?: string | null
  pest?: {
    status: string
    appearsAt?: string | null
    consumesAt?: string | null
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

export function getNextPestOrProtectionAt(
  plots: readonly PlotPestSchedule[],
  now = Date.now(),
): number | null {
  let nextDeadline: number | null = null

  function include(deadline: string | null | undefined) {
    if (!deadline) return

    const timestamp = new Date(deadline).getTime()
    if (!Number.isFinite(timestamp) || timestamp <= now) return

    nextDeadline = nextDeadline === null
      ? timestamp
      : Math.min(nextDeadline, timestamp)
  }

  for (const plot of plots) {
    include(plot.protectedUntil)

    if (plot.pest?.status === 'scheduled') {
      include(plot.pest.appearsAt)
    } else if (plot.pest?.status === 'active') {
      include(plot.pest.consumesAt)
    }
  }

  return nextDeadline
}

export function getNextFarmStateAt(
  plots: readonly PlotCareSchedule[] & readonly PlotPestSchedule[],
  includeCare: boolean,
  nextPestCheckAt: string | null = null,
  now = Date.now(),
): number | null {
  const pestCheckAt = nextPestCheckAt
    ? new Date(nextPestCheckAt).getTime()
    : Number.NaN
  const deadlines = [
    getNextPlotReadyAt(plots),
    includeCare ? getNextCareAt(plots, now) : null,
    getNextPestOrProtectionAt(plots, now),
    Number.isFinite(pestCheckAt) && pestCheckAt > now
      ? pestCheckAt
      : null,
  ].filter((deadline): deadline is number => deadline !== null)

  return deadlines.length > 0 ? Math.min(...deadlines) : null
}

export function parseTimeSpanToSeconds(time: string) {
  const [h, m, s] = time.split(':').map(Number)
  return h * 3600 + m * 60 + s
}
