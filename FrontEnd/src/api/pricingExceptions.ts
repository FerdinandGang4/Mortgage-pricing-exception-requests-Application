import type { ApprovedDiscount, PricingException, RequestHistory, UserSummary } from '../types'

export class ApiError extends Error {
  readonly status: number
  constructor(message: string, status: number) { super(message); this.name = 'ApiError'; this.status = status }
}
async function request<T>(path: string, actor: string, init: RequestInit = {}): Promise<T> {
  let response: Response
  try {
    response = await fetch('/api/v1' + path, {
      ...init, headers: { 'Content-Type': 'application/json', 'X-User-Id': actor, ...init.headers },
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new ApiError('Unable to connect. Check that the backend is running, then try again.', 0)
  }
  const body = await response.json().catch(() => null)
  if (!response.ok) {
    const fields = body?.errors ? Object.values(body.errors as Record<string, string[]>).flat().join(' ') : ''
    throw new ApiError(fields || body?.detail || body?.title || 'The request could not be completed.', response.status)
  }
  return body as T
}
export const pricingApi = {
  me: (actor: string, signal?: AbortSignal) => request<UserSummary>('/users/me', actor, { signal }),
  reviewers: (actor: string, signal?: AbortSignal) => request<UserSummary[]>('/users/reviewers', actor, { signal }),
  get: (id: string, actor: string, signal?: AbortSignal) => request<PricingException>('/pricing-exceptions/' + id, actor, { signal }),
  history: (id: string, actor: string, signal?: AbortSignal) => request<RequestHistory>('/pricing-exceptions/' + id + '/history', actor, { signal }),
  create: (body: { mortgageApplicationId: string; discountBps: number; reason: string; reviewerId: string }, actor: string, key: string) =>
    request<PricingException>('/pricing-exceptions', actor, { method: 'POST', headers: { 'Idempotency-Key': key }, body: JSON.stringify(body) }),
  revise: (id: string, body: { discountBps: number; reason: string; expectedVersion: number; expectedRevisionNumber: number }, actor: string, key: string) =>
    request<PricingException>('/pricing-exceptions/' + id + '/revisions', actor, { method: 'POST', headers: { 'Idempotency-Key': key }, body: JSON.stringify(body) }),
  review: (id: string, body: { outcome: 'Approved' | 'Declined'; reason: string; expectedVersion: number; reviewedRevisionNumber: number }, actor: string, key: string) =>
    request<PricingException>('/pricing-exceptions/' + id + '/decisions', actor, { method: 'POST', headers: { 'Idempotency-Key': key }, body: JSON.stringify(body) }),
  approved: (id: string, actor: string, signal?: AbortSignal) =>
    request<ApprovedDiscount>('/mortgage-applications/' + id + '/approved-discount', actor, { signal }),
}
export async function checkConnection(signal?: AbortSignal) {
  try { return (await fetch('/openapi/v1.json', { signal })).ok } catch { return false }
}
