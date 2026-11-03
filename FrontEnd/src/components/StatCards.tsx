import type { PricingException } from '../types'
import { Icon } from './Icon'
export function StatCards({ requests }: { requests: PricingException[] }) {
  const stats = [
    { label: 'Requests in workspace', value: requests.length, icon: 'requests' as const, caption: 'Created or opened here', color: 'blue' },
    { label: 'Awaiting review', value: requests.filter(r => r.status === 'Pending').length, icon: 'clock' as const, caption: 'Ready for a decision', color: 'amber' },
    { label: 'Currently approved', value: requests.filter(r => r.status === 'Approved').length, icon: 'check' as const, caption: 'Available to mortgage processing', color: 'green' },
    { label: 'Declined', value: requests.filter(r => r.status === 'Declined').length, icon: 'close' as const, caption: 'Retained in request history', color: 'slate' },
  ]
  return <section className="stats-grid" aria-label="Workspace statistics">{stats.map(stat => <article className="stat-card" key={stat.label}><div className="stat-top"><span>{stat.label}</span><span className={'stat-icon ' + stat.color}><Icon name={stat.icon} size={18}/></span></div><strong className="stat-value">{stat.value.toString().padStart(2, '0')}</strong><p>{stat.caption}</p></article>)}</section>
}
