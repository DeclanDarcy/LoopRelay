import type { ExecutionRunEvent, ExecutionRunPhase, ExecutionRunState } from '../../types'

export const initialExecutionRunState: ExecutionRunState = {
  status: 'Idle',
  phase: null,
  streamedText: '',
  milestoneCount: null,
  commit: null,
  handoff: null,
  completion: null,
  failure: null,
}

export type ExecutionRunAction =
  | { kind: 'event'; event: ExecutionRunEvent }
  | { kind: 'reset' }

export function executionRunReducer(
  state: ExecutionRunState,
  action: ExecutionRunAction,
): ExecutionRunState {
  switch (action.kind) {
    case 'reset':
      return initialExecutionRunState
    case 'event':
      return reduceEvent(state, action.event)
    default:
      return state
  }
}

function reduceEvent(state: ExecutionRunState, event: ExecutionRunEvent): ExecutionRunState {
  switch (event.type) {
    case 'run-started':
      // Begin a fresh run. Re-entry resets accumulated output without dropping a plan.
      return {
        ...initialExecutionRunState,
        status: 'Running',
        phase: 'ExecutePlan',
      }
    case 'phase':
      return runningWithPhase(state, event.phase)
    case 'delta':
      // Accumulate streamed output. A delta arriving before run-started still lands,
      // so the surface stays correct even if the run-started frame is missed on replay.
      return {
        ...state,
        status: state.status === 'Completed' || state.status === 'Failed' ? state.status : 'Running',
        streamedText: state.streamedText + event.text,
      }
    case 'milestones-extracted':
      return {
        ...runningGuard(state),
        milestoneCount: event.count,
      }
    case 'committed':
      return {
        ...runningGuard(state),
        commit: { commitSha: event.commitSha, pushed: event.pushed },
      }
    case 'lifecycle':
      // The lifecycle frame confirms the repository entered ExecutingPlan; it carries no
      // phase of its own, so keep the current indicator running.
      return runningGuard(state)
    case 'handoff-rotated':
      return {
        ...runningGuard(state),
        handoff: { sequence: event.sequence, path: event.path },
      }
    case 'completed':
      return {
        ...state,
        status: 'Completed',
        phase: null,
        failure: null,
        milestoneCount: event.milestoneCount,
        handoff: state.handoff ?? { sequence: 0, path: event.handoffPath },
        completion: {
          commitSha: event.commitSha,
          milestoneCount: event.milestoneCount,
          handoffPath: event.handoffPath,
          promptTokens: event.promptTokens,
          outputTokens: event.outputTokens,
        },
      }
    case 'failed':
      // Failure always wins, regardless of the phase it arrives in.
      return {
        ...state,
        status: 'Failed',
        phase: null,
        failure: {
          phase: event.phase ?? null,
          reason: event.reason,
          detail: event.detail ?? null,
        },
      }
    default:
      return state
  }
}

// A non-terminal event keeps the run live without overwriting a terminal status.
function runningGuard(state: ExecutionRunState): ExecutionRunState {
  if (state.status === 'Completed' || state.status === 'Failed') {
    return state
  }

  return { ...state, status: 'Running' }
}

function runningWithPhase(state: ExecutionRunState, phase: ExecutionRunPhase): ExecutionRunState {
  if (state.status === 'Completed' || state.status === 'Failed') {
    return { ...state, phase }
  }

  return { ...state, status: 'Running', phase }
}
