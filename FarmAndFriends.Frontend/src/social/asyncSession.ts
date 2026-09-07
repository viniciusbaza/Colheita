export interface AsyncSession {
  token: string | null
  generation: number
}

export function advanceAsyncSession(
  current: AsyncSession,
  token: string | null,
): AsyncSession {
  if (current.token === token) return current

  return {
    token,
    generation: current.generation + 1,
  }
}

export function isAsyncSessionCurrent(
  current: AsyncSession,
  captured: AsyncSession,
) {
  return current === captured
}
