import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError, checkConnection, pricingApi } from '../api/pricingExceptions'
import type { PricingException } from '../types'

const storageKey = 'blueharvest.requestIds'
function savedIds(): string[] {
  try {
    const ids = JSON.parse(localStorage.getItem(storageKey) ?? '[]')
    return Array.isArray(ids) ? ids.filter((id): id is string => typeof id === 'string').slice(0, 100) : []
  } catch { return [] }
}
interface WorkspaceState {
  actor: string
  requests: PricingException[]
  loading: boolean
  connected: boolean
  error: string
}
export function useRequestWorkspace(actor: string) {
  const ids = useRef(savedIds())
  const generation = useRef(0)
  const [state, setState] = useState<WorkspaceState>({
    actor, requests: [], loading: true, connected: false, error: '',
  })

  // Effect-driven reads update React state only after their asynchronous work.
  // Abort signals and generation checks reject stale results after role changes or writes.
  const load = useCallback(async (signal?: AbortSignal) => {
    const current = ++generation.current
    const connection = await checkConnection(signal)
    if (signal?.aborted) return
    try {
      const responses = await Promise.all(ids.current.map(async id => {
        try { return await pricingApi.get(id, actor, signal) } catch (error) {
          if (error instanceof ApiError && error.status === 404) return null
          throw error
        }
      }))
      if (signal?.aborted || current !== generation.current) return
      const found = responses.filter((request): request is PricingException => request !== null)
      ids.current = found.map(request => request.id)
      try { localStorage.setItem(storageKey, JSON.stringify(ids.current)) } catch { /* Optional browser persistence. */ }
      setState({ actor, requests: found, loading: false, connected: connection, error: '' })
    } catch (error) {
      if (signal?.aborted || current !== generation.current) return
      setState(previous => ({
        actor, requests: previous.actor === actor ? previous.requests : [],
        loading: false, connected: connection, error: (error as Error).message,
      }))
    }
  }, [actor])

  useEffect(() => {
    const controller = new AbortController()
    void load(controller.signal)
    return () => controller.abort()
  }, [load])

  const refresh = useCallback(() => {
    setState(previous => ({ ...previous, loading: true, error: '' }))
    return load()
  }, [load])

  const remember = useCallback((request: PricingException) => {
    generation.current++
    ids.current = [request.id, ...ids.current.filter(id => id !== request.id)].slice(0, 100)
    try { localStorage.setItem(storageKey, JSON.stringify(ids.current)) } catch { /* Optional browser persistence. */ }
    setState(previous => ({
      actor, requests: [request, ...previous.requests.filter(item => item.id !== request.id)],
      connected: true, loading: false, error: '',
    }))
  }, [actor])

  const currentActor = state.actor === actor
  return {
    requests: currentActor ? state.requests : [],
    loading: !currentActor || state.loading,
    connected: currentActor && state.connected,
    error: currentActor ? state.error : '',
    refresh, remember,
  }
}
