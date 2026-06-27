import { useCallback, useEffect, useState } from 'react'
import { formatError, getPlanStatus } from '../api'
import type { PlanStatus } from '../types'

export function usePlanStatus(repositoryId: string | null) {
  const [data, setData] = useState<PlanStatus | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    if (!repositoryId) {
      setData(null)
      return null
    }

    setIsLoading(true)
    setError(null)
    try {
      const nextStatus = await getPlanStatus(repositoryId)
      setData(nextStatus)
      return nextStatus
    } catch (loadError) {
      setError(formatError(loadError))
      return null
    } finally {
      setIsLoading(false)
    }
  }, [repositoryId])

  useEffect(() => {
    setData(null)
    if (!repositoryId) {
      return
    }

    const timeoutId = window.setTimeout(() => {
      void refresh().catch(() => undefined)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [refresh, repositoryId])

  return { data, setData, isLoading, error, refresh }
}
