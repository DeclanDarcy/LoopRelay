import type { ExecutionRunEvent, PlanStatus, PlanStreamEvent, PlanTurnPhase } from '../types'
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

export type ExecutionRunEventSubscription = {
  close: () => void
}

export function subscribeToExecutionRunEvents(
  backendUrl: string,
  repositoryId: string,
  onExecutionEvent: (event: ExecutionRunEvent) => void,
) {
  const eventSource = new EventSource(
    `${backendUrl}/api/repositories/${repositoryId}/execution/stream`,
  )

  eventSource.addEventListener('run-started', () => {
    onExecutionEvent({ type: 'run-started', phase: 'ExecutePlan' })
  })
  eventSource.addEventListener('phase', (event) => {
    const data = JSON.parse(event.data) as { phase: 'ExtractMilestones' | 'StartExecution' }
    onExecutionEvent({ type: 'phase', phase: data.phase })
  })
  eventSource.addEventListener('delta', (event) => {
    const data = JSON.parse(event.data) as { phase: string; text: string }
    onExecutionEvent({ type: 'delta', phase: data.phase, text: data.text })
  })
  eventSource.addEventListener('milestones-extracted', (event) => {
    const data = JSON.parse(event.data) as { count: number }
    onExecutionEvent({ type: 'milestones-extracted', count: data.count })
  })
  eventSource.addEventListener('committed', (event) => {
    const data = JSON.parse(event.data) as { commitSha: string | null; pushed: boolean }
    onExecutionEvent({ type: 'committed', commitSha: data.commitSha, pushed: data.pushed })
  })
  eventSource.addEventListener('lifecycle', (event) => {
    const data = JSON.parse(event.data) as { state: 'ExecutingPlan' }
    onExecutionEvent({ type: 'lifecycle', state: data.state })
  })
  eventSource.addEventListener('handoff-rotated', (event) => {
    const data = JSON.parse(event.data) as { sequence: number; path: string }
    onExecutionEvent({ type: 'handoff-rotated', sequence: data.sequence, path: data.path })
  })
  eventSource.addEventListener('completed', (event) => {
    const data = JSON.parse(event.data) as {
      commitSha: string | null
      milestoneCount: number
      handoffPath: string
      promptTokens: number
      outputTokens: number
    }
    onExecutionEvent({
      type: 'completed',
      commitSha: data.commitSha,
      milestoneCount: data.milestoneCount,
      handoffPath: data.handoffPath,
      promptTokens: data.promptTokens,
      outputTokens: data.outputTokens,
    })
  })
  eventSource.addEventListener('failed', (event) => {
    const data = JSON.parse(event.data) as { phase?: string; reason: string; detail?: string }
    onExecutionEvent({ type: 'failed', phase: data.phase, reason: data.reason, detail: data.detail })
  })

  eventSource.onerror = () => {
    if (eventSource.readyState === EventSource.CLOSED) {
      return
    }
  }

  return {
    close: () => eventSource.close(),
  } satisfies ExecutionRunEventSubscription
}
