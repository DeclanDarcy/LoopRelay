import { useCallback, useEffect, useState } from 'react'
import { formatError, getRepositoryWorkspace, refreshRepositoryWorkspace } from '../api'
import type { RepositoryWorkspaceProjection } from '../types'

export function useRepositoryWorkspace(repositoryId: string | null) {
  const [data, setData] = useState<RepositoryWorkspaceProjection | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    if (!repositoryId) {
      setData(null)
      setIsLoading(false)
      return null
    }

    setIsLoading(true)
    setError(null)
    try {
      const nextWorkspace = await getRepositoryWorkspace(repositoryId)
      setData(nextWorkspace)
      return nextWorkspace
    } catch (loadError) {
      const message = formatError(loadError)
      setData(null)
      setError(message)
      return null
    } finally {
      setIsLoading(false)
    }
  }, [repositoryId])

  const refresh = useCallback(async () => {
    if (!repositoryId) {
      setData(null)
      setIsLoading(false)
      return null
    }

    setIsLoading(true)
    setError(null)
    try {
      const nextWorkspace = await refreshRepositoryWorkspace(repositoryId)
      setData(nextWorkspace)
      return nextWorkspace
    } catch (refreshError) {
      const message = formatError(refreshError)
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
      void load().catch(() => undefined)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [load, repositoryId])

  return { data, setData, isLoading, error, load, refresh }
}
