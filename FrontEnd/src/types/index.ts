export type RequestStatus = 'Pending' | 'Approved' | 'Declined'
export type UserRole = 'RelationshipManager' | 'Reviewer' | 'MortgageProcess'
export interface Persona { id: string; role: UserRole; label: string; initials: string }
export interface UserSummary { id: string; displayName: string; role: UserRole }
export interface PricingException {
  id: string; mortgageApplicationId: string; createdByUserId: string
  status: RequestStatus; revisionNumber: number; version: number
  discountBps: number; reason: string; reviewerId: string | null; decidedAtUtc: string | null
  requesterId: string; requesterName: string; assignedReviewerId: string; assignedReviewerName: string; reviewerName: string | null
}
export interface HistoryEntry { sequence: number; revisionNumber: number; action: string; actorUserId: string; occurredAtUtc: string }
export interface Revision { revisionNumber: number; discountBps: number; reason: string; submittedByUserId: string; submittedAtUtc: string }
export interface Decision { revisionNumber: number; outcome: 'Approved' | 'Declined'; reason: string | null; reviewerId: string; decidedAtUtc: string; reviewerName: string }
export interface RequestHistory { events: HistoryEntry[]; revisions: Revision[]; decisions: Decision[] }
export interface ApprovedDiscount { requestId: string; mortgageApplicationId: string; discountBps: number; revisionNumber: number; reviewerId: string; decidedAtUtc: string; reviewerName: string }
export type View = 'requests' | 'activity' | 'discounts'
