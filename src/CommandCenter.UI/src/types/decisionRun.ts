// The decision run is the operational turn that follows "Execute Plan": the backend seeds a
// decision session and proposes decisions, streaming these events over SSE. The UI only
// renders them and carries the human's edited text back — it never composes prompt text,
// selects prompt classes, or replicates the backend's orchestration logic.
//
// Named `*Run*` to stay distinct from the existing `Decision*` lifecycle/projection types.

export type DecisionRunPhase = 'DecisionRun' | 'GetNextDecisions'

// The continuation router either reuses the warm decision session (Continue) or hands it off to a
// fresh one (Transfer). A Transfer inserts three preparatory phases before the proposal; Continue
// is the unchanged warm path. The field is optional so an older server (no route) reads as Continue.
export type DecisionRunRoute = 'Continue' | 'Transfer'

// The phases a Transfer-routed run streams before the normal decision flow resumes. They are kept
// distinct from DecisionRunPhase so the existing phase label and the warm Continue path are
// untouched; the machine tracks "transferring" separately rather than widening `phase`.
export type DecisionRunTransferPhase =
  | 'ProduceOperationalDelta'
  | 'UpdateOperationalContext'
  | 'StartDecisionSessionFromTransfer'

export type DecisionRunStartedEvent = {
  type: 'run-started'
  phase: 'DecisionRun'
  // Present once the server reports the continuation route. Absent on the warm/legacy path.
  route?: DecisionRunRoute
}

export type DecisionRunDiagnosticsEvent = {
  type: 'diagnostics'
  sandbox: string
  approvals: string
  seeded: boolean
}

export type DecisionRunPhaseEvent = {
  type: 'phase'
  // GetNextDecisions is the proposing phase; the three transfer phases stream only on a
  // Transfer-routed run, ahead of the proposal.
  phase: 'GetNextDecisions' | DecisionRunTransferPhase
}

// Emitted once a Transfer-routed run has produced the operational delta, updated the operational
// context, and started a fresh decision session. The normal decision flow resumes after this.
export type DecisionRunTransferredEvent = {
  type: 'transferred'
  operationalDelta: string
  operationalContext: string
}

export type DecisionRunDeltaEvent = {
  type: 'delta'
  text: string
}

export type DecisionRunCompletedEvent = {
  type: 'completed'
  promptTokens: number
  outputTokens: number
}

export type DecisionRunReviewReadyEvent = {
  type: 'review-ready'
  decisions: string
}

export type DecisionRunSubmittedEvent = {
  type: 'submitted'
  // The live canonical decisions path the submission persisted to.
  path: string
  // The rotated numbered submission path and its sequence, present once the loop is running.
  // Optional so a single-shot submit (no continuation) still type-checks.
  sequence?: number
  numberedPath?: string
}

export type DecisionRunFailedEvent = {
  type: 'failed'
  phase?: string
  reason: string
  detail?: string
}

export type DecisionRunEvent =
  | DecisionRunStartedEvent
  | DecisionRunDiagnosticsEvent
  | DecisionRunPhaseEvent
  | DecisionRunTransferredEvent
  | DecisionRunDeltaEvent
  | DecisionRunCompletedEvent
  | DecisionRunReviewReadyEvent
  | DecisionRunSubmittedEvent
  | DecisionRunFailedEvent

// The submit no longer ends the run: after Submitted the server runs a continuation turn and
// auto-starts the next decision run, so the machine loops back to Running. Only Failed is terminal.
export type DecisionRunStatus =
  | 'Idle'
  | 'Running'
  | 'Completed'
  | 'Submitting'
  | 'Submitted'
  | 'Failed'

export type DecisionRunDiagnostics = {
  sandbox: string
  approvals: string
  seeded: boolean
}

export type DecisionRunCompletion = {
  promptTokens: number
  outputTokens: number
}

export type DecisionRunState = {
  status: DecisionRunStatus
  // The most recent phase the backend reported, used to label the live indicator.
  phase: DecisionRunPhase | null
  // Streamed agent output, accumulated across delta events for the active run.
  streamedText: string
  // The validated sandbox configuration the backend logged before proposing decisions.
  diagnostics: DecisionRunDiagnostics | null
  // The full captured proposed-decisions text — populated only at review-ready.
  proposedDecisions: string | null
  // The editable, human-owned decisions text. Null until review-ready arrives: before then
  // the turn is still running and nothing is committed, so the gate stays closed.
  editableDecisions: string | null
  completion: DecisionRunCompletion | null
  // The persisted decisions path, set once a submit succeeds.
  submittedPath: string | null
  // The rotated numbered submission path the most recent submit produced, when the loop reports it.
  submittedNumberedPath: string | null
  // The rotated submission sequence the most recent submit produced, when the loop reports it.
  submittedSequence: number | null
  // Which decision turn the human is on, starting at 1 for the first proposal. Each auto-started
  // continuation decision run increments this so the surface can show the iteration.
  iteration: number
  // True while the continuation router is transferring the decision session: set on a
  // Transfer-routed run-started, any of the three transfer phases, or the `transferred` event, and
  // cleared once the proposal arrives (review-ready) or the run fails. The warm Continue path never
  // sets it, so that path stays visually unchanged.
  transferring: boolean
  failure: { phase: string | null; reason: string; detail: string | null } | null
}
