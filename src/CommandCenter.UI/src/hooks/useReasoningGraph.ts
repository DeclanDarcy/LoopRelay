import { useCallback, useEffect, useState } from 'react'
import {
  formatError,
  getReasoningGraph,
  traceReasoningBackward,
  traceReasoningForward,
} from '../api'
import type {
  ReasoningGraph,
  ReasoningReferenceKind,
  ReasoningTrace,
} from '../types'

export function useReasoningGraph(repositoryId: string | null) {
  const [data, setData] = useState<ReasoningGraph | null>(null)
  const [backwardTrace, setBackwardTrace] = useState<ReasoningTrace | null>(null)
  const [forwardTrace, setForwardTrace] = useState<ReasoningTrace | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [isTracing, setIsTracing] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    if (!repositoryId) {
      setData(null)
      setBackwardTrace(null)
      setForwardTrace(null)
      setIsLoading(false)
      return null
    }

    setIsLoading(true)
    setError(null)
    try {
      const graph = await getReasoningGraph(repositoryId)
      setData(graph)
      return graph
    } catch (graphError) {
      const message = formatError(graphError)
      setData(null)
      setBackwardTrace(null)
      setForwardTrace(null)
      setError(message)
      return null
    } finally {
      setIsLoading(false)
    }
  }, [repositoryId])

  const trace = useCallback(
    async (kind: ReasoningReferenceKind, id: string) => {
      if (!repositoryId) {
        setBackwardTrace(null)
        setForwardTrace(null)
        return null
      }

      setIsTracing(true)
      setError(null)
      try {
        const [backward, forward] = await Promise.all([
          traceReasoningBackward(repositoryId, kind, id),
          traceReasoningForward(repositoryId, kind, id),
        ])
        setBackwardTrace(backward)
        setForwardTrace(forward)
        return { backward, forward }
      } catch (traceError) {
        const message = formatError(traceError)
        setBackwardTrace(null)
        setForwardTrace(null)
        setError(message)
        return null
      } finally {
        setIsTracing(false)
      }
    },
    [repositoryId],
  )

  useEffect(() => {
    if (!repositoryId) {
      const timeoutId = window.setTimeout(() => {
        setData(null)
        setBackwardTrace(null)
        setForwardTrace(null)
        setIsLoading(false)
      }, 0)

      return () => window.clearTimeout(timeoutId)
    }

    const timeoutId = window.setTimeout(() => {
      void refresh().catch(() => undefined)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [refresh, repositoryId])

  return {
    data,
    backwardTrace,
    forwardTrace,
    isLoading,
    isTracing,
    error,
    refresh,
    trace,
  }
}
