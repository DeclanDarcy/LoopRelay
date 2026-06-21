import type { ExecutionEvent } from '../types'

export type ExecutionEventSubscription = {
  close: () => void
}

export function subscribeToExecutionEvents(
  backendUrl: string,
  sessionId: string,
  onExecutionEvent: (event: ExecutionEvent) => void,
) {
  const eventSource = new EventSource(
    `${backendUrl}/api/execution-sessions/${sessionId}/events/stream`,
  )

  eventSource.addEventListener('execution-event', (event) => {
    onExecutionEvent(JSON.parse(event.data) as ExecutionEvent)
  })

  eventSource.onerror = () => {
    if (eventSource.readyState === EventSource.CLOSED) {
      return
    }
  }

  return {
    close: () => eventSource.close(),
  } satisfies ExecutionEventSubscription
}
