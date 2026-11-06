import type { Persona } from '../types'
import { demoUsersEnabled, personas } from '../lib/format'
import { Icon } from './Icon'
import type { Theme } from '../hooks/useTheme'

export function Header({ persona, onPersona, connected, onMenu, theme, onToggleTheme }: { persona: Persona; onPersona: (id: string) => void; connected: boolean; onMenu: () => void; theme: Theme; onToggleTheme: () => void }) {
  const role = persona.role === 'RelationshipManager' ? 'Relationship manager' : persona.role === 'Reviewer' ? 'Reviewer' : 'Mortgage process'
  return <header className="topbar"><div className="breadcrumbs"><button className="icon-button mobile-menu" aria-label="Open navigation" onClick={onMenu}><Icon name="menu"/></button><span>Mortgage Management</span><span className="breadcrumb-divider">/</span><strong>Pricing Exceptions</strong></div><div className="header-actions"><span className={'connection ' + (connected ? 'online' : '')}><span/>{connected ? 'API connected' : 'API offline'}</span><button type="button" className="theme-toggle" aria-label="Toggle dark mode" aria-pressed={theme === 'dark'} title={theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'} onClick={onToggleTheme}><Icon name={theme === 'dark' ? 'sun' : 'moon'}/></button>{demoUsersEnabled && <div className="persona-picker"><span className="avatar">{persona.initials}</span><div><label><small>Select Demo User</small><select aria-label="Select Demo User" value={persona.id} onChange={e => onPersona(e.target.value)}>{personas.map(p => <option key={p.id} value={p.id}>{p.label}</option>)}</select></label><span className="selected-demo-role">{role}</span></div></div>}</div></header>
}
