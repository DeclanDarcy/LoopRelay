import { useCallback, useEffect, useState } from 'react'
import { formatError, getDecisionProposalLineage } from '../api'
import type { DecisionProposalLineage } from '../types'

export function useDecisionProposalLineage(repositoryId: string | null, proposalId: string | null) {
  const [data, setData] = useState<DecisionProposalLineage | null>(null)
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
      const lineage = await getDecisionProposalLineage(repositoryId, proposalId)
      setData(lineage)
      return lineage
    } catch (lineageError) {
      const message = formatError(lineageError)
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
