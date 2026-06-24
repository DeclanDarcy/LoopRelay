import { useCallback, useEffect, useState } from 'react'
import {
  formatError,
  getWorkflowContinuationEvaluation,
  getWorkflowContinuationHistory,
  runWorkflowContinuation,
} from '../api'
import type { WorkflowContinuationEvaluation, WorkflowContinuationEvent } from '../types'

export function useWorkflowContinuation(repositoryId: string | null) {
  const [evaluation, setEvaluation] = useState<WorkflowContinuationEvaluation | null>(null)
  const [history, setHistory] = useState<WorkflowContinuationEvent[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [isRunning, setIsRunning] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    if (!repositoryId) {
      setEvaluation(null)
      setHistory([])
      setIsLoading(false)
      return null
    }

    setIsLoading(true)
    setError(null)
    try {
      const [nextEvaluation, nextHistory] = await Promise.all([
        getWorkflowContinuationEvaluation(repositoryId),
        getWorkflowContinuationHistory(repositoryId),
      ])
      setEvaluation(nextEvaluation)
      setHistory(nextHistory)
      return nextEvaluation
    } catch (continuationError) {
      const message = formatError(continuationError)
      setEvaluation(null)
      setHistory([])
      setError(message)
      return null
    } finally {
      setIsLoading(false)
    }
  }, [repositoryId])

  const run = useCallback(async () => {
    if (!repositoryId) {
      return null
    }

    setIsRunning(true)
    setError(null)
    try {
      const event = await runWorkflowContinuation(repositoryId)
      await refresh()
      return event
    } catch (continuationError) {
      const message = formatError(continuationError)
      setError(message)
      return null
    } finally {
      setIsRunning(false)
    }
  }, [refresh, repositoryId])

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void refresh().catch(() => undefined)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [refresh])

  return { evaluation, history, isLoading, isRunning, error, refresh, load: refresh, run }
}
