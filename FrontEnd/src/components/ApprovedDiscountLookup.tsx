import { useState } from 'react'
import type { FormEvent } from 'react'
import type { ApprovedDiscount, Persona } from '../types'
import { pricingApi } from '../api/pricingExceptions'
import { dateTime, rateReduction, shortId, validGuid } from '../lib/format'
import { ErrorMessage } from './Feedback'
import { Icon } from './Icon'
export function ApprovedDiscountLookup({ persona, onOpen }: { persona: Persona; onOpen: (requestId: string) => void }) {
  const [mortgageApplicationId, setMortgageApplicationId] = useState(''); const [isLookingUpDiscount, setIsLookingUpDiscount] = useState(false); const [error, setError] = useState(''); const [approvedDiscount, setApprovedDiscount] = useState<ApprovedDiscount | null>(null)
  async function submit(event: FormEvent) {
    event.preventDefault(); if (isLookingUpDiscount) return
    if (!validGuid(mortgageApplicationId)) { setError('Enter a valid mortgage application ID.'); return }
    setIsLookingUpDiscount(true); setError(''); setApprovedDiscount(null)
    try { setApprovedDiscount(await pricingApi.approved(mortgageApplicationId.trim(), persona.id)) } catch (error) { setError((error as Error).message) } finally { setIsLookingUpDiscount(false) }
  }
  return <section className="panel lookup-panel"><div className="lookup-intro"><div className="lookup-icon"><Icon name="shield" size={29}/></div><span className="eyebrow">MORTGAGE PROCESSING</span><h2>Only approved terms.<br/>Ready for the next step.</h2><p>Find the currently approved discount for a mortgage application. Previous approvals never replace a pending revision.</p><div className="lookup-principle"><Icon name="check" size={17}/>Current revision only</div><div className="lookup-principle"><Icon name="check" size={17}/>Reviewer and time recorded</div></div><div className="lookup-form"><h3>Look up an approved discount</h3><p>Enter the mortgage application reference.</p>{persona.role !== 'MortgageProcess' ? <div className="info-note"><Icon name="shield" size={20}/><span>Switch the demo role to <strong>Mortgage process</strong> to access approved discounts.</span></div> : <form onSubmit={submit}><label className="field"><span>Mortgage application ID</span><input required value={mortgageApplicationId} disabled={isLookingUpDiscount} onChange={inputEvent => { setMortgageApplicationId(inputEvent.target.value); setApprovedDiscount(null); setError('') }} placeholder="Enter the full application ID"/></label><button className="button primary" type="submit" disabled={isLookingUpDiscount}>{isLookingUpDiscount ? <span className="spinner"/> : <Icon name="search" size={17}/>} {isLookingUpDiscount ? 'Looking up…' : 'Find approved discount'}</button><ErrorMessage message={error}/></form>}
    {approvedDiscount && persona.role === 'MortgageProcess' && <div className="approved-result" aria-live="polite"><div className="approved-result-heading"><span className="status-badge approved"><span/>Currently approved</span><span>Revision {approvedDiscount.revisionNumber}</span></div><strong>{approvedDiscount.discountBps}<small> bps</small></strong><p>{rateReduction(approvedDiscount.discountBps)} reduction from the standard rate</p><dl><div><dt>Approved by</dt><dd>{approvedDiscount.reviewerName}</dd></div><div><dt>Decision recorded</dt><dd>{dateTime(approvedDiscount.decidedAtUtc)}</dd></div><div><dt>Request reference</dt><dd>PE-{shortId(approvedDiscount.requestId)}</dd></div></dl><button className="text-button" onClick={() => onOpen(approvedDiscount.requestId)}>View request and history <Icon name="arrow" size={16}/></button></div>}</div></section>
}
