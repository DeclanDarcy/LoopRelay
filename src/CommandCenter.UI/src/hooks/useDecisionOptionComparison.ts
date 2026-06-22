import { useCallback, useEffect, useState } from 'react'
import { formatError, getDecisionOptionComparison } from '../api'
import type { DecisionOptionComparison } from '../types'

export function useDecisionOptionComparison(repositoryId: string | null, proposalId: string | null) {
  const [data, setData] = useState<DecisionOptionComparison | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    if (!repositoryId || !proposalId) {
      setData(null)
      setIsLoading(false)
      return null
    }

    setIsLoading(true)
    setError(null)
    try {
      const comparison = await getDecisionOptionComparison(repositoryId, proposalId)
      setData(comparison)
      return comparison
    } catch (comparisonError) {
      const message = formatError(comparisonError)
      setData(null)
      setError(message)
      return null
    } finally {
      setIsLoading(false)
    }
  }, [proposalId, repositoryId])

  useEffect(() => {
    if (!repositoryId || !proposalId) {
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
  }, [proposalId, refresh, repositoryId])

  return { data, setData, isLoading, error, refresh, load: refresh }
}
