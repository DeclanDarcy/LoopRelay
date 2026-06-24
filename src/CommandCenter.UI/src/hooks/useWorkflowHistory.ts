import { useCallback, useEffect, useState } from 'react'
import { formatError, getWorkflowHistory, getWorkflowTimeline, getWorkflowTransitions } from '../api'
import type { WorkflowHistoryProjection, WorkflowStateMachineDiagnostics, WorkflowTimeline } from '../types'

export function useWorkflowHistory(repositoryId: string | null) {
  const [history, setHistory] = useState<WorkflowHistoryProjection | null>(null)
  const [timeline, setTimeline] = useState<WorkflowTimeline | null>(null)
  const [transitions, setTransitions] = useState<WorkflowStateMachineDiagnostics | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    if (!repositoryId) {
      setHistory(null)
      setTimeline(null)
      setTransitions(null)
      setIsLoading(false)
      return null
    }

    setIsLoading(true)
    setError(null)
    try {
      const [nextHistory, nextTimeline, nextTransitions] = await Promise.all([
        getWorkflowHistory(repositoryId),
        getWorkflowTimeline(repositoryId),
        getWorkflowTransitions(repositoryId),
      ])
      setHistory(nextHistory)
      setTimeline(nextTimeline)
      setTransitions(nextTransitions)
      return nextHistory
    } catch (historyError) {
      const message = formatError(historyError)
      setHistory(null)
      setTimeline(null)
      setTransitions(null)
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

  return { history, timeline, transitions, isLoading, error, refresh, load: refresh }
}
