// The decision run is the operational turn that follows "Execute Plan": the backend seeds a
// decision session and proposes decisions, streaming these events over SSE. The UI only
// renders them and carries the human's edited text back — it never composes prompt text,
// selects prompt classes, or replicates the backend's orchestration logic.
//
// Named `*Run*` to stay distinct from the existing `Decision*` lifecycle/projection types.

export type DecisionRunPhase = 'DecisionRun' | 'GetNextDecisions'

export type DecisionRunStartedEvent = {
  type: 'run-started'
  phase: 'DecisionRun'
}

export type DecisionRunDiagnosticsEvent = {
  type: 'diagnostics'
  sandbox: string
  approvals: string
  seeded: boolean
}

export type DecisionRunPhaseEvent = {
  type: 'phase'
  phase: 'GetNextDecisions'
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
  path: string
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
  | DecisionRunDeltaEvent
  | DecisionRunCompletedEvent
  | DecisionRunReviewReadyEvent
  | DecisionRunSubmittedEvent
  | DecisionRunFailedEvent

export type DecisionRunStatus = 'Idle' | 'Running' | 'Completed' | 'Submitted' | 'Failed'

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
  failure: { phase: string | null; reason: string; detail: string | null } | null
}
