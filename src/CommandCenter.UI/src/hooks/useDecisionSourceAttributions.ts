import { useCallback, useEffect, useState } from 'react'
import { formatError, listDecisionSourceAttributions } from '../api'
import type { DecisionSourceAttribution } from '../types'

export function useDecisionSourceAttributions(repositoryId: string | null, proposalId: string | null) {
  const [data, setData] = useState<DecisionSourceAttribution[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    if (!repositoryId || !proposalId) {
      setData([])
      setIsLoading(false)
      return []
    }

    setIsLoading(true)
    setError(null)
    try {
      const attributions = await listDecisionSourceAttributions(repositoryId, proposalId)
      setData(attributions)
      return attributions
    } catch (attributionError) {
      const message = formatError(attributionError)
      setData([])
      setError(message)
      return []
    } finally {
      setIsLoading(false)
    }
  }, [proposalId, repositoryId])

  useEffect(() => {
    if (!repositoryId || !proposalId) {
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
  }, [proposalId, refresh, repositoryId])

  return { data, setData, isLoading, error, refresh, load: refresh }
}
