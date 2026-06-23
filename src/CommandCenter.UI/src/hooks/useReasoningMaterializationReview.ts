import { useCallback, useEffect, useState } from 'react'
import { formatError, getReasoningMaterializationReview, runReasoningMaterializationReview } from '../api'
import type {
  ReasoningMaterializationReviewReport,
  ReasoningMaterializationReviewRequest,
} from '../types'

export function useReasoningMaterializationReview(repositoryId: string | null) {
  const [data, setData] = useState<ReasoningMaterializationReviewReport | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [isRunning, setIsRunning] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    if (!repositoryId) {
      setData(null)
      setIsLoading(false)
      setError(null)
      return null
    }

    setIsLoading(true)
    setError(null)
    try {
      const report = await getReasoningMaterializationReview(repositoryId)
      setData(report)
      return report
    } catch (reviewError) {
      const message = formatError(reviewError)
      setData(null)
      setError(message)
      return null
    } finally {
      setIsLoading(false)
    }
  }, [repositoryId])

  const run = useCallback(
    async (request: ReasoningMaterializationReviewRequest = {}) => {
      if (!repositoryId) {
        setData(null)
        return null
      }

      setIsRunning(true)
      setError(null)
      try {
        const report = await runReasoningMaterializationReview(repositoryId, request)
        setData(report)
        return report
      } catch (reviewError) {
        const message = formatError(reviewError)
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
        setIsLoading(false)
        setIsRunning(false)
        setError(null)
      }, 0)

      return () => window.clearTimeout(timeoutId)
    }

    const timeoutId = window.setTimeout(() => {
      void refresh().catch(() => undefined)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [refresh, repositoryId])

  return { data, setData, isLoading, isRunning, error, refresh, run }
}
