import type { RequestHistory as RequestHistoryData } from '../types'
import { actorName, dateTime } from '../lib/format'
import { Icon } from './Icon'

export function RequestHistory({ history, requesterId, requesterName }: { history: RequestHistoryData; requesterId: string; requesterName: string }) {
  return <ol className="timeline history-events" aria-label="Request history">{[...history.events].sort((leftHistoryEntry, rightHistoryEntry) => rightHistoryEntry.sequence - leftHistoryEntry.sequence).map(event => {
    const revision = history.revisions.find(requestRevision => requestRevision.revisionNumber === event.revisionNumber)
    const decision = history.decisions.find(reviewDecision => reviewDecision.revisionNumber === event.revisionNumber)
    const isDecision = event.action === 'Approved' || event.action === 'Declined'
    const historyActorName = event.actorUserId === requesterId ? requesterName : decision?.reviewerId === event.actorUserId ? decision.reviewerName : actorName(event.actorUserId)
    const historyEventTitle = event.action === 'Created' ? 'Request submitted' : event.action === 'Revised' ? 'New revision submitted' : 'Request ' + event.action.toLowerCase()

    return <li key={event.sequence}>
      <span className={'timeline-node ' + event.action.toLowerCase()}><Icon name={event.action === 'Approved' ? 'check' : event.action === 'Declined' ? 'close' : 'file'} size={16}/></span>
      <div className="timeline-event">
        <div className="timeline-heading"><strong>{historyEventTitle}</strong><span className="revision-badge">Revision {event.revisionNumber}</span></div>
        <p className="timeline-meta">{historyActorName} {'\u00b7'} <time dateTime={event.occurredAtUtc}>{dateTime(event.occurredAtUtc)}</time></p>
        {isDecision ? decision?.reason && <div className="timeline-decision"><h3>Decision reason</h3><p className="decision-note">{'\u201c'}{decision.reason}{'\u201d'}</p></div> : revision && <div className="timeline-summary">
          <p>Requested discount: {revision.discountBps} bps</p>
          <p>Reason: {revision.reason}</p>
        </div>}
      </div>
    </li>
  })}</ol>
}
