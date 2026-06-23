import { useCallback, useEffect, useState } from 'react'
import { formatError, listReasoningRelationships } from '../api'
import type { ReasoningRelationship } from '../types'

export function useReasoningRelationships(repositoryId: string | null) {
  const [data, setData] = useState<ReasoningRelationship[]>([])
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
      const relationships = await listReasoningRelationships(repositoryId)
      setData(relationships)
      return relationships
    } catch (relationshipsError) {
      const message = formatError(relationshipsError)
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
