export const AVATAR_SAVE_TIMEOUT_MS = 15_000

// A lost response must not leave the profile modal blocking the farm forever.
// The server may have saved the preference; retrying the same ID is safe.
export async function withAvatarSaveDeadline<T>(
  request: (signal: AbortSignal) => Promise<T>,
  timeoutMs = AVATAR_SAVE_TIMEOUT_MS,
): Promise<T> {
  const controller = new AbortController()
  let timeoutId: ReturnType<typeof setTimeout> | undefined
  const deadline = new Promise<never>((_, reject) => {
    timeoutId = setTimeout(() => {
      reject(new Error('Não foi possível confirmar o salvamento. Tente novamente.'))
      controller.abort()
    }, timeoutMs)
  })

  try {
    return await Promise.race([request(controller.signal), deadline])
  } finally {
    clearTimeout(timeoutId)
  }
}
