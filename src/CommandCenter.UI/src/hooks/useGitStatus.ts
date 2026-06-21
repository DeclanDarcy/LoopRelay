import { useCallback, useEffect, useState } from 'react'
import { formatError, getGitStatus } from '../api'
import type { RepositoryGitStatus } from '../types'

export function useGitStatus(repositoryId: string | null) {
  const [data, setData] = useState<RepositoryGitStatus | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    if (!repositoryId) {
      setData(null)
      setIsLoading(false)
      return null
    }

    setIsLoading(true)
    setError(null)
    try {
      const status = await getGitStatus(repositoryId)
      setData(status)
      return status
    } catch (statusError) {
      const message = formatError(statusError)
      setData(null)
      setError(message)
      return null
    } finally {
      setIsLoading(false)
    }
  }, [repositoryId])

  useEffect(() => {
    if (!repositoryId) {
      const timeoutId = window.setTimeout(() => {
        setData(null)
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
