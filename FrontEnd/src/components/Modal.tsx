import { useEffect, useRef } from 'react'
import type { ReactNode } from 'react'
import { Icon } from './Icon'

export function Modal({ title, subtitle, children, onClose, busy = false }: { title: string; subtitle: string; children: ReactNode; onClose: () => void; busy?: boolean }) {
  const ref = useRef<HTMLDialogElement>(null)
  useEffect(() => { const dialog = ref.current!; dialog.showModal(); return () => { dialog.close() } }, [])
  return <dialog ref={ref} className="modal" aria-labelledby="dialog-title" aria-describedby="dialog-subtitle" onCancel={event => { event.preventDefault(); if (!busy) onClose() }} onClick={event => { if (event.target === ref.current && !busy) onClose() }}><div className="modal-heading"><div><span className="eyebrow">BLUE HARVEST WORKSPACE</span><h2 id="dialog-title">{title}</h2><p id="dialog-subtitle">{subtitle}</p></div><button className="icon-button" aria-label="Close dialog" disabled={busy} onClick={onClose}><Icon name="close"/></button></div>{children}</dialog>
}
