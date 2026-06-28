import { useCallback, useEffect, useReducer, useRef, useState } from 'react'
import {
  formatError,
  getBackendUrl,
  startDecisionRun,
  submitDecisions,
  subscribeToDecisionRunEvents,
} from '../api'
import {
  decisionRunReducer,
  initialDecisionRunState,
} from '../features/decision/decisionRunMachine'
import type { DecisionRunEvent } from '../types'

type DecisionStreamBridge = {
  subscribe: (
    repositoryId: string,
    onDecisionEvent: (event: DecisionRunEvent) => void,
  ) => () => void
}

declare global {
  interface Window {
    __COMMAND_CENTER_MOCK_DECISION_STREAM__?: DecisionStreamBridge
  }
}

export function useDecisionStream(repositoryId: string | null, active: boolean) {
  const [state, dispatch] = useReducer(decisionRunReducer, initialDecisionRunState)
  const [backendUrl, setBackendUrl] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
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

    if (!repositoryId || !backendUrl || !active) {
      return
    }

    let isCurrent = true
    const handle = (event: DecisionRunEvent) => {
      if (isCurrent) {
        dispatch({ kind: 'event', event })
      }
    }

    if (backendUrl === 'mock') {
      const bridge = window.__COMMAND_CENTER_MOCK_DECISION_STREAM__
      if (!bridge) {
        return
      }

      const unsubscribe = bridge.subscribe(repositoryId, handle)
      return () => {
        isCurrent = false
        unsubscribe()
      }
    }

    const subscription = subscribeToDecisionRunEvents(backendUrl, repositoryId, handle)
    return () => {
      isCurrent = false
      subscription.close()
    }
  }, [active, backendUrl, repositoryId])

  // A "Generate decisions" action triggers the background run; the result streams back over the
  // subscription above, so nothing is dispatched here on success.
  const generateDecisions = useCallback(async () => {
    const targetRepositoryId = repositoryIdRef.current
    if (!targetRepositoryId) {
      return
    }

    setError(null)
    try {
      await startDecisionRun(targetRepositoryId)
    } catch (runError) {
      const message = formatError(runError)
      setError(message)
      dispatch({ kind: 'event', event: { type: 'failed', reason: message } })
    }
  }, [])

  // The reviewer edits the captured decisions in place; the buffer is owned by the reducer.
  const editDecisions = useCallback((decisions: string) => {
    dispatch({ kind: 'edit', decisions })
  }, [])

  // Submitting persists the (possibly edited) decisions through the human-review gate. The
  // backend confirms with a `submitted` SSE frame, which lands the terminal state.
  const submitReviewedDecisions = useCallback(async (decisions: string) => {
    const targetRepositoryId = repositoryIdRef.current
    if (!targetRepositoryId) {
      return
    }

    setError(null)
    try {
      await submitDecisions(targetRepositoryId, decisions)
    } catch (submitError) {
      const message = formatError(submitError)
      setError(message)
      dispatch({ kind: 'event', event: { type: 'failed', phase: 'SubmitDecisions', reason: message } })
    }
  }, [])

  return { state, error, generateDecisions, editDecisions, submitReviewedDecisions }
}
