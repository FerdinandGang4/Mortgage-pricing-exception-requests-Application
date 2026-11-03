import type { Persona } from '../types'

export const personas: Persona[] = [
  { id: '11111111-1111-1111-1111-111111111111', role: 'RelationshipManager', label: 'Relationship manager', initials: 'RM' },
  { id: '22222222-2222-2222-2222-222222222222', role: 'Reviewer', label: 'Independent reviewer', initials: 'IR' },
  { id: '33333333-3333-3333-3333-333333333333', role: 'MortgageProcess', label: 'Mortgage process', initials: 'MP' },
]
export const shortId = (id: string) => id.slice(0, 8).toUpperCase()
export const rateReduction = (bps: number) => (bps / 100).toFixed(2) + '%'
export const validGuid = (value: string) => /^(?!00000000-0000-0000-0000-000000000000$)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value.trim())
export const actorName = (id: string) => personas.find(p => p.id === id)?.label ?? shortId(id)
export const dateTime = (value: string) => new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
