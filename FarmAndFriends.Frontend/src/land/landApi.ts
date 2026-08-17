import { authFetch } from '../api/http'
import type {
  LandPaymentCurrency,
  LandPurchaseRequest,
  LandPurchaseResponse,
} from '../types/Farm'

export function purchaseLandPlot(
  plotId: string,
  paymentCurrency: LandPaymentCurrency,
  idempotencyKey: string,
): Promise<LandPurchaseResponse> {
  const body: LandPurchaseRequest = { paymentCurrency }

  return authFetch<LandPurchaseResponse>(`/plots/${plotId}/purchase`, {
    method: 'POST',
    headers: { 'Idempotency-Key': idempotencyKey },
    body: JSON.stringify(body),
  })
}
