import { useCallback, useEffect, useState } from 'react'
import { formatError, listDecisionCandidates } from '../api'
import type { DecisionCandidate } from '../types'

export function useDecisionDiscovery(repositoryId: string | null) {
  const [data, setData] = useState<DecisionCandidate[]>([])
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
      const candidates = await listDecisionCandidates(repositoryId)
      setData(candidates)
      return candidates
    } catch (candidateError) {
      const message = formatError(candidateError)
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
