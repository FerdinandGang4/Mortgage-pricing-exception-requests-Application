import type { RequestHistory } from '../types'
import { actorName, dateTime } from '../lib/format'
import { Icon } from './Icon'

export function HistoryTimeline({ history, requesterId, requesterName }: { history: RequestHistory; requesterId: string; requesterName: string }) {
  return <ol className="timeline history-events" aria-label="Request history">{[...history.events].sort((a, b) => b.sequence - a.sequence).map(event => {
    const revision = history.revisions.find(r => r.revisionNumber === event.revisionNumber)
    const decision = history.decisions.find(d => d.revisionNumber === event.revisionNumber)
    const isDecision = event.action === 'Approved' || event.action === 'Declined'
    const actor = event.actorUserId === requesterId ? requesterName : decision?.reviewerId === event.actorUserId ? decision.reviewerName : actorName(event.actorUserId)
    const title = event.action === 'Created' ? 'Request submitted' : event.action === 'Revised' ? 'New revision submitted' : 'Request ' + event.action.toLowerCase()

    return <li key={event.sequence}>
      <span className={'timeline-node ' + event.action.toLowerCase()}><Icon name={event.action === 'Approved' ? 'check' : event.action === 'Declined' ? 'close' : 'file'} size={16}/></span>
      <div className="timeline-event">
        <div className="timeline-heading"><strong>{title}</strong><span className="revision-badge">Revision {event.revisionNumber}</span></div>
        <p className="timeline-meta">{actor} {'\u00b7'} <time dateTime={event.occurredAtUtc}>{dateTime(event.occurredAtUtc)}</time></p>
        {isDecision ? decision?.reason && <div className="timeline-decision"><h3>Decision reason</h3><p className="decision-note">{'\u201c'}{decision.reason}{'\u201d'}</p></div> : revision && <div className="timeline-summary">
          <p>Requested discount: {revision.discountBps} bps</p>
          <p>Reason: {revision.reason}</p>
        </div>}
      </div>
    </li>
  })}</ol>
}
