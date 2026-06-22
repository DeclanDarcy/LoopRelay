import { useCallback, useEffect, useState } from 'react'
import { formatError, listDecisionProposalBrowser } from '../api'
import type { DecisionProposalBrowserItem, DecisionProposalState } from '../types'

export function useDecisionProposals(
  repositoryId: string | null,
  states: DecisionProposalState[] = [],
) {
  const [data, setData] = useState<DecisionProposalBrowserItem[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const stateKey = states.join(',')

  const refresh = useCallback(async () => {
    if (!repositoryId) {
      setData([])
      setIsLoading(false)
      return []
    }

    setIsLoading(true)
    setError(null)
    try {
      const selectedStates = stateKey ? (stateKey.split(',') as DecisionProposalState[]) : []
      const proposals = await listDecisionProposalBrowser(repositoryId, selectedStates)
      setData(proposals)
      return proposals
    } catch (proposalError) {
      const message = formatError(proposalError)
      setData([])
      setError(message)
      return []
    } finally {
      setIsLoading(false)
    }
  }, [repositoryId, stateKey])

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
