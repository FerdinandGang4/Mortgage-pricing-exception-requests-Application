import { useEffect, useState } from 'react'
import type { Persona, PricingException, RequestHistory } from '../types'
import { pricingApi } from '../api/pricingExceptions'
import { actorName, dateTime, rateReduction, shortId } from '../lib/format'
import { ErrorMessage, EmptyState } from './Feedback'
import { HistoryTimeline } from './HistoryTimeline'
import { Icon } from './Icon'
import { StatusBadge } from './StatusBadge'

export function RequestDetails({ request, persona, onRevise, onReview, onCopy, historyOnly = false }: { request: PricingException | null; persona: Persona; onRevise: (request: PricingException) => void; onReview: (request: PricingException) => void; onCopy: (id: string) => void; historyOnly?: boolean }) {
  const [history, setHistory] = useState<RequestHistory | null>(null)
  const [error, setError] = useState('')
  const [tab, setTab] = useState<'details' | 'history'>('details')
  useEffect(() => {
    setHistory(null); setError('')
    if (!request) return
    const controller = new AbortController()
    void pricingApi.history(request.id, persona.id, controller.signal).then(setHistory).catch(error => { if (!controller.signal.aborted) setError((error as Error).message) })
    return () => controller.abort()
  }, [request, persona.id])
  if (!request) return <section className="panel detail-panel"><div className="panel-heading"><h2>Request details</h2><Icon name="file" size={19}/></div><EmptyState title="The detail is in the decision">Select a request to inspect its terms, review its history, or take the next step.</EmptyState><div className="detail-assurance"><Icon name="shield" size={18}/><p>Approvals are always tied to the exact terms reviewed.</p></div></section>
  const own = persona.id === request.createdByUserId
  return <section className={'panel detail-panel ' + (historyOnly ? 'history-wide' : '')}>
    <div className="panel-heading"><div><span className="eyebrow">PRICING EXCEPTION</span><h2>PE-{shortId(request.id)}</h2></div><button className="icon-button" aria-label="Copy request ID" title="Copy full request ID" onClick={() => onCopy(request.id)}><Icon name="copy" size={17}/></button></div>
    {!historyOnly && <div className="detail-tabs"><button onClick={() => setTab('details')} className={tab === 'details' ? 'selected' : ''}>Details</button><button onClick={() => setTab('history')} className={tab === 'history' ? 'selected' : ''}>History <span>{history?.events.length ?? '—'}</span></button></div>}
    {historyOnly || tab === 'history' ? <div className="history-content"><ErrorMessage message={error}/>{history ? <HistoryTimeline history={history}/> : !error && <p className="muted" role="status">Loading request history…</p>}</div> : <><div className="detail-content"><div className="detail-status"><StatusBadge status={request.status}/><span className="muted">Revision {request.revisionNumber}</span></div><div className="discount-figure"><span>Requested discount</span><div><strong>{request.discountBps}</strong><span>basis points</span></div><p>{rateReduction(request.discountBps)} reduction from the standard rate</p></div><dl className="detail-fields"><div><dt>Mortgage application</dt><dd className="mono" title={request.mortgageApplicationId}>{request.mortgageApplicationId}</dd></div><div><dt>Submitted by</dt><dd>{actorName(request.createdByUserId)}</dd></div><div><dt>Supporting reason</dt><dd className="reason-text">{request.reason}</dd></div>{request.decidedAtUtc && <><div><dt>Reviewed by</dt><dd>{actorName(request.reviewerId!)}</dd></div><div><dt>Decision recorded</dt><dd>{dateTime(request.decidedAtUtc)}</dd></div></>}</dl></div><div className="detail-actions">
      {persona.role === 'Reviewer' && request.status === 'Pending' && !own && <button className="button primary full-width" onClick={() => onReview(request)}><Icon name="shield" size={17}/>Review request</button>}
      {persona.role === 'RelationshipManager' && own && <button className="button secondary full-width" onClick={() => onRevise(request)}><Icon name="file" size={17}/>Submit a revision</button>}
      {request.status === 'Pending' && persona.role !== 'Reviewer' && <p className="action-note"><Icon name="clock" size={15}/>Awaiting an independent reviewer</p>}
      {request.status === 'Approved' && <p className="action-note approved-note"><Icon name="check" size={15}/>Current terms approved for processing</p>}
      <p className="detail-footnote">Previous terms and decisions are preserved in history.</p></div></>}
  </section>
}
