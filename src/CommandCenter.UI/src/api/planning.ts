import type { ExecutionRunEvent, PlanStatus, PlanStreamEvent, PlanTurnPhase } from '../types'
import { invokeCommand } from './tauri'

// Transport-level callbacks shared by every SSE subscriber. They are client-only signals — no new
// event types, no reducer-state fields on the frozen run types — so the UI can surface a transient
// "Reconnecting…" state and a recoverable transport failure instead of an indefinite spinner.
//
// onReconnecting: the browser is retrying a dropped connection (readyState CONNECTING).
// onError:        the browser gave up (readyState CLOSED); the stream will not recover on its own.
// onActive:       a frame was successfully received, so any reconnecting state should clear.
//
// Note: there is NO backend `cancelled` producer in m9. A lost or aborted turn surfaces here as a
// transport failure (onError) rather than a forged cancellation event.
export type StreamTransportOptions = {
  onReconnecting?: () => void
  onError?: () => void
  onActive?: () => void
}

// How long (ms) a stream may sit in CONNECTING after a mid-stream drop before we give up and
// surface a recoverable transport failure. The browser's default reconnect interval is ~3s, so this
// spans several reconnect cycles: long enough to ride out a brief blip, short enough that a truly
// dead backend doesn't strand the user on an indefinite "Reconnecting…" pill.
const RECONNECT_WINDOW_MS = 12_000

// A single per-connection controller owning both the dedupe gate and the onerror wiring, so the
// bounded reconnect window (armed on a CONNECTING drop, cleared when a frame arrives) is shared
// between them. EventSource auto-reconnects forever on a mid-stream drop — readyState stays
// CONNECTING and never reaches CLOSED — so without this window the transport-failure surface would
// be unreachable for the common case. createStreamGate/attachTransport stay as thin wrappers over
// this for any caller that only needs one half.
export function createTransportController(options?: StreamTransportOptions) {
  let lastSeq = -1
  let windowTimer: ReturnType<typeof setTimeout> | null = null
  let eventSource: EventSource | null = null

  const clearWindow = () => {
    if (windowTimer !== null) {
      clearTimeout(windowTimer)
      windowTimer = null
    }
  }

  // The backend writes `id:{sequence}` on every frame and replays on reconnect. The gate skips
  // replayed frames whose sequence is <= the last seen one (defense-in-depth dedupe). Frames with
  // no parseable id are always delivered, and every delivered frame clears reconnecting via onActive
  // AND cancels the reconnect window — a received frame means the stream recovered. One gate is
  // shared across all event types of a single connection, so the sequence is tracked per-connection.
  const gate = (event: MessageEvent, deliver: (event: MessageEvent) => void) => {
    if (event.lastEventId !== '') {
      const seq = Number(event.lastEventId)
      if (Number.isFinite(seq)) {
        if (seq <= lastSeq) {
          return
        }

        lastSeq = seq
      }
    }

    clearWindow()
    options?.onActive?.()
    deliver(event)
  }

  // Wire the browser's onerror to the client-only transport callbacks. CONNECTING means the browser
  // is retrying (recoverable, show "Reconnecting…"); per the EventSource spec a mid-stream drop
  // stays in CONNECTING and auto-reconnects forever rather than reaching CLOSED, so we arm a bounded
  // window on the FIRST CONNECTING error (never refreshing it — continuous 3s retries must not push
  // it forever) and, if no frame arrives before it expires, close the stream and surface onError.
  // CLOSED (initial-connect failure / explicit close) still maps straight to onError.
  const attach = (source: EventSource) => {
    eventSource = source
    source.onerror = () => {
      if (source.readyState === EventSource.CLOSED) {
        clearWindow()
        options?.onError?.()
        return
      }

      if (source.readyState === EventSource.CONNECTING) {
        options?.onReconnecting?.()
        if (windowTimer === null) {
          windowTimer = setTimeout(() => {
            windowTimer = null
            source.close()
            options?.onError?.()
          }, RECONNECT_WINDOW_MS)
        }
      }
    }
  }

  // Called on subscription teardown alongside the EventSource close, so an armed window can never
  // fire onError on a stream the caller has already abandoned.
  const dispose = () => {
    clearWindow()
    eventSource?.close()
  }

  return { gate, attach, dispose }
}

