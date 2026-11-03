import { useState } from 'react'
import type { FormEvent } from 'react'
import type { ApprovedDiscount, Persona } from '../types'
import { pricingApi } from '../api/pricingExceptions'
import { dateTime, rateReduction, shortId, validGuid } from '../lib/format'
import { ErrorMessage } from './Feedback'
import { Icon } from './Icon'
export function ApprovedDiscountLookup({ persona, onOpen }: { persona: Persona; onOpen: (id: string) => void }) {
  const [id, setId] = useState(''); const [busy, setBusy] = useState(false); const [error, setError] = useState(''); const [result, setResult] = useState<ApprovedDiscount | null>(null)
  async function submit(event: FormEvent) {
    event.preventDefault(); if (busy) return
    if (!validGuid(id)) { setError('Enter a valid mortgage application ID.'); return }
    setBusy(true); setError(''); setResult(null)
    try { setResult(await pricingApi.approved(id.trim(), persona.id)) } catch (error) { setError((error as Error).message) } finally { setBusy(false) }
  }
  return <section className="panel lookup-panel"><div className="lookup-intro"><div className="lookup-icon"><Icon name="shield" size={29}/></div><span className="eyebrow">MORTGAGE PROCESSING</span><h2>Only approved terms.<br/>Ready for the next step.</h2><p>Find the currently approved discount for a mortgage application. Previous approvals never replace a pending revision.</p><div className="lookup-principle"><Icon name="check" size={17}/>Current revision only</div><div className="lookup-principle"><Icon name="check" size={17}/>Reviewer and time recorded</div></div><div className="lookup-form"><h3>Look up an approved discount</h3><p>Enter the mortgage application reference.</p>{persona.role !== 'MortgageProcess' ? <div className="info-note"><Icon name="shield" size={20}/><span>Switch the demo role to <strong>Mortgage process</strong> to access approved discounts.</span></div> : <form onSubmit={submit}><label className="field"><span>Mortgage application ID</span><input required value={id} disabled={busy} onChange={e => { setId(e.target.value); setResult(null); setError('') }} placeholder="Enter the full application ID"/></label><button className="button primary" type="submit" disabled={busy}>{busy ? <span className="spinner"/> : <Icon name="search" size={17}/>} {busy ? 'Looking up…' : 'Find approved discount'}</button><ErrorMessage message={error}/></form>}
    {result && persona.role === 'MortgageProcess' && <div className="approved-result" aria-live="polite"><div className="approved-result-heading"><span className="status-badge approved"><span/>Currently approved</span><span>Revision {result.revisionNumber}</span></div><strong>{result.discountBps}<small> bps</small></strong><p>{rateReduction(result.discountBps)} reduction from the standard rate</p><dl><div><dt>Approved by</dt><dd>{result.reviewerName}</dd></div><div><dt>Decision recorded</dt><dd>{dateTime(result.decidedAtUtc)}</dd></div><div><dt>Request reference</dt><dd>PE-{shortId(result.requestId)}</dd></div></dl><button className="text-button" onClick={() => onOpen(result.requestId)}>View request and history <Icon name="arrow" size={16}/></button></div>}</div></section>
}
