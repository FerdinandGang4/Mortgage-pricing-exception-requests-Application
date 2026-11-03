import type { ReactNode } from 'react'
import { Icon } from './Icon'
export function ErrorMessage({ message }: { message: string }) {
  return message ? <div className="error-message" role="alert"><Icon name="alert" size={18}/><span>{message}</span></div> : null
}
export function EmptyState({ title, children, action }: { title: string; children: ReactNode; action?: ReactNode }) {
  return <div className="empty-state"><div className="empty-icon"><Icon name="requests" size={28}/></div><h3>{title}</h3><p>{children}</p>{action}</div>
}
