import { useCallback, useEffect, useState } from 'react'
import { formatError, getWorkflowGateHistory, getWorkflowGates } from '../api'
import type { WorkflowGateCatalogProjection, WorkflowGateHistoryProjection } from '../types'

export function useWorkflowGates(repositoryId: string | null) {
  const [gates, setGates] = useState<WorkflowGateCatalogProjection | null>(null)
  const [history, setHistory] = useState<WorkflowGateHistoryProjection | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    if (!repositoryId) {
      setGates(null)
      setHistory(null)
      setIsLoading(false)
      return null
    }

    setIsLoading(true)
    setError(null)
    try {
      const [nextGates, nextHistory] = await Promise.all([
        getWorkflowGates(repositoryId),
        getWorkflowGateHistory(repositoryId),
      ])
      setGates(nextGates)
      setHistory(nextHistory)
      return nextGates
    } catch (gateError) {
      const message = formatError(gateError)
      setGates(null)
      setHistory(null)
      setError(message)
      return null
    } finally {
      setIsLoading(false)
    }
  }, [repositoryId])

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void refresh().catch(() => undefined)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [refresh])

  return { gates, history, isLoading, error, refresh, load: refresh }
}
