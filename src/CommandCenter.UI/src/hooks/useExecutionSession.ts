import { useCallback, useEffect, useRef, useState } from 'react'
import { formatError, getBackendUrl, getExecutionStatus } from '../api'
import type { ExecutionStatus } from '../types'

type RefreshOptions = {
  silent?: boolean
}

export function useExecutionSession(repositoryId: string | null, sessionId: string | null) {
  const [data, setData] = useState<ExecutionStatus | null>(null)
  const [backendUrl, setBackendUrl] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const latestSelection = useRef({ repositoryId, sessionId })
  const requestSequence = useRef(0)

  useEffect(() => {
    latestSelection.current = { repositoryId, sessionId }
  }, [repositoryId, sessionId])

  useEffect(() => {
    let isCurrent = true

    getBackendUrl()
      .then((url) => {
        if (isCurrent) {
          setBackendUrl(url)
        }
      })
      .catch((backendUrlError) => {
        if (isCurrent) {
          setError(formatError(backendUrlError))
        }
      })

    return () => {
      isCurrent = false
    }
  }, [])

  const refresh = useCallback(
    async (options: RefreshOptions = {}) => {
      const requestedRepositoryId = repositoryId
      const requestedSessionId = sessionId
      const requestId = requestSequence.current + 1
      requestSequence.current = requestId

      if (!requestedRepositoryId || !requestedSessionId || !backendUrl || backendUrl === 'mock') {
        setData(null)
        if (!options.silent) {
          setIsLoading(false)
        }
        return null
      }

      if (!options.silent) {
        setIsLoading(true)
        setError(null)
      }

      try {
        const status = await getExecutionStatus(backendUrl, requestedSessionId)
        const currentSelection = latestSelection.current
        if (
          requestSequence.current === requestId &&
          currentSelection.repositoryId === requestedRepositoryId &&
          currentSelection.sessionId === requestedSessionId
        ) {
          setData(status)
        }
        return status
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
    [backendUrl, repositoryId, sessionId],
  )

  useEffect(() => {
    if (!repositoryId || !sessionId) {
      requestSequence.current += 1
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
  }, [refresh, repositoryId, sessionId])

  return { data, setData, backendUrl, isLoading, error, refresh, load: refresh }
}
