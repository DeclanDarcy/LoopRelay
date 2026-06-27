import type { PlanStatus, PlanStreamEvent, PlanTurnPhase } from '../types'
import { invokeCommand } from './tauri'

export function getPlanStatus(repositoryId: string) {
  return invokeCommand<PlanStatus>('get_plan_status', { repositoryId })
}

export function writePlan(repositoryId: string, roadmap: string, specs: string[], newCodebase: boolean) {
  return invokeCommand<{ phase: string }>('write_plan', {
    repositoryId,
    roadmap,
    specs,
    newCodebase,
  })
}

export function revisePlan(repositoryId: string, feedback: string) {
  return invokeCommand<{ phase: string }>('revise_plan', { repositoryId, feedback })
}

export function executePlan(repositoryId: string) {
  return invokeCommand<{ phase: string }>('execute_plan', { repositoryId })
}

export type PlanEventSubscription = {
  close: () => void
}

export function subscribeToPlanEvents(
  backendUrl: string,
  repositoryId: string,
  onPlanEvent: (event: PlanStreamEvent) => void,
) {
  const eventSource = new EventSource(
    `${backendUrl}/api/repositories/${repositoryId}/plan/stream`,
  )

  eventSource.addEventListener('turn-started', (event) => {
    const data = JSON.parse(event.data) as { phase: PlanTurnPhase }
    onPlanEvent({ type: 'turn-started', phase: data.phase })
  })
  eventSource.addEventListener('delta', (event) => {
    const data = JSON.parse(event.data) as { text: string }
    onPlanEvent({ type: 'delta', text: data.text })
  })
  eventSource.addEventListener('completed', (event) => {
    const data = JSON.parse(event.data) as {
      plan: string
      promptTokens: number
      outputTokens: number
    }
    onPlanEvent({
      type: 'completed',
      plan: data.plan,
      promptTokens: data.promptTokens,
      outputTokens: data.outputTokens,
    })
  })
  eventSource.addEventListener('failed', (event) => {
    const data = JSON.parse(event.data) as { reason: string; detail?: string }
    onPlanEvent({ type: 'failed', reason: data.reason, detail: data.detail })
  })

  eventSource.onerror = () => {
    if (eventSource.readyState === EventSource.CLOSED) {
      return
    }
  }

  return {
    close: () => eventSource.close(),
  } satisfies PlanEventSubscription
}
