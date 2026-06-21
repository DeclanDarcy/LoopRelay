import { useEffect, useState } from 'react'
import { formatError, getBackendUrl, subscribeToExecutionEvents } from '../api'
import type { ExecutionEvent } from '../types'

export function mergeExecutionEvents(
  currentEvents: ExecutionEvent[],
  incomingEvents: ExecutionEvent[],
) {
  const eventsBySequence = new Map<number, ExecutionEvent>()
  currentEvents.forEach((event) => eventsBySequence.set(event.sequence, event))
  incomingEvents.forEach((event) => eventsBySequence.set(event.sequence, event))
  return Array.from(eventsBySequence.values()).sort((left, right) => left.sequence - right.sequence)
}

export function useExecutionEvents(sessionId: string | null) {
  const [data, setData] = useState<ExecutionEvent[]>([])
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
    setData([])
    setError(null)

    if (!sessionId || !backendUrl || backendUrl === 'mock') {
      return
    }

    let isCurrent = true
    const subscription = subscribeToExecutionEvents(backendUrl, sessionId, (executionEvent) => {
      if (isCurrent) {
        setData((currentEvents) => mergeExecutionEvents(currentEvents, [executionEvent]))
      }
    })

    return () => {
      isCurrent = false
      subscription.close()
    }
  }, [backendUrl, sessionId])

  return { data, error }
}
