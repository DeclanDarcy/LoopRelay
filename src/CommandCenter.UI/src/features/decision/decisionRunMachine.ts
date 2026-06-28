import type { DecisionRunEvent, DecisionRunPhase, DecisionRunState } from '../../types'

export const initialDecisionRunState: DecisionRunState = {
  status: 'Idle',
  phase: null,
  streamedText: '',
  diagnostics: null,
  proposedDecisions: null,
  editableDecisions: null,
  completion: null,
  submittedPath: null,
  failure: null,
}

export type DecisionRunAction =
  | { kind: 'event'; event: DecisionRunEvent }
  // The reviewer edits the captured decisions in place before submitting them.
  | { kind: 'edit'; decisions: string }
  | { kind: 'reset' }

export function decisionRunReducer(
  state: DecisionRunState,
  action: DecisionRunAction,
): DecisionRunState {
  switch (action.kind) {
    case 'reset':
      return initialDecisionRunState
    case 'edit':
      // Editing is only meaningful once the review gate is open; before review-ready there is
      // no editable buffer to write into, so the edit is ignored.
      if (state.editableDecisions === null) {
        return state
      }

      return { ...state, editableDecisions: action.decisions }
    case 'event':
      return reduceEvent(state, action.event)
    default:
      return state
  }
}

function reduceEvent(state: DecisionRunState, event: DecisionRunEvent): DecisionRunState {
  switch (event.type) {
    case 'run-started':
      // Begin a fresh run. Re-entry resets accumulated output and any prior review buffer.
      return {
        ...initialDecisionRunState,
        status: 'Running',
        phase: 'DecisionRun',
      }
    case 'diagnostics':
      // The sandbox config is validated and logged here; the seed turn itself is not streamed.
      return {
        ...runningGuard(state),
        diagnostics: {
          sandbox: event.sandbox,
          approvals: event.approvals,
          seeded: event.seeded,
        },
      }
    case 'phase':
      return runningWithPhase(state, event.phase)
    case 'delta':
      // Accumulate streamed output. A delta arriving before run-started still lands, so the
      // surface stays correct even if the run-started frame is missed on replay.
      return {
        ...state,
        status: isTerminal(state) ? state.status : 'Running',
        streamedText: state.streamedText + event.text,
      }
    case 'completed':
      // The proposing turn finished. The run is not terminal yet — the review gate
      // (review-ready) opens next and submission is still ahead — but completion is recorded.
      return {
        ...state,
        status: isTerminal(state) ? state.status : 'Running',
        phase: null,
        completion: {
          promptTokens: event.promptTokens,
          outputTokens: event.outputTokens,
        },
      }
    case 'review-ready':
      // The human review gate opens. The captured text becomes editable for the first time;
      // the editable buffer is prefilled with it and left for the reviewer to change.
      if (state.status === 'Submitted' || state.status === 'Failed') {
        return state
      }

      return {
        ...state,
        status: 'Completed',
        phase: null,
        failure: null,
        proposedDecisions: event.decisions,
        editableDecisions: event.decisions,
      }
    case 'submitted':
      // The edited decisions were persisted. This is terminal.
      return {
        ...state,
        status: 'Submitted',
        phase: null,
        failure: null,
        submittedPath: event.path,
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

function isTerminal(state: DecisionRunState): boolean {
  return state.status === 'Submitted' || state.status === 'Failed'
}

// A non-terminal event keeps the run live without overwriting a terminal status. Completed is
// not terminal here: the run only ends at Submitted or Failed, so a diagnostics/phase frame
// replayed after completion may legitimately reassert Running.
function runningGuard(state: DecisionRunState): DecisionRunState {
  if (isTerminal(state)) {
    return state
  }

  return { ...state, status: 'Running' }
}

function runningWithPhase(state: DecisionRunState, phase: DecisionRunPhase): DecisionRunState {
  if (isTerminal(state)) {
    return { ...state, phase }
  }

  return { ...state, status: 'Running', phase }
}
