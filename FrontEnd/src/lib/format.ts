import type { Persona } from '../types'

export const personas: Persona[] = [
  { id: '11111111-1111-1111-1111-111111111111', role: 'RelationshipManager', label: 'James Wilson (Manager)', initials: 'JW' },
  { id: '11111111-1111-1111-1111-111111111112', role: 'RelationshipManager', label: 'Sarah Johnson (Manager)', initials: 'SJ' },
  { id: '11111111-1111-1111-1111-111111111113', role: 'RelationshipManager', label: 'Michael Brown (Manager)', initials: 'MB' },
  { id: '22222222-2222-2222-2222-222222222222', role: 'Reviewer', label: 'David Miller (Reviewer)', initials: 'DM' },
  { id: '22222222-2222-2222-2222-222222222223', role: 'Reviewer', label: 'Emily Davis (Reviewer)', initials: 'ED' },
  { id: '22222222-2222-2222-2222-222222222224', role: 'Reviewer', label: 'Robert Taylor (Reviewer)', initials: 'RT' },
  { id: '22222222-2222-2222-2222-222222222225', role: 'Reviewer', label: 'Jennifer Anderson (Reviewer)', initials: 'JA' },
  { id: '22222222-2222-2222-2222-222222222226', role: 'Reviewer', label: 'Daniel Thomas (Reviewer)', initials: 'DT' },
  { id: '33333333-3333-3333-3333-333333333333', role: 'MortgageProcess', label: 'Mortgage process', initials: 'MP' },
]
// Normal production builds omit demo switching and never send the demo identity header.
// The local Docker demo explicitly opts in; the API still rejects these identities outside Development.
export const demoUsersEnabled = import.meta.env.DEV || import.meta.env.VITE_ENABLE_DEMO_USERS === 'true'
export const shortId = (id: string) => id.slice(0, 8).toUpperCase()
export const rateReduction = (bps: number) => (bps / 100).toFixed(2) + '%'
export const validGuid = (value: string) => /^(?!00000000-0000-0000-0000-000000000000$)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value.trim())
export const actorName = (id: string) => personas.find(p => p.id === id)?.label ?? shortId(id)
export const dateTime = (value: string) => new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
