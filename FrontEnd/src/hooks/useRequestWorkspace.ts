import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError, checkConnection, pricingApi } from '../api/pricingExceptions'
import type { PricingException } from '../types'

const storageKey = 'blueharvest.requestIds'
function savedIds(): string[] {
  try { const ids = JSON.parse(localStorage.getItem(storageKey) ?? '[]'); return Array.isArray(ids) ? ids.filter((id): id is string => typeof id === 'string').slice(0, 100) : [] } catch { return [] }
}
export function useRequestWorkspace(actor: string) {
  const ids = useRef(savedIds())
  const generation = useRef(0)
  const [requests, setRequests] = useState<PricingException[]>([])
  const [loading, setLoading] = useState(true)
  const [connected, setConnected] = useState(false)
  const [error, setError] = useState('')
  const refresh = useCallback(async () => {
    const current = ++generation.current
    setLoading(true); setError('')
    const connection = await checkConnection()
    const responses = await Promise.all(ids.current.map(async id => {
      try { return await pricingApi.get(id, actor) } catch (error) {
        if (error instanceof ApiError && error.status === 404) return null
        throw error
      }
    })).catch((error: Error) => { if (current === generation.current) setError(error.message); return null })
    if (current !== generation.current) return
    setConnected(connection)
    if (responses) {
      const found = responses.filter((r): r is PricingException => r !== null)
      setRequests(found)
      ids.current = found.map(r => r.id)
      try { localStorage.setItem(storageKey, JSON.stringify(ids.current)) } catch { /* Workspace still works without local storage. */ }
    }
    setLoading(false)
  }, [actor])
  useEffect(() => { void refresh(); return () => { generation.current++ } }, [refresh])
  const remember = useCallback((request: PricingException) => {
    generation.current++
    ids.current = [request.id, ...ids.current.filter(id => id !== request.id)].slice(0, 100)
    try { localStorage.setItem(storageKey, JSON.stringify(ids.current)) } catch { /* Optional persistence. */ }
    setRequests(previous => [request, ...previous.filter(r => r.id !== request.id)])
    setConnected(true); setLoading(false); setError('')
  }, [])
  return { requests, loading, connected, error, refresh, remember }
}
