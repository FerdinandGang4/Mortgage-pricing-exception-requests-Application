import { useState } from 'react'
import type { FormEvent } from 'react'
import type { Persona, PricingException } from '../types'
import { pricingApi } from '../api/pricingExceptions'
import { validGuid } from '../lib/format'
import { Modal } from './Modal'
import { ErrorMessage } from './Feedback'
export function OpenRequestDialog({ persona, onClose, onSuccess }: { persona: Persona; onClose: () => void; onSuccess: (request: PricingException) => void }) {
  const [id, setId] = useState(''); const [busy, setBusy] = useState(false); const [error, setError] = useState('')
  async function submit(event: FormEvent) {
    event.preventDefault(); if (busy) return
    if (!validGuid(id)) { setError('Enter the full request ID.'); return }
    setBusy(true); setError('')
    try { onSuccess(await pricingApi.get(id.trim(), persona.id)) } catch (error) { setError((error as Error).message) } finally { setBusy(false) }
  }
  return <Modal title="Open an existing request" subtitle="Use a shared request ID to bring it into your workspace." onClose={onClose} busy={busy}><form className="dialog-form" onSubmit={submit}><ErrorMessage message={error}/><label className="field"><span>Request ID</span><input autoFocus required value={id} disabled={busy} onChange={e => setId(e.target.value)} placeholder="Paste the full request ID"/></label><div className="modal-actions"><button type="button" className="button secondary" disabled={busy} onClick={onClose}>Cancel</button><button className="button primary" type="submit" disabled={busy}>{busy ? 'Opening…' : 'Open request'}</button></div></form></Modal>
}
