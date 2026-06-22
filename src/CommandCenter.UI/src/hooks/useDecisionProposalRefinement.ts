import { useCallback, useState } from 'react'
import { formatError, refineDecisionProposal } from '../api'
import type { DecisionRefinementRequest } from '../types'

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

  return { refine, isSubmitting, error, setError }
}
