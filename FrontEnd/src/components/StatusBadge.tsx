import type { RequestStatus } from '../types'
export function StatusBadge({ status }: { status: RequestStatus }) {
  return <span className={'status-badge ' + status.toLowerCase()}><span />{status === 'Pending' ? 'Pending review' : status}</span>
}
