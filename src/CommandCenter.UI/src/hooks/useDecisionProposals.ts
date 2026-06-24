import { useCallback, useEffect, useState } from 'react'
import {
  discardDecisionProposal,
  expireDecisionProposal,
  formatError,
  generateDecisionProposal,
  listDecisionProposalBrowser,
} from '../api'
import type {
  DecisionProposalBrowserItem,
  DecisionProposalState,
  DecisionProposalTransitionRequest,
} from '../types'

export function useDecisionProposals(
  repositoryId: string | null,
  states: DecisionProposalState[] = [],
) {
  const [data, setData] = useState<DecisionProposalBrowserItem[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [isMutating, setIsMutating] = useState(false)
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

  const runMutation = useCallback(async <T>(operation: () => Promise<T>) => {
    if (!repositoryId) {
      return null
    }

    setIsMutating(true)
    setError(null)
    try {
      const result = await operation()
      await refresh()
      return result
    } catch (mutationError) {
      const message = formatError(mutationError)
      setError(message)
      return null
    } finally {
      setIsMutating(false)
    }
  }, [refresh, repositoryId])

  const generateProposal = useCallback((candidateId: string) =>
    runMutation(() => generateDecisionProposal(repositoryId as string, candidateId)), [
    repositoryId,
    runMutation,
  ])

  const expireProposal = useCallback((proposalId: string, request: DecisionProposalTransitionRequest = {}) =>
    runMutation(() => expireDecisionProposal(repositoryId as string, proposalId, request)), [
    repositoryId,
    runMutation,
  ])

  const discardProposal = useCallback((proposalId: string, request: DecisionProposalTransitionRequest = {}) =>
    runMutation(() => discardDecisionProposal(repositoryId as string, proposalId, request)), [
    repositoryId,
    runMutation,
  ])

  return {
    data,
    setData,
    isLoading,
    isMutating,
    error,
    refresh,
    load: refresh,
    generateProposal,
    expireProposal,
    discardProposal,
  }
}
