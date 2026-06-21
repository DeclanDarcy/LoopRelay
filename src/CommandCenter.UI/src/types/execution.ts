import type { RepositoryDirtyState } from './git'

export type ExecutionReadiness = 'MissingPlan' | 'MissingMilestones' | 'Ready'

export type RepositoryExecutionState =
  | 'Ready'
  | 'Executing'
  | 'AwaitingAcceptance'
  | 'Accepted'
  | 'AwaitingCommit'
  | 'AwaitingPush'
  | 'Failed'
  | 'Cancelled'

export type ExecutionSessionState = 'Created' | 'Executing' | 'Completed' | 'Failed' | 'Cancelled'

export type ExecutionSessionSummary = {
  sessionId: string
  state: ExecutionSessionState
  repositoryState: RepositoryExecutionState
  milestonePath: string | null
  startedAt: string | null
  completedAt: string | null
  duration: string | null
  acceptedAt: string | null
  rejectedAt: string | null
  decisionNote: string | null
  lastActivityAt: string | null
  providerName: string
  providerExecutablePath: string | null
  providerProcessId: number | null
  providerStartedAt: string | null
  handoffPath: string | null
  commitSha: string | null
  committedAt: string | null
  commitMessage: string | null
  preparationSnapshotId: string | null
  pushAttemptedAt: string | null
  pushedAt: string | null
  pushedCommitSha: string | null
  pushRemoteName: string | null
  pushBranchName: string | null
  failureReason: string | null
}

export type ExecutionSession = ExecutionSessionSummary & {
  id: string
  repositoryId: string
  repositoryPath: string
}

export type ExecutionEvent = {
  sequence: number
  timestamp: string
  type: string
  message: string
}

export type ExecutionStatus = {
  sessionId: string
  state: ExecutionSessionState
  repositoryState: RepositoryExecutionState
  startedAt: string
  completedAt: string | null
  duration: string | null
  acceptedAt: string | null
  rejectedAt: string | null
  decisionNote: string | null
  lastActivityAt: string | null
  providerName: string
  providerExecutablePath: string | null
  providerProcessId: number | null
  providerStartedAt: string | null
  handoffPath: string | null
  failureReason: string | null
  recentEvents: ExecutionEvent[]
}

export type ExecutionContextArtifact = {
  role: string
  relativePath: string
  name: string
  content: string
  byteCount: number
  characterCount: number
}

export type ExecutionContextArtifactDiagnostic = {
  role: string
  relativePath: string
  byteCount: number
  characterCount: number
  warningThresholdBytes: number
  hardLimitBytes: number
  warningThresholdExceeded: boolean
  hardLimitExceeded: boolean
}

export type ExecutionRepositorySnapshot = {
  branch: string
  dirtyState: RepositoryDirtyState
  capturedAt: string
}

export type ExecutionContextDiagnostics = {
  totalBytes: number
  totalCharacters: number
  warningThresholdBytes: number
  hardLimitBytes: number
  warningThresholdExceeded: boolean
  hardLimitExceeded: boolean
  artifactDiagnostics: ExecutionContextArtifactDiagnostic[]
  validationErrors: string[]
  missingOptionalArtifacts: string[]
  launchBlocked: boolean
}

export type ExecutionContextPreview = {
  repositoryId: string
  repositoryName: string
  repositoryPath: string
  milestonePath: string
  generatedAt: string
  artifacts: ExecutionContextArtifact[]
  repositorySnapshot: ExecutionRepositorySnapshot | null
  diagnostics: ExecutionContextDiagnostics
}

export type ExecutionWorkflowStepState = 'complete' | 'current' | 'pending' | 'blocked'

export type ExecutionWorkflowStep = {
  key: string
  label: string
  detail: string
  state: ExecutionWorkflowStepState
}
