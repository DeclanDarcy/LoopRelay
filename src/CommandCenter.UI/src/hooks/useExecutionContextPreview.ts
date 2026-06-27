import { useCallback, useState } from 'react'
import { formatError, previewExecutionContext } from '../api'
import type { ExecutionContextPreview } from '../types'

export function useExecutionContextPreview(repositoryId: string | null) {
  const [data, setData] = useState<ExecutionContextPreview | null>(null)
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
      const nextPreview = await previewExecutionContext(repositoryId)
      setData(nextPreview)
      return nextPreview
    } catch (loadError) {
      const message = formatError(loadError)
      setError(message)
      return null
    } finally {
      setIsLoading(false)
    }
  }, [repositoryId])

  return { data, setData, isLoading, error, load, refresh: load }
}
