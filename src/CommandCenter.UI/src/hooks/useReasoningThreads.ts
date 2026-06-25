import { useCallback, useEffect, useState } from 'react'
import { formatError, getBoundaryViolation, listReasoningThreads } from '../api'
import type { BoundaryViolationProjection, ReasoningThread } from '../types'

export function useReasoningThreads(repositoryId: string | null) {
  const [data, setData] = useState<ReasoningThread[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [boundaryViolation, setBoundaryViolation] = useState<BoundaryViolationProjection | null>(null)

  const refresh = useCallback(async () => {
    if (!repositoryId) {
      setData([])
      setIsLoading(false)
      return []
    }

    setIsLoading(true)
    setError(null)
    setBoundaryViolation(null)
    try {
      const threads = await listReasoningThreads(repositoryId)
      setData(threads)
      return threads
    } catch (threadsError) {
      const message = formatError(threadsError)
      setData([])
      setError(message)
      setBoundaryViolation(getBoundaryViolation(threadsError))
      return []
    } finally {
      setIsLoading(false)
    }
  }, [repositoryId])

  useEffect(() => {
    if (!repositoryId) {
      const timeoutId = window.setTimeout(() => {
        setData([])
        setIsLoading(false)
        setBoundaryViolation(null)
      }, 0)

      return () => window.clearTimeout(timeoutId)
    }

    const timeoutId = window.setTimeout(() => {
      void refresh().catch(() => undefined)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [refresh, repositoryId])

  return { data, setData, isLoading, error, boundaryViolation, refresh, load: refresh }
}
