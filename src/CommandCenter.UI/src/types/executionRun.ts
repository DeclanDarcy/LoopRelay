// The execution run is the operational turn that follows "Execute Plan". The backend
// streams these events over SSE; the UI only renders them — it never composes prompt
// text, selects prompt classes, or replicates the backend's orchestration logic.

// ContinueExecution is the m6 continuation turn: after decisions are submitted the server reuses
// the execution stream vocabulary to run the next handoff turn, so the same machine renders it.
export type ExecutionRunPhase =
  | 'ExecutePlan'
  | 'ExtractMilestones'
  | 'StartExecution'
  | 'ContinueExecution'

export type ExecutionRunLifecycleState = 'ExecutingPlan'

export type ExecutionRunStartedEvent = {
  type: 'run-started'
  // ExecutePlan is the first run; ContinueExecution is each post-submit continuation run.
  phase: 'ExecutePlan' | 'ContinueExecution'
}

export type ExecutionRunPhaseEvent = {
  type: 'phase'
  phase: 'ExtractMilestones' | 'StartExecution' | 'ContinueExecution'
}

export type ExecutionRunDeltaEvent = {
  type: 'delta'
  phase: string
  text: string
}

export type ExecutionRunMilestonesExtractedEvent = {
  type: 'milestones-extracted'
  count: number
}

export type ExecutionRunCommittedEvent = {
  type: 'committed'
  commitSha: string | null
  pushed: boolean
}

export type ExecutionRunLifecycleEvent = {
  type: 'lifecycle'
  state: ExecutionRunLifecycleState
}

export type ExecutionRunHandoffRotatedEvent = {
  type: 'handoff-rotated'
  sequence: number
  path: string
}

export type ExecutionRunCompletedEvent = {
  type: 'completed'
  commitSha: string | null
  milestoneCount: number
  handoffPath: string
  promptTokens: number
  outputTokens: number
}

export type ExecutionRunFailedEvent = {
  type: 'failed'
  phase?: string
  reason: string
  detail?: string
}

export type ExecutionRunEvent =
  | ExecutionRunStartedEvent
  | ExecutionRunPhaseEvent
  | ExecutionRunDeltaEvent
  | ExecutionRunMilestonesExtractedEvent
  | ExecutionRunCommittedEvent
  | ExecutionRunLifecycleEvent
  | ExecutionRunHandoffRotatedEvent
  | ExecutionRunCompletedEvent
  | ExecutionRunFailedEvent

export type ExecutionRunStatus = 'Idle' | 'Running' | 'Completed' | 'Failed'

export type ExecutionRunCommit = {
  commitSha: string | null
  pushed: boolean
}

export type ExecutionRunHandoff = {
  sequence: number
  path: string
}

export type ExecutionRunCompletion = {
  commitSha: string | null
  milestoneCount: number
  handoffPath: string
  promptTokens: number
  outputTokens: number
}

export type ExecutionRunState = {
  status: ExecutionRunStatus
  // The most recent phase the backend reported, used to label the live indicator.
  phase: ExecutionRunPhase | null
  // Streamed agent output, accumulated across delta events for the active run.
  streamedText: string
  milestoneCount: number | null
  commit: ExecutionRunCommit | null
  handoff: ExecutionRunHandoff | null
  completion: ExecutionRunCompletion | null
  failure: { phase: string | null; reason: string; detail: string | null } | null
}
