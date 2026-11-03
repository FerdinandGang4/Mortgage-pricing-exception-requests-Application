import { useRef } from 'react'
export function useOperationKey() {
  const last = useRef<{ payload: string; key: string } | null>(null)
  return (payload: unknown) => {
    const serialized = JSON.stringify(payload)
    if (last.current?.payload !== serialized) last.current = { payload: serialized, key: crypto.randomUUID() }
    return last.current!.key
  }
}
