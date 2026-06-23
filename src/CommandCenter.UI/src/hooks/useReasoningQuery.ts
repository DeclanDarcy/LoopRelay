import { useCallback, useEffect, useState } from 'react'
import { formatError, queryReasoning } from '../api'
import type { ReasoningQuery, ReasoningQueryResult } from '../types'

export function useReasoningQuery(repositoryId: string | null) {
  const [data, setData] = useState<ReasoningQueryResult | null>(null)
  const [isRunning, setIsRunning] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const run = useCallback(
    async (query: ReasoningQuery) => {
      if (!repositoryId) {
        setData(null)
        return null
      }

      setIsRunning(true)
      setError(null)
      try {
        const result = await queryReasoning(repositoryId, query)
        setData(result)
        return result
      } catch (queryError) {
        const message = formatError(queryError)
        setData(null)
        setError(message)
        return null
      } finally {
        setIsRunning(false)
      }
    },
    [repositoryId],
  )

  useEffect(() => {
    if (!repositoryId) {
      const timeoutId = window.setTimeout(() => {
        setData(null)
        setIsRunning(false)
        setError(null)
      }, 0)

      return () => window.clearTimeout(timeoutId)
    }

    return undefined
  }, [repositoryId])

  return {
    data,
    isRunning,
    error,
    run,
  }
}
