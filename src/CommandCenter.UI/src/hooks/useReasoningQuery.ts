import { useCallback, useEffect, useState } from 'react'
import { formatError, getBoundaryViolation, queryReasoning } from '../api'
import type { BoundaryViolationProjection, ReasoningQuery, ReasoningQueryResult } from '../types'

export function useReasoningQuery(repositoryId: string | null) {
  const [data, setData] = useState<ReasoningQueryResult | null>(null)
  const [isRunning, setIsRunning] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [boundaryViolation, setBoundaryViolation] = useState<BoundaryViolationProjection | null>(null)

  const run = useCallback(
    async (query: ReasoningQuery) => {
      if (!repositoryId) {
        setData(null)
        return null
      }

      setIsRunning(true)
      setError(null)
      setBoundaryViolation(null)
      try {
        const result = await queryReasoning(repositoryId, query)
        setData(result)
        return result
      } catch (queryError) {
        const message = formatError(queryError)
        setData(null)
        setError(message)
        setBoundaryViolation(getBoundaryViolation(queryError))
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
        setBoundaryViolation(null)
      }, 0)

      return () => window.clearTimeout(timeoutId)
    }

    return undefined
  }, [repositoryId])

  return {
    data,
    isRunning,
    error,
    boundaryViolation,
    run,
  }
}
