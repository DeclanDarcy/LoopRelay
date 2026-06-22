import { useCallback, useEffect, useState } from 'react'
import { formatError, listContinuityReports } from '../api'
import type { ContinuityReport } from '../types'

export function useContinuityReports(repositoryId: string | null) {
  const [data, setData] = useState<ContinuityReport[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    if (!repositoryId) {
      setData([])
      setIsLoading(false)
      return []
    }

    setIsLoading(true)
    setError(null)
    try {
      const reports = await listContinuityReports(repositoryId)
      setData(reports)
      return reports
    } catch (reportsError) {
      const message = formatError(reportsError)
      setData([])
      setError(message)
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
      }, 0)

      return () => window.clearTimeout(timeoutId)
    }

    const timeoutId = window.setTimeout(() => {
      void refresh().catch(() => undefined)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [refresh, repositoryId])

  return { data, setData, isLoading, error, refresh, load: refresh }
}
