import type { Persona } from '../types'
import { personas } from '../lib/format'
import { Icon } from './Icon'
export function Header({ persona, onPersona, connected, onMenu }: { persona: Persona; onPersona: (id: string) => void; connected: boolean; onMenu: () => void }) {
  return <header className="topbar"><div className="breadcrumbs"><button className="icon-button mobile-menu" aria-label="Open navigation" onClick={onMenu}><Icon name="menu"/></button><span>Mortgage operations</span><span className="breadcrumb-divider">/</span><strong>Exception management</strong></div><div className="header-actions"><span className={'connection ' + (connected ? 'online' : '')}><span/>{connected ? 'API connected' : 'API offline'}</span><div className="persona-picker"><span className="avatar">{persona.initials}</span><label><small>DEMO ROLE</small><select aria-label="Demo role" value={persona.id} onChange={e => onPersona(e.target.value)}>{personas.map(p => <option key={p.id} value={p.id}>{p.label}</option>)}</select></label></div></div></header>
}
