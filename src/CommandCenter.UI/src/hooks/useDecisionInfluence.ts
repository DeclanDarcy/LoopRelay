import { useCallback, useEffect, useRef, useState } from 'react'
import { formatError, getDecisionInfluence, getExecutionDecisionInfluence } from '../api'
import type { DecisionInfluenceTrace } from '../types'

type RefreshOptions = {
  silent?: boolean
}

export function useExecutionDecisionInfluence(
  repositoryId: string | null,
  executionSessionId: string | null,
) {
  const [data, setData] = useState<DecisionInfluenceTrace | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const latestSelection = useRef({ repositoryId, executionSessionId })
  const requestSequence = useRef(0)

  useEffect(() => {
    latestSelection.current = { repositoryId, executionSessionId }
  }, [repositoryId, executionSessionId])

  const refresh = useCallback(
    async (options: RefreshOptions = {}) => {
      const requestedRepositoryId = repositoryId
      const requestedExecutionSessionId = executionSessionId
      const requestId = requestSequence.current + 1
      requestSequence.current = requestId

      if (!requestedRepositoryId || !requestedExecutionSessionId) {
        setData(null)
        if (!options.silent) {
          setIsLoading(false)
          setError(null)
        }
        return null
      }

      if (!options.silent) {
        setIsLoading(true)
        setError(null)
      }

      try {
        const trace = await getExecutionDecisionInfluence(
          requestedRepositoryId,
          requestedExecutionSessionId,
        )
        const currentSelection = latestSelection.current
        if (
          requestSequence.current === requestId &&
          currentSelection.repositoryId === requestedRepositoryId &&
          currentSelection.executionSessionId === requestedExecutionSessionId
        ) {
          setData(trace)
        }
        return trace
      } catch (loadError) {
        if (!options.silent && requestSequence.current === requestId) {
          setError(formatError(loadError))
          setData(null)
        }
        return null
      } finally {
        if (!options.silent && requestSequence.current === requestId) {
          setIsLoading(false)
        }
      }
    },
    [executionSessionId, repositoryId],
  )

  useEffect(() => {
    if (!repositoryId || !executionSessionId) {
      requestSequence.current += 1
      const timeoutId = window.setTimeout(() => {
        setData(null)
        setIsLoading(false)
        setError(null)
      }, 0)

      return () => window.clearTimeout(timeoutId)
    }

    const timeoutId = window.setTimeout(() => {
      void refresh().catch(() => undefined)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [executionSessionId, refresh, repositoryId])

  return { data, setData, isLoading, error, refresh, load: refresh }
}

export function useDecisionInfluence(repositoryId: string | null, decisionId: string | null) {
  const [data, setData] = useState<DecisionInfluenceTrace[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const latestSelection = useRef({ repositoryId, decisionId })
  const requestSequence = useRef(0)

  useEffect(() => {
    latestSelection.current = { repositoryId, decisionId }
  }, [repositoryId, decisionId])

  const refresh = useCallback(
    async (options: RefreshOptions = {}) => {
      const requestedRepositoryId = repositoryId
      const requestedDecisionId = decisionId
      const requestId = requestSequence.current + 1
      requestSequence.current = requestId

      if (!requestedRepositoryId || !requestedDecisionId) {
        setData([])
        if (!options.silent) {
          setIsLoading(false)
          setError(null)
        }
        return []
      }

      if (!options.silent) {
        setIsLoading(true)
        setError(null)
      }

      try {
        const traces = await getDecisionInfluence(requestedRepositoryId, requestedDecisionId)
        const currentSelection = latestSelection.current
        if (
          requestSequence.current === requestId &&
          currentSelection.repositoryId === requestedRepositoryId &&
          currentSelection.decisionId === requestedDecisionId
        ) {
          setData(traces)
        }
        return traces
      } catch (loadError) {
        if (!options.silent && requestSequence.current === requestId) {
          setError(formatError(loadError))
          setData([])
        }
        return []
      } finally {
        if (!options.silent && requestSequence.current === requestId) {
          setIsLoading(false)
        }
      }
    },
    [decisionId, repositoryId],
  )

  useEffect(() => {
    if (!repositoryId || !decisionId) {
      requestSequence.current += 1
      const timeoutId = window.setTimeout(() => {
        setData([])
        setIsLoading(false)
        setError(null)
      }, 0)

      return () => window.clearTimeout(timeoutId)
    }

    const timeoutId = window.setTimeout(() => {
      void refresh().catch(() => undefined)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [decisionId, refresh, repositoryId])

  return { data, setData, isLoading, error, refresh, load: refresh }
}
