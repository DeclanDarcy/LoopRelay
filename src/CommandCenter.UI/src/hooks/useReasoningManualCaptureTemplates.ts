import { useCallback, useEffect, useState } from 'react'
import { formatError, listReasoningManualCaptureTemplates } from '../api'
import type { ManualReasoningCaptureTemplate } from '../types'

export function useReasoningManualCaptureTemplates(repositoryId: string | null) {
  const [data, setData] = useState<ManualReasoningCaptureTemplate[]>([])
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
      const templates = await listReasoningManualCaptureTemplates(repositoryId)
      setData(templates)
      return templates
    } catch (templateError) {
      const message = formatError(templateError)
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
