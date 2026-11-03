import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import type { Persona, PricingException } from '../types'
import { pricingApi } from '../api/pricingExceptions'
import { shortId } from '../lib/format'
import { Modal } from './Modal'
import { ErrorMessage } from './Feedback'

export function OpenRequestDialog({ persona, onClose, onSuccess }: { persona: Persona; onClose: () => void; onSuccess: (request: PricingException) => void }) {
  const [id, setId] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [attempt, setAttempt] = useState(0)
  const [directory, setDirectory] = useState<{ actor: string; attempt: number; requests: PricingException[]; error: string } | null>(null)
  useEffect(() => {
    const controller = new AbortController()
    void pricingApi.list(persona.id, controller.signal).then(requests => {
      if (!controller.signal.aborted) setDirectory({ actor: persona.id, attempt, requests, error: '' })
    }).catch(error => {
      if (!controller.signal.aborted) setDirectory({ actor: persona.id, attempt, requests: [], error: (error as Error).message })
    })
    return () => controller.abort()
  }, [persona.id, attempt])
  const loading = !directory || directory.actor !== persona.id || directory.attempt !== attempt
  const requests = loading ? [] : directory.requests
  const loadError = loading ? '' : directory.error
  const selected = requests.find(request => request.id === id)

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (busy || loading) return
    if (!selected) { setError('Select an existing request to open.'); return }
    setBusy(true); setError('')
    try { onSuccess(await pricingApi.get(selected.id, persona.id)) }
    catch (error) { setError((error as Error).message) }
    finally { setBusy(false) }
  }

  return <Modal title="Open an existing request" subtitle="Choose from all existing pricing exception requests." onClose={onClose} busy={busy}>
    <form className="dialog-form" onSubmit={submit}>
      <ErrorMessage message={error || loadError}/>
      {loadError && <button type="button" className="text-button" onClick={() => { setError(''); setAttempt(value => value + 1) }}>Retry loading requests</button>}
      <label className="field">
        <span>Existing request</span>
        <select aria-label="Existing request" autoFocus required value={id} disabled={busy || loading || Boolean(loadError) || requests.length === 0} onChange={event => { setId(event.target.value); setError('') }}>
          <option value="">{loading ? 'Loading requests…' : requests.length ? 'Select a request' : 'No existing requests'}</option>
          {requests.map(request => <option key={request.id} value={request.id}>PE-{shortId(request.id)} · {request.requesterName} · {request.discountBps} bps · {request.status}</option>)}
        </select>
        {!loading && !loadError && <small>{requests.length ? requests.length + ' existing requests available.' : 'Create a pricing exception first, then return here to open it.'}</small>}
      </label>
      {selected && <div className="review-summary">
        <span>Selected request</span>
        <p><b>Request ID:</b> {selected.id}</p>
        <p><b>Application:</b> {selected.mortgageApplicationId}</p>
        <p><b>Requester:</b> {selected.requesterName} · <b>Reviewer:</b> {selected.assignedReviewerName}</p>
        <p>{selected.reason}</p>
      </div>}
      <div className="modal-actions">
        <button type="button" className="button secondary" disabled={busy} onClick={onClose}>Cancel</button>
        <button className="button primary" type="submit" disabled={busy || loading || !selected}>{busy ? 'Opening…' : 'Open request'}</button>
      </div>
    </form>
  </Modal>
}
