import type { DecisionRunEvent } from '../types'
import { invokeCommand } from './tauri'

// The decision run mirrors the execution run: the POSTs reach the backend through Tauri proxy
// commands (decision_run / decision_submit), while the SSE stream is consumed via EventSource
// directly. The UI carries the human's edited decisions text only — it never composes prompts.

export function startDecisionRun(repositoryId: string) {
  return invokeCommand<{ phase: string }>('decision_run', { repositoryId })
}

export function submitDecisions(repositoryId: string, decisions: string) {
  return invokeCommand<{ phase: string }>('decision_submit', { repositoryId, decisions })
}

export type DecisionRunEventSubscription = {
  close: () => void
}

export function subscribeToDecisionRunEvents(
  backendUrl: string,
  repositoryId: string,
  onDecisionEvent: (event: DecisionRunEvent) => void,
) {
  const eventSource = new EventSource(
    `${backendUrl}/api/repositories/${repositoryId}/decision/stream`,
  )

  eventSource.addEventListener('run-started', () => {
    onDecisionEvent({ type: 'run-started', phase: 'DecisionRun' })
  })
  eventSource.addEventListener('diagnostics', (event) => {
    const data = JSON.parse(event.data) as { sandbox: string; approvals: string; seeded: boolean }
    onDecisionEvent({
      type: 'diagnostics',
      sandbox: data.sandbox,
      approvals: data.approvals,
      seeded: data.seeded,
    })
  })
  eventSource.addEventListener('phase', (event) => {
    const data = JSON.parse(event.data) as { phase: 'GetNextDecisions' }
    onDecisionEvent({ type: 'phase', phase: data.phase })
  })
  eventSource.addEventListener('delta', (event) => {
    const data = JSON.parse(event.data) as { text: string }
    onDecisionEvent({ type: 'delta', text: data.text })
  })
  eventSource.addEventListener('completed', (event) => {
    const data = JSON.parse(event.data) as { promptTokens: number; outputTokens: number }
    onDecisionEvent({
      type: 'completed',
      promptTokens: data.promptTokens,
      outputTokens: data.outputTokens,
    })
  })
  eventSource.addEventListener('review-ready', (event) => {
    const data = JSON.parse(event.data) as { decisions: string }
    onDecisionEvent({ type: 'review-ready', decisions: data.decisions })
  })
  eventSource.addEventListener('submitted', (event) => {
    const data = JSON.parse(event.data) as { path: string }
    onDecisionEvent({ type: 'submitted', path: data.path })
  })
  eventSource.addEventListener('failed', (event) => {
    const data = JSON.parse(event.data) as { phase?: string; reason: string; detail?: string }
    onDecisionEvent({ type: 'failed', phase: data.phase, reason: data.reason, detail: data.detail })
  })

  eventSource.onerror = () => {
    if (eventSource.readyState === EventSource.CLOSED) {
      return
    }
  }

  return {
    close: () => eventSource.close(),
  } satisfies DecisionRunEventSubscription
}
