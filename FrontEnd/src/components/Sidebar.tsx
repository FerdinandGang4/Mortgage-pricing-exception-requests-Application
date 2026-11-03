import type { View } from '../types'
import { Icon } from './Icon'

export function Sidebar({ view, onView, pending, open, onClose }: { view: View; onView: (view: View) => void; pending: number; open: boolean; onClose: () => void }) {
  const [mobile, setMobile] = useState(() => window.matchMedia('(max-width: 760px)').matches)
  useEffect(() => {
    const media = window.matchMedia('(max-width: 760px)')
    const update = () => setMobile(media.matches)
    media.addEventListener('change', update)
    return () => media.removeEventListener('change', update)
  }, [])
  useEffect(() => {
    if (!open) return
    const escape = (event: KeyboardEvent) => { if (event.key === 'Escape') onClose() }
    document.addEventListener('keydown', escape)
    return () => document.removeEventListener('keydown', escape)
  }, [open, onClose])
  const navigate = (next: View) => { onView(next); onClose() }
  return <>
    {open && <button className="sidebar-scrim" aria-label="Close navigation" onClick={onClose}/>}
    <aside className={'sidebar ' + (open ? 'is-open' : '')} inert={mobile && !open}>
      <a className="brand" href="#" onClick={event => { event.preventDefault(); navigate('requests') }}>
        <span className="brand-mark"><span/><span/><span/></span><span>blue harvest<span className="brand-subtitle">LENDING WORKSPACE</span></span>
      </a>
      <div className="workspace-label"><span className="workspace-symbol">BH</span><div>Mortgage operations<small>Internal workspace</small></div><span className="workspace-dot"/></div>
      <p className="nav-label">WORKSPACE</p>
      <nav aria-label="Main navigation">
        <button className={view === 'requests' ? 'nav-item active' : 'nav-item'} onClick={() => navigate('requests')} aria-current={view === 'requests' ? 'page' : undefined}><Icon name="requests"/><span>Pricing exceptions</span>{pending > 0 && <span className="nav-count">{pending}</span>}</button>
        <button className={view === 'discounts' ? 'nav-item active' : 'nav-item'} onClick={() => navigate('discounts')} aria-current={view === 'discounts' ? 'page' : undefined}><Icon name="discount"/><span>Approved discounts</span></button>
        <button className={view === 'activity' ? 'nav-item active' : 'nav-item'} onClick={() => navigate('activity')} aria-current={view === 'activity' ? 'page' : undefined}><Icon name="activity"/><span>Request history</span></button>
      </nav>
      <div className="sidebar-bottom"><div className="control-note"><Icon name="shield" size={23}/><strong>Confidence in every decision.</strong><p>Reviewed terms. Clear ownership.<br/>A complete approval trail.</p></div><div className="sidebar-footer"><span className="live-dot"/>Development workspace<span>v1.0</span></div></div>
    </aside>
  </>
}
import { useEffect, useState } from 'react'
