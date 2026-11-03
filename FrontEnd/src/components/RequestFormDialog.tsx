import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import type { Persona, PricingException, UserSummary } from '../types'
import { pricingApi } from '../api/pricingExceptions'
import { rateReduction, validGuid } from '../lib/format'
import { useOperationKey } from '../hooks/useOperationKey'
import { ErrorMessage } from './Feedback'
import { Modal } from './Modal'
import { Icon } from './Icon'

export function RequestFormDialog({ persona, request, onClose, onSuccess }: { persona: Persona; request?: PricingException; onClose: () => void; onSuccess: (request: PricingException) => void }) {
  const [application, setApplication] = useState(request?.mortgageApplicationId ?? '')
  const [discount, setDiscount] = useState(String(request?.discountBps ?? ''))
  const [reason, setReason] = useState(request?.reason ?? '')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [reviewerId, setReviewerId] = useState(request?.assignedReviewerId ?? '')
  const [directory, setDirectory] = useState<{ user: UserSummary; reviewers: UserSummary[] } | null>(null)
  useEffect(() => {
    if (request) return
    const controller = new AbortController()
    void Promise.all([pricingApi.me(persona.id, controller.signal), pricingApi.reviewers(persona.id, controller.signal)])
      .then(([user, reviewers]) => {
        if (!controller.signal.aborted) {
          setDirectory({ user, reviewers })
          if (!reviewers.length) setError('No eligible reviewers are available. Please contact your administrator.')
        }
      })
      .catch(error => { if (!controller.signal.aborted) setError((error as Error).message) })
    return () => controller.abort()
  }, [persona.id, request])
  const keyFor = useOperationKey()
  async function submit(event: FormEvent) {
    event.preventDefault()
    if (busy) return
    if (!validGuid(application)) { setError('Enter a valid, non-empty mortgage application ID.'); return }
    if (!request && (!directory || !directory.reviewers.some(user => user.id === reviewerId))) { setError('Select a reviewer from the available reviewers.'); return }
    const bps = Number(discount)
    if (!Number.isInteger(bps) || bps < 1 || bps > 500 || !reason.trim()) { setError('Enter a discount between 1 and 500 basis points and a supporting reason.'); return }
    setBusy(true); setError('')
    try {
      if (request) {
        const payload = { discountBps: bps, reason: reason.trim(), expectedVersion: request.version, expectedRevisionNumber: request.revisionNumber }
        onSuccess(await pricingApi.revise(request.id, payload, persona.id, keyFor(payload)))
      } else {
        const payload = { mortgageApplicationId: application.trim(), discountBps: bps, reason: reason.trim(), reviewerId }
        onSuccess(await pricingApi.create(payload, persona.id, keyFor(payload)))
      }
    } catch (error) { setError((error as Error).message) } finally { setBusy(false) }
  }
  return <Modal title={request ? 'Submit revised terms' : 'Create pricing exception'} subtitle={request ? 'A new revision preserves the original terms and requires a new review.' : 'Propose a rate discount for an existing mortgage application.'} onClose={onClose} busy={busy}><form onSubmit={submit} className="dialog-form"><ErrorMessage message={error}/>
    {request?.status === 'Approved' && <div className="warning-note"><Icon name="alert" size={19}/><span>Submitting this revision withdraws the current approval until the new terms are approved.</span></div>}
    <label className="field"><span>Requester</span><input aria-label="Requester" readOnly value={request?.requesterName ?? directory?.user.displayName ?? 'Loading signed-in user…'}/><small>{request?.requesterId ?? directory?.user.id ?? 'Captured automatically from your signed-in identity.'}</small></label>
    {request ? <label className="field"><span>Assigned reviewer</span><input readOnly value={request.assignedReviewerName}/><small>{request.assignedReviewerId}</small></label> : <label className="field"><span>Assigned reviewer <span className="required">*</span></span><select required disabled={busy || !directory} value={reviewerId} onChange={event => setReviewerId(event.target.value)}><option value="">Select a reviewer</option>{directory?.reviewers.map(user => <option key={user.id} value={user.id}>{user.displayName}</option>)}</select><small>Only the selected independent reviewer can approve or decline this request.</small></label>}
    <label className="field"><span>Mortgage application ID <span className="required">*</span></span><input required disabled={Boolean(request) || busy} value={application} onChange={e => setApplication(e.target.value)} placeholder="e.g. aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" autoFocus/><small>Use the ID from the mortgage application.</small></label>
    <label className="field"><span>Requested discount <span className="required">*</span></span><div className="input-with-unit"><input type="number" min="1" max="500" step="1" required value={discount} disabled={busy} onChange={e => setDiscount(e.target.value)} placeholder="50"/><span>basis points</span></div><small>{Number(discount) > 0 ? rateReduction(Number(discount)) + ' reduction from the standard rate' : 'Between 1 and 500 basis points. 100 bps = 1 percentage point.'}</small></label>
    <label className="field"><span>Supporting reason <span className="required">*</span></span><textarea required maxLength={2000} rows={4} value={reason} disabled={busy} onChange={e => setReason(e.target.value)} placeholder="Explain the business case for this exception…"/><small className="character-count">{reason.length.toLocaleString()} / 2,000 characters</small></label>
    <div className="form-assurance"><Icon name="shield" size={18}/><span>Submitted terms are preserved and reviewed independently.</span></div><div className="modal-actions"><button type="button" className="button secondary" disabled={busy} onClick={onClose}>Cancel</button><button type="submit" className="button primary" disabled={busy}>{busy ? <span className="spinner"/> : <Icon name="arrow" size={17}/>} {busy ? 'Submitting…' : request ? 'Submit revision' : 'Submit request'}</button></div>
  </form></Modal>
}
