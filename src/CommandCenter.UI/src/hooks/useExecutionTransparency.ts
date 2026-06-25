import { useCallback, useEffect, useRef, useState } from 'react'
import { formatError, getExecutionTransparency } from '../api'
import type { ExecutionSessionTransparency } from '../types'

type RefreshOptions = {
  silent?: boolean
}

export function useExecutionTransparency(sessionId: string | null) {
  const [data, setData] = useState<ExecutionSessionTransparency | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const latestSessionId = useRef(sessionId)
  const requestSequence = useRef(0)

  useEffect(() => {
    latestSessionId.current = sessionId
  }, [sessionId])

  const refresh = useCallback(
    async (options: RefreshOptions = {}) => {
      const requestedSessionId = sessionId
      const requestId = requestSequence.current + 1
      requestSequence.current = requestId

      if (!requestedSessionId) {
        setData(null)
        if (!options.silent) {
          setIsLoading(false)
          setError(null)
        }
        return null
      }

      if (!options.silent) {
        setIsLoading(true)
        setError(null)
      }

      try {
        const transparency = await getExecutionTransparency(requestedSessionId)
        if (requestSequence.current === requestId && latestSessionId.current === requestedSessionId) {
          setData(transparency)
        }
        return transparency
      } catch (loadError) {
        if (!options.silent && requestSequence.current === requestId) {
          setError(formatError(loadError))
        }
        return null
      } finally {
        if (!options.silent && requestSequence.current === requestId) {
          setIsLoading(false)
        }
      }
    },
    [sessionId],
  )

  useEffect(() => {
    if (!sessionId) {
      requestSequence.current += 1
      const timeoutId = window.setTimeout(() => {
        setData(null)
        setIsLoading(false)
        setError(null)
      }, 0)

      return () => window.clearTimeout(timeoutId)
    }

    const timeoutId = window.setTimeout(() => {
      void refresh().catch(() => undefined)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [refresh, sessionId])

  return { data, setData, isLoading, error, refresh, load: refresh }
}
