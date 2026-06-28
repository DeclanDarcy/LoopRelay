import { useEffect, useReducer, useState } from 'react'
import { formatError, getBackendUrl, subscribeToExecutionRunEvents } from '../api'
import {
  executionRunReducer,
  initialExecutionRunState,
} from '../features/planning/executionRunMachine'
import type { ExecutionRunEvent } from '../types'

type ExecutionStreamBridge = {
  subscribe: (
    repositoryId: string,
    onExecutionEvent: (event: ExecutionRunEvent) => void,
  ) => () => void
}

declare global {
  interface Window {
    __COMMAND_CENTER_MOCK_EXECUTION_STREAM__?: ExecutionStreamBridge
  }
}

export function useExecutionStream(repositoryId: string | null, active: boolean) {
  const [state, dispatch] = useReducer(executionRunReducer, initialExecutionRunState)
  const [backendUrl, setBackendUrl] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

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

  useEffect(() => {
    dispatch({ kind: 'reset' })
    setError(null)

    if (!repositoryId || !backendUrl || !active) {
      return
    }

    let isCurrent = true
    const handle = (event: ExecutionRunEvent) => {
      if (isCurrent) {
        dispatch({ kind: 'event', event })
      }
    }

    if (backendUrl === 'mock') {
      const bridge = window.__COMMAND_CENTER_MOCK_EXECUTION_STREAM__
      if (!bridge) {
        return
      }

      const unsubscribe = bridge.subscribe(repositoryId, handle)
      return () => {
        isCurrent = false
        unsubscribe()
      }
    }

    const subscription = subscribeToExecutionRunEvents(backendUrl, repositoryId, handle)
    return () => {
      isCurrent = false
      subscription.close()
    }
  }, [active, backendUrl, repositoryId])

  return { state, error }
}
