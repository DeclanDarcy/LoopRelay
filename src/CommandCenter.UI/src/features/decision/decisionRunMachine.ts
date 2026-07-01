import type {
  DecisionRunEvent,
  DecisionRunPhase,
  DecisionRunState,
  DecisionRunTransferPhase,
} from '../../types'

export const initialDecisionRunState: DecisionRunState = {
  status: 'Idle',
  phase: null,
  streamedText: '',
  diagnostics: null,
  proposedDecisions: null,
  editableDecisions: null,
  completion: null,
  submittedPath: null,
  submittedNumberedPath: null,
  submittedSequence: null,
  iteration: 0,
  transferring: false,
  failure: null,
}

// The four preparatory phases a Transfer-routed run streams before the proposal. They flag the
// transfer indicator but never become the labelled `phase`, so the Continue path is untouched.
const TRANSFER_PHASES: readonly DecisionRunTransferPhase[] = [
  'ProduceOperationalDelta',
  'UpdateOperationalContext',
  'ArchiveOperationalDelta',
  'StartDecisionSessionFromTransfer',
]

function isTransferPhase(phase: string): phase is DecisionRunTransferPhase {
  return (TRANSFER_PHASES as readonly string[]).includes(phase)
}

export type DecisionRunAction =
  | { kind: 'event'; event: DecisionRunEvent }
  // The reviewer edits the captured decisions in place before submitting them.
  | { kind: 'edit'; decisions: string }
  // The reviewer submits the edited decisions; the gate closes optimistically while the backend
  // persists them and starts the continuation turn.
  | { kind: 'submit' }
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
    case 'submit':
      // Submitting optimistically closes the gate while the backend persists the decisions and
      // runs the continuation turn. Ignore submit unless the gate is actually open.
      if (state.editableDecisions === null || isTerminal(state)) {
        return state
      }

      return { ...state, status: 'Submitting', phase: null, editableDecisions: null }
    case 'event':
      return reduceEvent(state, action.event)
    default:
      return state
  }
}

function reduceEvent(state: DecisionRunState, event: DecisionRunEvent): DecisionRunState {
  switch (event.type) {
    case 'run-started':
      // Begin a fresh run. Re-entry resets accumulated output and any prior review buffer. In the
      // continuation loop this is also the auto-started next decision run, so it advances the
      // iteration and reopens streaming after a submit. A Transfer-routed run raises the transfer
      // indicator straight away; Continue (or a routeless legacy frame) leaves it down.
      return {
        ...initialDecisionRunState,
        status: 'Running',
        phase: 'DecisionRun',
        iteration: state.iteration + 1,
        transferring: event.route === 'Transfer',
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
      // The four transfer phases only flag the transfer indicator; they never become the labelled
      // `phase`, so the proposing-phase label and the warm path are unchanged. GetNextDecisions is
      // the normal proposing phase and flows through as before.
      if (isTransferPhase(event.phase)) {
        return isTerminal(state)
          ? { ...state, transferring: true }
          : { ...state, status: 'Running', transferring: true }
      }

      return runningWithPhase(state, event.phase)
    case 'transferred':
      // The transfer finished its preparatory steps; the proposal still streams next, so the
      // indicator stays raised until review-ready resolves it.
      return runningGuard({ ...state, transferring: true })
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
      // The human review gate opens. The captured text becomes editable; the editable buffer is
      // prefilled with it and left for the reviewer to change. A failed run never reopens, but a
      // submitted/submitting run does — that is the continuation loop's next review gate.
      if (state.status === 'Failed') {
        return state
      }

      return {
        ...state,
        status: 'Completed',
        phase: null,
        // The proposal has arrived; any in-flight transfer is resolved.
        transferring: false,
        failure: null,
        proposedDecisions: event.decisions,
        editableDecisions: event.decisions,
      }
    case 'submitted':
      // The edited decisions were persisted. This is NOT terminal in the continuation loop: the
      // server runs a continuation turn then auto-starts the next decision run (a fresh
      // run-started). The gate stays closed until that next run reopens it.
      if (state.status === 'Failed') {
        return state
      }

      return {
        ...state,
        status: 'Submitted',
        phase: null,
        failure: null,
        editableDecisions: null,
        submittedPath: event.path,
        submittedNumberedPath: event.numberedPath ?? null,
        submittedSequence: event.sequence ?? null,
      }
    case 'failed':
      // Failure always wins, regardless of the phase it arrives in. A failure during a transfer
      // step ends the run, so the indicator clears and the failure surface takes over. The phase
      // string (including the transfer phases) is carried through unchanged.
      return {
        ...state,
        status: 'Failed',
        phase: null,
        transferring: false,
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

// Only Failed is terminal in the continuation loop. Submitted and Submitting are transient: the
// machine loops back to Running when the next decision run auto-starts.
function isTerminal(state: DecisionRunState): boolean {
  return state.status === 'Failed'
}

// A non-terminal event keeps the run live without overwriting a terminal status. Completed,
// Submitting, and Submitted are not terminal here: only Failed ends the machine, so a
// diagnostics/phase frame replayed mid-loop may legitimately reassert Running.
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
