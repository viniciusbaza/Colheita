export type VisitRecoveryFeedback = {
  message: string
}

function hasHttpStatus(
  error: unknown,
  expectedStatus: number,
): boolean {
  if (!error || typeof error !== 'object') return false

  return 'status' in error && error.status === expectedStatus
}

export function getVisitRecoveryFeedback(
  error: unknown,
  ownerUsername?: string,
): VisitRecoveryFeedback | null {
  if (!hasHttpStatus(error, 403)) return null

  const owner = ownerUsername?.trim()

  return {
    message: owner
      ? `A visita a ${owner} não está mais disponível. Você voltou para sua fazenda.`
      : 'Essa visita não está mais disponível. Você voltou para sua fazenda.',
  }
}
