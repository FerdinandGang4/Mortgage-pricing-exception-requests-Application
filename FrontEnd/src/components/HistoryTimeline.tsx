import type { RequestHistory } from '../types'
import { actorName, dateTime } from '../lib/format'
import { Icon } from './Icon'
export function HistoryTimeline({ history, requesterId, requesterName }: { history: RequestHistory; requesterId: string; requesterName: string }) {
  return <ol className="timeline">{[...history.events].reverse().map(event => {
    const revision = history.revisions.find(r => r.revisionNumber === event.revisionNumber)
    const decision = history.decisions.find(d => d.revisionNumber === event.revisionNumber)
    return <li key={event.sequence}><span className={'timeline-node ' + event.action.toLowerCase()}><Icon name={event.action === 'Approved' ? 'check' : event.action === 'Declined' ? 'close' : 'file'} size={14}/></span><div><div className="timeline-heading"><strong>{event.action === 'Created' ? 'Request submitted' : event.action === 'Revised' ? 'New revision submitted' : 'Request ' + event.action.toLowerCase()}</strong><span>Revision {event.revisionNumber}</span></div><p>{event.actorUserId === requesterId ? requesterName : decision?.reviewerId === event.actorUserId ? decision.reviewerName : actorName(event.actorUserId)} · {dateTime(event.occurredAtUtc)}</p>{revision && <div className="timeline-terms"><strong>{revision.discountBps} bps</strong><span>{revision.reason}</span></div>}{decision?.reason && (event.action === 'Approved' || event.action === 'Declined') && <p className="decision-note">“{decision.reason}”</p>}</div></li>
  })}</ol>
}
