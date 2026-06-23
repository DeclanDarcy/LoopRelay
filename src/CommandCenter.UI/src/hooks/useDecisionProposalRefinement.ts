import { useCallback, useState } from 'react'
import {
  analyzeDecisionRefinement,
  formatError,
  refineDecisionProposal,
  regenerateDecisionRefinement,
} from '../api'
import type {
  DecisionPackageRegenerationRequest,
  DecisionRefinementAnalysisRequest,
  DecisionRefinementRequest,
} from '../types'

export function useDecisionProposalRefinement(repositoryId: string | null, proposalId: string | null) {
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refine = useCallback(
    async (request: DecisionRefinementRequest) => {
      if (!repositoryId || !proposalId) {
        const message = 'Select a proposal before requesting refinement.'
        setError(message)
        return null
      }

      setIsSubmitting(true)
      setError(null)
      try {
        const proposal = await refineDecisionProposal(repositoryId, proposalId, request)
        return proposal
      } catch (refinementError) {
        const message = formatError(refinementError)
        setError(message)
        return null
      } finally {
        setIsSubmitting(false)
      }
    },
    [proposalId, repositoryId],
  )

  const analyze = useCallback(
    async (request: DecisionRefinementAnalysisRequest) => {
      if (!repositoryId || !proposalId) {
        const message = 'Select a proposal before analyzing refinement guidance.'
        setError(message)
        return null
      }

      setIsSubmitting(true)
      setError(null)
      try {
        return await analyzeDecisionRefinement(repositoryId, proposalId, request)
      } catch (analysisError) {
        const message = formatError(analysisError)
        setError(message)
        return null
      } finally {
        setIsSubmitting(false)
      }
    },
    [proposalId, repositoryId],
  )

  const regenerate = useCallback(
    async (request: DecisionPackageRegenerationRequest) => {
      if (!repositoryId || !proposalId) {
        const message = 'Select a proposal before regenerating a decision package.'
        setError(message)
        return null
      }

      setIsSubmitting(true)
      setError(null)
      try {
        return await regenerateDecisionRefinement(repositoryId, proposalId, request)
      } catch (regenerationError) {
        const message = formatError(regenerationError)
        setError(message)
        return null
      } finally {
        setIsSubmitting(false)
      }
    },
    [proposalId, repositoryId],
  )

  return { refine, analyze, regenerate, isSubmitting, error, setError }
}
