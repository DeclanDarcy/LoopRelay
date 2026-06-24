import { useCallback, useEffect, useState } from 'react'
import {
  dismissDecisionCandidate,
  discoverDecisions,
  expireDecisionCandidate,
  formatError,
  listDecisionCandidates,
  markDecisionCandidateDuplicate,
  promoteDecisionCandidate,
} from '../api'
import type { DecisionCandidate, DecisionCandidateTransitionRequest } from '../types'

export function useDecisionDiscovery(repositoryId: string | null) {
  const [data, setData] = useState<DecisionCandidate[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [isMutating, setIsMutating] = useState(false)
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

  const discover = useCallback(() => runMutation(() => discoverDecisions(repositoryId as string)), [
    repositoryId,
    runMutation,
  ])

  const promote = useCallback((candidateId: string, request: DecisionCandidateTransitionRequest = {}) =>
    runMutation(() => promoteDecisionCandidate(repositoryId as string, candidateId, request)), [
    repositoryId,
    runMutation,
  ])

  const dismiss = useCallback((candidateId: string, request: DecisionCandidateTransitionRequest = {}) =>
    runMutation(() => dismissDecisionCandidate(repositoryId as string, candidateId, request)), [
    repositoryId,
    runMutation,
  ])

  const expire = useCallback((candidateId: string, request: DecisionCandidateTransitionRequest = {}) =>
    runMutation(() => expireDecisionCandidate(repositoryId as string, candidateId, request)), [
    repositoryId,
    runMutation,
  ])

  const markDuplicate = useCallback((candidateId: string, request: DecisionCandidateTransitionRequest) =>
    runMutation(() => markDecisionCandidateDuplicate(repositoryId as string, candidateId, request)), [
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
    discover,
    promote,
    dismiss,
    expire,
    markDuplicate,
  }
}
