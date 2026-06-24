import { useCallback, useEffect, useState } from 'react'
import { formatError, getWorkflowProjection } from '../api'
import type { WorkflowInstance } from '../types'

export function useWorkflowProjection(repositoryId: string | null) {
  const [data, setData] = useState<WorkflowInstance | null>(null)
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
      const projection = await getWorkflowProjection(repositoryId)
      setData(projection)
      return projection
    } catch (loadError) {
      const message = formatError(loadError)
      setData(null)
      setError(message)
      return null
    } finally {
      setIsLoading(false)
    }
  }, [repositoryId])

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void load().catch(() => undefined)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [load])

  return { data, setData, isLoading, error, load, refresh: load }
}
