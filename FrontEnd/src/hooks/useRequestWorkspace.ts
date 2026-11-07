import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError, checkConnection, pricingApi } from '../api/pricingExceptions'
import type { PricingException } from '../types'

const storageKey = 'blueharvest.requestIds'
function savedIds(): string[] {
  try {
    const savedRequestIds = JSON.parse(localStorage.getItem(storageKey) ?? '[]')
    return Array.isArray(savedRequestIds) ? savedRequestIds.filter((id): id is string => typeof id === 'string').slice(0, 100) : []
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
  const savedRequestIds = useRef(savedIds())
  const requestLoadGeneration = useRef(0)
  const [state, setState] = useState<WorkspaceState>({
    actor, requests: [], loading: true, connected: false, error: '',
  })

  // Effect-driven reads update React state only after their asynchronous work.
  // Abort signals and generation checks reject stale results after role changes or writes.
  const load = useCallback(async (signal?: AbortSignal) => {
    const loadGeneration = ++requestLoadGeneration.current
    const isApiConnected = await checkConnection(signal)
    if (signal?.aborted) return
    try {
      const requestResponses = await Promise.all(savedRequestIds.current.map(async id => {
        try { return await pricingApi.get(id, actor, signal) } catch (error) {
          if (error instanceof ApiError && error.status === 404) return null
          throw error
        }
      }))
      if (signal?.aborted || loadGeneration !== requestLoadGeneration.current) return
      const loadedRequests = requestResponses.filter((request): request is PricingException => request !== null)
      savedRequestIds.current = loadedRequests.map(request => request.id)
      try { localStorage.setItem(storageKey, JSON.stringify(savedRequestIds.current)) } catch { /* Optional browser persistence. */ }
      setState({ actor, requests: loadedRequests, loading: false, connected: isApiConnected, error: '' })
    } catch (error) {
      if (signal?.aborted || loadGeneration !== requestLoadGeneration.current) return
      setState(previous => ({
        actor, requests: previous.actor === actor ? previous.requests : [],
        loading: false, connected: isApiConnected, error: (error as Error).message,
      }))
    }
  }, [actor])

  useEffect(() => {
    const requestAbortController = new AbortController()
    void load(requestAbortController.signal)
    return () => requestAbortController.abort()
  }, [load])

  const refresh = useCallback(() => {
    setState(previous => ({ ...previous, loading: true, error: '' }))
    return load()
  }, [load])

  const remember = useCallback((request: PricingException) => {
    requestLoadGeneration.current++
    savedRequestIds.current = [request.id, ...savedRequestIds.current.filter(id => id !== request.id)].slice(0, 100)
    try { localStorage.setItem(storageKey, JSON.stringify(savedRequestIds.current)) } catch { /* Optional browser persistence. */ }
    setState(previous => ({
      actor, requests: [request, ...previous.requests.filter(existingRequest => existingRequest.id !== request.id)],
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
