export function formatTimeRemaining(readyAt: string) {
  const now = Date.now()
  const readyTime = new Date(readyAt).getTime()

  const diffMs = readyTime - now

  if (diffMs <= 0) return 'Pronto!'

  const totalSeconds = Math.floor(diffMs / 1000)
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60

  if (minutes > 0) {
    return `${minutes}m ${seconds}s`
  }

  return `${seconds}s`
}
