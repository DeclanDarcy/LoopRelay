import { useCallback, useEffect, useState } from 'react'
import { formatError, getBoundaryViolation, listReasoningEvents } from '../api'
import type { BoundaryViolationProjection, ReasoningEvent } from '../types'

export function useReasoningEvents(repositoryId: string | null) {
  const [data, setData] = useState<ReasoningEvent[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [boundaryViolation, setBoundaryViolation] = useState<BoundaryViolationProjection | null>(null)

  const refresh = useCallback(async () => {
    if (!repositoryId) {
      setData([])
      setIsLoading(false)
      return []
    }

    setIsLoading(true)
    setError(null)
    setBoundaryViolation(null)
    try {
      const events = await listReasoningEvents(repositoryId)
      setData(events)
      return events
    } catch (eventsError) {
      const message = formatError(eventsError)
      setData([])
      setError(message)
      setBoundaryViolation(getBoundaryViolation(eventsError))
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
        setBoundaryViolation(null)
      }, 0)

      return () => window.clearTimeout(timeoutId)
    }

    const timeoutId = window.setTimeout(() => {
      void refresh().catch(() => undefined)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [refresh, repositoryId])

  return { data, setData, isLoading, error, boundaryViolation, refresh, load: refresh }
}
