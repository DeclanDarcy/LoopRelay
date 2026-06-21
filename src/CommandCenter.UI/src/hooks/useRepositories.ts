import { useCallback, useEffect, useState } from 'react'
import { formatError, listRepositories } from '../api'
import type { RepositoryDashboardProjection } from '../types'

export function useRepositories() {
  const [data, setData] = useState<RepositoryDashboardProjection[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    setIsLoading(true)
    setError(null)
    try {
      const nextRepositories = await listRepositories()
      setData(nextRepositories)
      return nextRepositories
    } catch (loadError) {
      const message = formatError(loadError)
      setError(message)
      return []
    } finally {
      setIsLoading(false)
    }
  }, [])

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void refresh().catch(() => undefined)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [refresh])

  return { data, setData, isLoading, error, refresh }
}
