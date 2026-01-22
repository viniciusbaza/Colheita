export function formatTimeRemaining(readyAt: string) {
  const now = Date.now()
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

export function parseTimeSpanToSeconds(time: string) {
  const [h, m, s] = time.split(':').map(Number)
  return h * 3600 + m * 60 + s
}