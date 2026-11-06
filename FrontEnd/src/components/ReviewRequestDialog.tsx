import { useState } from 'react'
import type { FormEvent } from 'react'
import type { Persona, PricingException } from '../types'
import { pricingApi } from '../api/pricingExceptions'
import { shortId } from '../lib/format'
import { useOperationKey } from '../hooks/useOperationKey'
import { ErrorMessage } from './Feedback'
import { Modal } from './Modal'
import { Icon } from './Icon'

export function ReviewRequestDialog({ request, persona, onClose, onSuccess }: { request: PricingException; persona: Persona; onClose: () => void; onSuccess: (request: PricingException) => void }) {
  const [outcome, setOutcome] = useState<'Approved' | 'Declined'>('Approved')
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const keyFor = useOperationKey()
  async function submit(event: FormEvent) {
    event.preventDefault(); if (busy) return
    setBusy(true); setError('')
    const payload = { outcome, reason: reason.trim(), expectedVersion: request.version, reviewedRevisionNumber: request.revisionNumber }
    try { onSuccess(await pricingApi.review(request.id, payload, persona.id, keyFor(payload))) }
    catch (error) { setError((error as Error).message) } finally { setBusy(false) }
  }
  return <Modal title="Review pricing exception" subtitle={'PE-' + shortId(request.id) + ' · Revision ' + request.revisionNumber} onClose={onClose} busy={busy}><form className="dialog-form" onSubmit={submit}><ErrorMessage message={error}/><div className="review-summary"><span>Terms being reviewed</span><strong>{request.discountBps} <small>basis points</small></strong><p>{request.reason}</p></div><fieldset className="decision-fieldset" disabled={busy}><legend>Your decision</legend><label className={'decision-option ' + (outcome === 'Approved' ? 'chosen approve' : '')}><input type="radio" name="decision" value="Approved" checked={outcome === 'Approved'} onChange={() => setOutcome('Approved')}/><Icon name="check"/><span>Approve<small>Authorize these exact terms</small></span></label><label className={'decision-option ' + (outcome === 'Declined' ? 'chosen decline' : '')}><input type="radio" name="decision" value="Declined" checked={outcome === 'Declined'} onChange={() => setOutcome('Declined')}/><Icon name="close"/><span>Decline<small>Do not authorize this discount</small></span></label></fieldset><label className="field"><span>Review note <small className="optional">Optional</small></span><textarea maxLength={2000} rows={3} value={reason} disabled={busy} onChange={e => setReason(e.target.value)} placeholder="Add context for your decision…"/></label><div className="form-assurance"><Icon name="shield" size={18}/><span>Your decision is final for this revision and will be recorded in history.</span></div><div className="modal-actions"><button type="button" className="button secondary" onClick={onClose} disabled={busy}>Cancel</button><button type="submit" className={'button ' + (outcome === 'Approved' ? 'primary' : 'danger')} disabled={busy}>{busy && <span className="spinner"/>}{busy ? 'Recording…' : outcome === 'Approved' ? 'Confirm approval' : 'Confirm decline'}</button></div></form></Modal>
}
