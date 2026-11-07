import type { PricingException } from '../types'
import { Icon } from './Icon'
export function RequestSummary({ requests }: { requests: PricingException[] }) {
  const requestStatistics = [
    { label: 'Total Requests', value: requests.length, icon: 'requests' as const, caption: 'Created or opened here', color: 'blue' },
    { label: 'Pending Approval', value: requests.filter(pricingExceptionRequest => pricingExceptionRequest.status === 'Pending').length, icon: 'clock' as const, caption: 'Ready for a decision', color: 'amber' },
    { label: 'Approved Requests', value: requests.filter(pricingExceptionRequest => pricingExceptionRequest.status === 'Approved').length, icon: 'check' as const, caption: 'Available to mortgage processing', color: 'green' },
    { label: 'Declined Requests', value: requests.filter(pricingExceptionRequest => pricingExceptionRequest.status === 'Declined').length, icon: 'close' as const, caption: 'Retained in request history', color: 'slate' },
  ]
  return <section className="stats-grid" aria-label="Workspace statistics">{requestStatistics.map(requestStatistic => <article className="stat-card" key={requestStatistic.label}><div className="stat-top"><span>{requestStatistic.label}</span><span className={'stat-icon ' + requestStatistic.color}><Icon name={requestStatistic.icon} size={18}/></span></div><strong className="stat-value">{requestStatistic.value.toString().padStart(2, '0')}</strong><p>{requestStatistic.caption}</p></article>)}</section>
}
