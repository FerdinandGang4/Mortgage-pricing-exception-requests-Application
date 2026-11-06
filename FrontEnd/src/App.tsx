import { useRef, useState } from 'react'
import type { Persona, PricingException, View } from './types'
import { personas, shortId } from './lib/format'
import { pricingApi } from './api/pricingExceptions'
import { useRequestWorkspace } from './hooks/useRequestWorkspace'
import { useTheme } from './hooks/useTheme'
import { Sidebar } from './components/Sidebar'
import { Header } from './components/Header'
import { Footer } from './components/Footer'
import { RequestSummary } from './components/RequestSummary'
import { PricingExceptionTable } from './components/PricingExceptionTable'
import { RequestDetails } from './components/RequestDetails'
import { RequestFormDialog } from './components/RequestFormDialog'
import { ReviewRequestDialog } from './components/ReviewRequestDialog'
import { RequestDetailsDialog } from './components/RequestDetailsDialog'
import { ApprovedDiscountLookup } from './components/ApprovedDiscountLookup'
import { ErrorMessage } from './components/Feedback'
import { Icon } from './components/Icon'
import './App.css'
import './chrome.css'

type Dialog = { kind: 'create' | 'open' } | { kind: 'revise' | 'review'; request: PricingException } | null
function App() {
  const { theme, toggleTheme } = useTheme()
  const [year] = useState(() => new Date().getFullYear())
  const [persona, setPersona] = useState<Persona>(personas[0])
  const activePersonaId = useRef(personas[0].id)
  const [view, setView] = useState<View>('requests')
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [dialog, setDialog] = useState<Dialog>(null)
  const [menuOpen, setMenuOpen] = useState(false)
  const [toast, setToast] = useState('')
  const [actionError, setActionError] = useState('')
  const workspace = useRequestWorkspace(persona.id)
  const selected = workspace.requests.find(r => r.id === selectedId) ?? null
  function success(request: PricingException) {
    // An operation started by the previous demo user must not replace the new workspace.
    if (activePersonaId.current !== persona.id) return
    workspace.remember(request); setSelectedId(request.id); setDialog(null); setView('requests')
    setToast('PE-' + shortId(request.id) + ' is up to date.')
  }
  async function openById(id: string) {
    setActionError(''); setSelectedId(id); setView('requests')
    try { success(await pricingApi.get(id, persona.id)) } catch (error) {
      if (activePersonaId.current === persona.id) setActionError((error as Error).message)
    }
  }
  async function copy(id: string) {
    try { await navigator.clipboard.writeText(id); setToast('Request ID copied. Share it with your reviewer.') }
    catch { setActionError('Copy was unavailable. The full request ID is ' + id) }
  }
  const heading = view === 'requests' ? 'Mortgage Pricing Exceptions' : view === 'activity' ? 'Request history' : 'Approved discounts'
  return <div className="app-shell">
    <Sidebar view={view} onView={setView} pending={workspace.requests.filter(r => r.status === 'Pending').length} open={menuOpen} onClose={() => setMenuOpen(false)}/>
    <div className="main-shell"><Header theme={theme} onToggleTheme={toggleTheme} persona={persona} connected={workspace.connected} onPersona={id => { activePersonaId.current = id; setPersona(personas.find(p => p.id === id)!); setDialog(null); setToast(''); setActionError('') }} onMenu={() => setMenuOpen(true)}/>
      <main className="main-content"><div className="page-heading"><div><p className="eyebrow">MORTGAGE LENDING</p><h1>{heading}</h1><p>{view === 'requests' ? 'Thoughtful exceptions. Confident decisions.' : view === 'activity' ? 'Every revision and decision, in the order it happened.' : 'Verified discounts for the mortgage application process.'}</p></div><div className="page-actions"><button className="button secondary" onClick={() => setDialog({ kind: 'open' })}><Icon name="search" size={17}/>Find Request</button>{persona.role === 'RelationshipManager' && <button className="button primary" onClick={() => setDialog({ kind: 'create' })}><Icon name="plus" size={18}/>Create Request</button>}</div></div>
        <ErrorMessage message={workspace.error || actionError}/>
        {toast && <div className="toast" role="status"><Icon name="check" size={18}/><span>{toast}</span><button aria-label="Dismiss notification" className="icon-button" onClick={() => setToast('')}><Icon name="close" size={16}/></button></div>}
        {view === 'discounts' ? <ApprovedDiscountLookup key={persona.id} persona={persona} onOpen={id => void openById(id)}/> : <>
          <RequestSummary requests={workspace.requests}/>
          <div className="section-caption"><div><span className="section-dot"/><span>Request Activity</span><span className="caption-divider">/</span><span>{workspace.requests.length} requests tracked</span></div><button className="text-button" disabled={workspace.loading} onClick={() => void workspace.refresh()}><Icon name="refresh" size={15} className={workspace.loading ? 'spinning' : ''}/>{workspace.loading ? 'Refreshing…' : 'Refresh'}</button></div>
          {view === 'activity' && <div className="activity-selector"><label htmlFor="history-request">Request history</label><select id="history-request" value={selectedId ?? ''} onChange={e => setSelectedId(e.target.value || null)}><option value="">Select a request</option>{workspace.requests.map(r => <option key={r.id} value={r.id}>PE-{shortId(r.id)} · {r.discountBps} bps · {r.status}</option>)}</select></div>}
          <div className={view === 'requests' ? 'workspace-grid' : 'history-layout'}>{view === 'requests' && <PricingExceptionTable requests={workspace.requests} selectedId={selectedId} loading={workspace.loading} onSelect={r => setSelectedId(r.id)} onOpen={() => setDialog({ kind: 'open' })}/>}<RequestDetails request={selected} persona={persona} onRevise={request => setDialog({ kind: 'revise', request })} onReview={request => setDialog({ kind: 'review', request })} onCopy={id => void copy(id)} historyOnly={view === 'activity'}/></div>
          <div className="workspace-note"><Icon name="shield" size={17}/><p>Changes create a new revision. An approval always belongs to the terms that were reviewed.</p><span>CONTROLLED BY DESIGN</span></div>
        </>}
        <Footer year={year}/>
      </main>
    </div>
    {dialog?.kind === 'create' && <RequestFormDialog persona={persona} onClose={() => setDialog(null)} onSuccess={success}/>}
    {dialog?.kind === 'revise' && <RequestFormDialog persona={persona} request={dialog.request} onClose={() => setDialog(null)} onSuccess={success}/>}
    {dialog?.kind === 'review' && <ReviewRequestDialog persona={persona} request={dialog.request} onClose={() => setDialog(null)} onSuccess={success}/>}
    {dialog?.kind === 'open' && <RequestDetailsDialog persona={persona} onClose={() => setDialog(null)} onSuccess={success}/>}
  </div>
}
export default App