// Thin wrappers preserved for callers (and tests) that build the gate and onerror wiring
// separately. They each own an independent controller, so the reconnect window is not shared
// between them — the subscribers below use a single controller instead.
export function createStreamGate(options?: StreamTransportOptions) {
  return createTransportController(options).gate
}

export function attachTransport(eventSource: EventSource, options?: StreamTransportOptions) {
  createTransportController(options).attach(eventSource)
}

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
  options?: StreamTransportOptions,
) {
  const eventSource = new EventSource(
    `${backendUrl}/api/repositories/${repositoryId}/plan/stream`,
  )

  const controller = createTransportController(options)
  const on = (type: string, handle: (event: MessageEvent) => void) => {
    eventSource.addEventListener(type, (event) => controller.gate(event, handle))
  }

  on('turn-started', (event) => {
    const data = JSON.parse(event.data) as { phase: PlanTurnPhase }
    onPlanEvent({ type: 'turn-started', phase: data.phase })
  })
  on('delta', (event) => {
    const data = JSON.parse(event.data) as { text: string }
    onPlanEvent({ type: 'delta', text: data.text })
  })
  on('completed', (event) => {
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
  on('failed', (event) => {
    const data = JSON.parse(event.data) as { reason: string; detail?: string }
    onPlanEvent({ type: 'failed', reason: data.reason, detail: data.detail })
  })

  controller.attach(eventSource)

  return {
    close: () => controller.dispose(),
  } satisfies PlanEventSubscription
}

export type ExecutionRunEventSubscription = {
  close: () => void
}

export function subscribeToExecutionRunEvents(
  backendUrl: string,
  repositoryId: string,
  onExecutionEvent: (event: ExecutionRunEvent) => void,
  options?: StreamTransportOptions,
) {
  const eventSource = new EventSource(
    `${backendUrl}/api/repositories/${repositoryId}/execution/stream`,
  )

  const controller = createTransportController(options)
  const on = (type: string, handle: (event: MessageEvent) => void) => {
    eventSource.addEventListener(type, (event) => controller.gate(event, handle))
  }

  on('run-started', () => {
    onExecutionEvent({ type: 'run-started', phase: 'ExecutePlan' })
  })
  on('phase', (event) => {
    const data = JSON.parse(event.data) as { phase: 'ExtractMilestones' | 'StartExecution' }
    onExecutionEvent({ type: 'phase', phase: data.phase })
  })
  on('delta', (event) => {
    const data = JSON.parse(event.data) as { phase: string; text: string }
    onExecutionEvent({ type: 'delta', phase: data.phase, text: data.text })
  })
  on('milestones-extracted', (event) => {
    const data = JSON.parse(event.data) as { count: number }
    onExecutionEvent({ type: 'milestones-extracted', count: data.count })
  })
  on('committed', (event) => {
    const data = JSON.parse(event.data) as { commitSha: string | null; pushed: boolean }
    onExecutionEvent({ type: 'committed', commitSha: data.commitSha, pushed: data.pushed })
  })
  on('lifecycle', (event) => {
    const data = JSON.parse(event.data) as { state: 'ExecutingPlan' }
    onExecutionEvent({ type: 'lifecycle', state: data.state })
  })
  on('handoff-rotated', (event) => {
    const data = JSON.parse(event.data) as { sequence: number; path: string }
    onExecutionEvent({ type: 'handoff-rotated', sequence: data.sequence, path: data.path })
  })
  on('completed', (event) => {
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
  on('failed', (event) => {
    const data = JSON.parse(event.data) as { phase?: string; reason: string; detail?: string }
    onExecutionEvent({ type: 'failed', phase: data.phase, reason: data.reason, detail: data.detail })
  })

  controller.attach(eventSource)

  return {
    close: () => controller.dispose(),
  } satisfies ExecutionRunEventSubscription
}
