import type { DecisionRunEvent } from '../types'
import { createTransportController, type StreamTransportOptions } from './planning'
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
  options?: StreamTransportOptions,
) {
  const eventSource = new EventSource(
    `${backendUrl}/api/repositories/${repositoryId}/decision/stream`,
  )

  const controller = createTransportController(options)
  const on = (type: string, handle: (event: MessageEvent) => void) => {
    eventSource.addEventListener(type, (event) => controller.gate(event, handle))
  }

  // Note: `route`/`transferred`/`numberedPath` are not parsed here — the decision transfer and
  // numbered-path surface is mock-only until the backend producer is wired (pre-existing wire gap,
  // predates m9). Do not add parsing in this pass.
  on('run-started', () => {
    onDecisionEvent({ type: 'run-started', phase: 'DecisionRun' })
  })
  on('diagnostics', (event) => {
    const data = JSON.parse(event.data) as { sandbox: string; approvals: string; seeded: boolean }
    onDecisionEvent({
      type: 'diagnostics',
      sandbox: data.sandbox,
      approvals: data.approvals,
      seeded: data.seeded,
    })
  })
  on('phase', (event) => {
    const data = JSON.parse(event.data) as { phase: 'GetNextDecisions' }
    onDecisionEvent({ type: 'phase', phase: data.phase })
  })
  on('delta', (event) => {
    const data = JSON.parse(event.data) as { text: string }
    onDecisionEvent({ type: 'delta', text: data.text })
  })
  on('completed', (event) => {
    const data = JSON.parse(event.data) as { promptTokens: number; outputTokens: number }
    onDecisionEvent({
      type: 'completed',
      promptTokens: data.promptTokens,
      outputTokens: data.outputTokens,
    })
  })
  on('review-ready', (event) => {
    const data = JSON.parse(event.data) as { decisions: string }
    onDecisionEvent({ type: 'review-ready', decisions: data.decisions })
  })
  on('submitted', (event) => {
    const data = JSON.parse(event.data) as { path: string }
    onDecisionEvent({ type: 'submitted', path: data.path })
  })
  on('failed', (event) => {
    const data = JSON.parse(event.data) as { phase?: string; reason: string; detail?: string }
    onDecisionEvent({ type: 'failed', phase: data.phase, reason: data.reason, detail: data.detail })
  })

  controller.attach(eventSource)

  return {
    close: () => controller.dispose(),
  } satisfies DecisionRunEventSubscription
}
