import type { DecisionSourceReference } from './decisions'
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

export type PushAttemptResult = {
  succeeded: boolean
  retryable: boolean
  error: string | null
  attemptedAt: string | null
  session: ExecutionSessionSummary | null
  diagnostics: string[]
}

export type ExecutionGitActionEligibilityRequest = {
  commitMessage?: string | null
  selectedPaths?: string[]
}

export type ExecutionGitRemoteBranchState = {
  branch: string
  aheadCount: number
  behindCount: number
  hasUnpushedChanges: boolean
  hasRemoteDivergence: boolean
  capturedAt: string
}

export type ExecutionGitActionEligibility = {
  sessionId: string
  sessionExists: boolean
  repositoryState: RepositoryExecutionState
  commitPreparationLoaded: boolean
  commitPreparationCurrent: boolean
  commitPreparationId: string | null
  preparedStatusSnapshotId: string | null
  currentStatusSnapshotId: string | null
  selectedPathCount: number
  preparedPathCount: number
  unknownSelectedPaths: string[]
  commitMessagePresent: boolean
  repositoryAllowsCommit: boolean
  awaitingPush: boolean
  commitShaExists: boolean
  commitSha: string | null
  previousPushAttemptedAt: string | null
  previousPushFailure: string | null
  remoteBranchState: ExecutionGitRemoteBranchState | null
  canCommit: boolean
  canPush: boolean
  commitDisabledReasons: string[]
  pushDisabledReasons: string[]
  diagnostics: string[]
}

export type ExecutionPromptManifestArtifact = {
  role: string
  relativePath: string
  byteCount: number | null
  characterCount: number | null
  delivered: boolean
}

export type ExecutionPromptManifest = {
  sessionId: string
  generatedAt: string
  promptText: string
  promptArtifactPath: string | null
  requestedArtifacts: ExecutionPromptManifestArtifact[]
  requestedContextBytes: number
  requestedContextCharacters: number
  deliveredArtifacts: ExecutionPromptManifestArtifact[]
  deliveredContextBytes: number
  deliveredContextCharacters: number
  dirtyRepositoryAtRequestTime: boolean
  dirtyRepositoryAtDeliveryTime: boolean | null
  governedDecisionCountRequested: number
  governedDecisionCountDelivered: number
  operationalContextSourceRequested: string | null
  operationalContextSourceDelivered: string | null
  handoffSourceRequested: string | null
  handoffSourceDelivered: string | null
  milestoneSourceRequested: string | null
  milestoneSourceDelivered: string | null
  providerDeliveryStatus: string
  providerAdjustments: string[]
  divergenceReason: string | null
  diagnostics: string[]
}

export type ExecutionPromptMetadata = {
  generatedAt: string
  repositoryPath: string
  includedArtifactPaths: string[]
}

export type ExecutionRecoveryTransparency = {
  recoveryRan: boolean
  recoveryTrigger: string | null
  reattachAttempted: boolean | null
  reattachSucceeded: boolean | null
  orphanedProviderState: boolean
  sessionMarkedFailedByRecovery: boolean
  recoveryEventTimestamp: string | null
  recoveryMessage: string | null
}

export type ExecutionMonitoringTransparency = {
  providerProcessState: string
  exitCode: number | null
  lastActivityAt: string | null
  staleActivity: boolean
  retainedEventCount: number
  firstRetainedEventSequence: number | null
  lastRetainedEventSequence: number | null
  eventRetentionTrimmingDetected: boolean
  monitoringWarnings: string[]
}

export type ExecutionHandoffProcessingTransparency = {
  handoffProduced: boolean
  handoffMissing: boolean
  handoffArchived: boolean
  archivePath: string | null
  archiveSequence: number | null
  archiveFailed: boolean
  handoffValidated: boolean
  validationFailure: string | null
  resultingSessionState: ExecutionSessionState
  resultingRepositoryState: RepositoryExecutionState
  processedAt: string | null
  providerFailureDistinctFromHandoffFailure: boolean
  providerFailureReason: string | null
  handoffFailureReason: string | null
  diagnostics: string[]
}

export type ExecutionSessionTransparency = {
  sessionId: string
  promptMetadata: ExecutionPromptMetadata | null
  recovery: ExecutionRecoveryTransparency
  monitoring: ExecutionMonitoringTransparency
  handoffProcessing: ExecutionHandoffProcessingTransparency
}

export type ExecutionEvent = {
  sequence: number
  timestamp: string
  type: string
  category?: string
  consequence?: string
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

export type ExecutionGovernedConflictDiagnostic = {
  id: string
  decisionId: string
  title: string
  statement: string
  conflictingExcerpt: string
  conflictReason: string
  affectedContext: string
  affectedPromptSection: string
  recommendedResolution: string
  severity: string
  originatingAuthority: string
  sources: DecisionSourceReference[]
  evidence: string[]
  diagnostics: string[]
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
  governedConflicts: ExecutionGovernedConflictDiagnostic[]
  missingOptionalArtifacts: string[]
  launchBlocked: boolean
}

export type ExecutionContextPreview = {
  id: string
  name: string
  path: string
  generatedAt: string
  artifacts: ExecutionContextArtifact[]
  snapshot: ExecutionRepositorySnapshot | null
  diagnostics: ExecutionContextDiagnostics
}
