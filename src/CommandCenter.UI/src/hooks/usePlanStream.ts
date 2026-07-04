import { useCallback, useEffect, useReducer, useRef, useState } from 'react'
import {
  executePlan,
  formatError,
  getBackendUrl,
  revisePlan,
  subscribeToPlanEvents,
  writePlan,
} from '../api'
import {
  initialPlanAuthoringState,
  planAuthoringReducer,
} from '../features/planning/planAuthoringMachine'
import type { PlanStreamEvent, WritePlanRequest } from '../types'

type PlanStreamBridge = {
  subscribe: (repositoryId: string, onPlanEvent: (event: PlanStreamEvent) => void) => () => void
}

declare global {
  interface Window {
    __COMMAND_CENTER_MOCK_PLAN_STREAM__?: PlanStreamBridge
  }
}

export function usePlanStream(repositoryId: string | null) {
  const [state, dispatch] = useReducer(planAuthoringReducer, initialPlanAuthoringState)
  const [backendUrl, setBackendUrl] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  // Client-only transport state, kept off the frozen run-state type: the browser is retrying a
  // dropped stream (isReconnecting) or gave up (transportFailed). Any received frame clears both.
  const [isReconnecting, setIsReconnecting] = useState(false)
  const [transportFailed, setTransportFailed] = useState(false)
  const repositoryIdRef = useRef(repositoryId)

  useEffect(() => {
    repositoryIdRef.current = repositoryId
  }, [repositoryId])

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

    if (!repositoryId || !backendUrl) {
      return
    }

    let isCurrent = true
    const handle = (event: PlanStreamEvent) => {
      if (isCurrent) {
        dispatch({ kind: 'event', event })
      }
    }

    if (backendUrl === 'mock') {
      const bridge = window.__COMMAND_CENTER_MOCK_PLAN_STREAM__
      if (!bridge) {
        return
      }

      const unsubscribe = bridge.subscribe(repositoryId, handle)
      return () => {
        isCurrent = false
        unsubscribe()
      }
    }

    const subscription = subscribeToPlanEvents(backendUrl, repositoryId, handle, {
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
  }, [backendUrl, repositoryId])

  const submitWrite = useCallback(
    async (request: WritePlanRequest) => {
      const targetRepositoryId = repositoryIdRef.current
      if (!targetRepositoryId) {
        return
      }

      setError(null)
      dispatch({ kind: 'write-submitted' })
      try {
        await writePlan(
          targetRepositoryId,
          request.epic,
          request.specs,
          request.newCodebase,
        )
      } catch (writeError) {
        const message = formatError(writeError)
        setError(message)
        dispatch({ kind: 'command-failed', reason: message })
      }
    },
    [],
  )

  const submitRevise = useCallback(async (feedback: string) => {
    const targetRepositoryId = repositoryIdRef.current
    if (!targetRepositoryId) {
      return
    }

    setError(null)
    dispatch({ kind: 'revise-submitted' })
    try {
      await revisePlan(targetRepositoryId, feedback)
    } catch (reviseError) {
      const message = formatError(reviseError)
      setError(message)
      dispatch({ kind: 'command-failed', reason: message })
    }
  }, [])

  const submitExecute = useCallback(async () => {
    const targetRepositoryId = repositoryIdRef.current
    if (!targetRepositoryId) {
      return
    }

    setError(null)
    dispatch({ kind: 'execute-submitted' })
    try {
      await executePlan(targetRepositoryId)
    } catch (executeError) {
      const message = formatError(executeError)
      setError(message)
      dispatch({ kind: 'command-failed', reason: message })
    }
  }, [])

  const dismissFailure = useCallback(() => {
    setError(null)
    dispatch({ kind: 'reset' })
  }, [])

  return {
    state,
    error,
    isReconnecting,
    transportFailed,
    submitWrite,
    submitRevise,
    submitExecute,
    dismissFailure,
  }
}
