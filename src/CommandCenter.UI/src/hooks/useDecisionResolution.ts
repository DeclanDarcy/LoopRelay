import { useCallback, useState } from 'react'
import {
  formatError,
  getDecisionAssimilationRecommendation,
  proposeDecisionOperationalContextAssimilation,
  resolveDecisionProposal,
} from '../api'
import type {
  CreateDecisionAssimilationRecommendationCommand,
  Decision,
  DecisionAssimilationRecommendation,
  ResolveDecisionCommand,
} from '../types'

export function useDecisionResolution(repositoryId: string | null, proposalId: string | null) {
  const [decision, setDecision] = useState<Decision | null>(null)
  const [assimilationRecommendation, setAssimilationRecommendation] =
    useState<DecisionAssimilationRecommendation | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [isAssimilationLoading, setIsAssimilationLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const resolve = useCallback(
    async (request: ResolveDecisionCommand) => {
      if (!repositoryId || !proposalId) {
        return null
      }

      setIsSubmitting(true)
      setError(null)
      try {
        const resolvedDecision = await resolveDecisionProposal(repositoryId, proposalId, request)
        setDecision(resolvedDecision)
        setAssimilationRecommendation(null)
        return resolvedDecision
      } catch (resolutionError) {
        setError(formatError(resolutionError))
        return null
      } finally {
        setIsSubmitting(false)
      }
    },
    [proposalId, repositoryId],
  )

  const loadAssimilationRecommendation = useCallback(
    async (decisionId: string) => {
      if (!repositoryId) {
        return null
      }

      setIsAssimilationLoading(true)
      setError(null)
      try {
        const recommendation = await getDecisionAssimilationRecommendation(repositoryId, decisionId)
        setAssimilationRecommendation(recommendation)
        return recommendation
      } catch (assimilationError) {
        setError(formatError(assimilationError))
        setAssimilationRecommendation(null)
        return null
      } finally {
        setIsAssimilationLoading(false)
      }
    },
    [repositoryId],
  )

  const proposeAssimilationRecommendation = useCallback(
    async (
      decisionId: string,
      request: CreateDecisionAssimilationRecommendationCommand,
    ) => {
      if (!repositoryId) {
        return null
      }

      setIsAssimilationLoading(true)
      setError(null)
      try {
        const recommendation = await proposeDecisionOperationalContextAssimilation(
          repositoryId,
          decisionId,
          request,
        )
        setAssimilationRecommendation(recommendation)
        return recommendation
      } catch (assimilationError) {
        setError(formatError(assimilationError))
        return null
      } finally {
        setIsAssimilationLoading(false)
      }
    },
    [repositoryId],
  )

  const reset = useCallback(() => {
    setDecision(null)
    setAssimilationRecommendation(null)
    setError(null)
    setIsSubmitting(false)
    setIsAssimilationLoading(false)
  }, [])

  return {
    decision,
    assimilationRecommendation,
    isSubmitting,
    isAssimilationLoading,
    error,
    resolve,
    loadAssimilationRecommendation,
    proposeAssimilationRecommendation,
    reset,
  }
}
