import { useCallback, useEffect, useState } from 'react'
import {
  formatError,
  getDecisionProposalReview,
  markDecisionProposalNeedsRefinement,
  markDecisionProposalReadyForResolution,
  markDecisionProposalViewed,
} from '../api'
import type { DecisionProposalTransitionRequest, DecisionReviewWorkspace } from '../types'

export function useDecisionProposalReview(repositoryId: string | null, proposalId: string | null) {
  const [data, setData] = useState<DecisionReviewWorkspace | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [isMutating, setIsMutating] = useState(false)
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
      const workspace = await getDecisionProposalReview(repositoryId, proposalId)
      setData(workspace)
      return workspace
    } catch (reviewError) {
      const message = formatError(reviewError)
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

  const runMutation = useCallback(async <T>(operation: () => Promise<T>) => {
    if (!repositoryId || !proposalId) {
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
  }, [proposalId, refresh, repositoryId])

  const markViewed = useCallback((request: DecisionProposalTransitionRequest = {}) =>
    runMutation(() => markDecisionProposalViewed(repositoryId as string, proposalId as string, request)), [
    proposalId,
    repositoryId,
    runMutation,
  ])

  const markNeedsRefinement = useCallback((request: DecisionProposalTransitionRequest = {}) =>
    runMutation(() => markDecisionProposalNeedsRefinement(repositoryId as string, proposalId as string, request)), [
    proposalId,
    repositoryId,
    runMutation,
  ])

  const markReadyForResolution = useCallback((request: DecisionProposalTransitionRequest = {}) =>
    runMutation(() => markDecisionProposalReadyForResolution(repositoryId as string, proposalId as string, request)), [
    proposalId,
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
    markViewed,
    markNeedsRefinement,
    markReadyForResolution,
  }
}
