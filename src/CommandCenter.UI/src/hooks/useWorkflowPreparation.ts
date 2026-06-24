import { useCallback, useEffect, useState } from 'react'
import {
  formatError,
  getWorkflowPreparationEvaluation,
  getWorkflowPreparationHistory,
  runWorkflowPreparation,
} from '../api'
import type { WorkflowPreparationEvaluation, WorkflowPreparationEvent } from '../types'

export function useWorkflowPreparation(repositoryId: string | null) {
  const [evaluation, setEvaluation] = useState<WorkflowPreparationEvaluation | null>(null)
  const [history, setHistory] = useState<WorkflowPreparationEvent[]>([])
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
        getWorkflowPreparationEvaluation(repositoryId),
        getWorkflowPreparationHistory(repositoryId),
      ])
      setEvaluation(nextEvaluation)
      setHistory(nextHistory)
      return nextEvaluation
    } catch (preparationError) {
      const message = formatError(preparationError)
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
      const event = await runWorkflowPreparation(repositoryId)
      await refresh()
      return event
    } catch (preparationError) {
      const message = formatError(preparationError)
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
