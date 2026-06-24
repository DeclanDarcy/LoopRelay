import { useCallback, useEffect, useState } from 'react'
import { formatError, getDecisionLifecycleEligibility } from '../api'
import type { DecisionLifecycleEligibilityProjection } from '../types'

export function useDecisionLifecycleEligibility(repositoryId: string | null) {
  const [data, setData] = useState<DecisionLifecycleEligibilityProjection | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    if (!repositoryId) {
      setData(null)
      setIsLoading(false)
      return null
    }

    setIsLoading(true)
    setError(null)
    try {
      const eligibility = await getDecisionLifecycleEligibility(repositoryId)
      setData(eligibility)
      return eligibility
    } catch (eligibilityError) {
      const message = formatError(eligibilityError)
      setData(null)
      setError(message)
      return null
    } finally {
      setIsLoading(false)
    }
  }, [repositoryId])

  useEffect(() => {
    if (!repositoryId) {
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
  }, [refresh, repositoryId])

  return {
    data,
    setData,
    isLoading,
    error,
    refresh,
    load: refresh,
  }
}
