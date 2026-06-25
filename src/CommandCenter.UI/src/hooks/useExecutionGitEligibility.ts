import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { formatError, getExecutionGitEligibility } from '../api'
import type { ExecutionGitActionEligibility } from '../types'

type UseExecutionGitEligibilityInput = {
  sessionId: string | null
  commitMessage: string
  selectedPaths: string[]
}

export function useExecutionGitEligibility({
  sessionId,
  commitMessage,
  selectedPaths,
}: UseExecutionGitEligibilityInput) {
  const [data, setData] = useState<ExecutionGitActionEligibility | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const requestSequence = useRef(0)
  const stableSelectedPaths = useMemo(
    () => [...selectedPaths].sort((left, right) => left.localeCompare(right)).join('\n'),
    [selectedPaths],
  )

  const refresh = useCallback(async () => {
    const requestedSessionId = sessionId
    const requestId = requestSequence.current + 1
    requestSequence.current = requestId

    if (!requestedSessionId) {
      setData(null)
      setIsLoading(false)
      setError(null)
      return null
    }

    setIsLoading(true)
    setError(null)
    try {
      const eligibility = await getExecutionGitEligibility(
        requestedSessionId,
        commitMessage,
        stableSelectedPaths.length > 0 ? stableSelectedPaths.split('\n') : [],
      )
      if (requestSequence.current === requestId) {
        setData(eligibility)
      }
      return eligibility
    } catch (eligibilityError) {
      if (requestSequence.current === requestId) {
        setError(formatError(eligibilityError))
        setData(null)
      }
      return null
    } finally {
      if (requestSequence.current === requestId) {
        setIsLoading(false)
      }
    }
  }, [commitMessage, sessionId, stableSelectedPaths])

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void refresh().catch(() => undefined)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [refresh])

  return { data, setData, isLoading, error, refresh, load: refresh }
}
