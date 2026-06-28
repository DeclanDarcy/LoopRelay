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
  // Client-only transport state, kept off the frozen run-state type: the browser is retrying a
  // dropped stream (isReconnecting) or gave up (transportFailed). Any received frame clears both.
  const [isReconnecting, setIsReconnecting] = useState(false)
  const [transportFailed, setTransportFailed] = useState(false)

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
    setIsReconnecting(false)
    setTransportFailed(false)

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

    const subscription = subscribeToExecutionRunEvents(backendUrl, repositoryId, handle, {
      onReconnecting: () => isCurrent && setIsReconnecting(true),
      onError: () => isCurrent && setTransportFailed(true),
      // Any successfully received frame means the stream is live again: clear both flags.
      onActive: () => {
        if (isCurrent) {
          setIsReconnecting(false)
          setTransportFailed(false)
        }
      },
    })
    return () => {
      isCurrent = false
      subscription.close()
    }
  }, [active, backendUrl, repositoryId])

  return { state, error, isReconnecting, transportFailed }
}
