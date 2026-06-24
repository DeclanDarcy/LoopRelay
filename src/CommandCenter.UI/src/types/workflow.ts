export type WorkflowStage =
  | 'Unknown'
  | 'WorkSelection'
  | 'Execution'
  | 'Handoff'
  | 'Decision'
  | 'OperationalContext'
  | 'Commit'
  | 'Push'
  | 'Completed'
  | 'Blocked'
  | 'Failed'

export type WorkflowProgressState =
  | 'Ready'
  | 'Active'
  | 'AwaitingGate'
  | 'Blocked'
  | 'Completed'
  | 'Failed'
  | 'Recovering'
  | 'WaitingForHuman'

export type WorkflowGateType =
  | 'None'
  | 'WorkSelection'
  | 'ExecutionAcceptance'
  | 'DecisionResolution'
  | 'OperationalContextReview'
  | 'OperationalContextPromotion'
  | 'CommitApproval'
  | 'PushApproval'

export type WorkflowBlockingCondition =
  | 'MissingWorkSelection'
  | 'MissingExecution'
  | 'ExecutionRunning'
  | 'ExecutionFailure'
  | 'ExecutionCancelled'
  | 'MissingHandoff'
  | 'InvalidHandoff'
  | 'PendingHandoffAcceptance'
  | 'RejectedHandoff'
  | 'MissingDecision'
  | 'UnresolvedDecision'
  | 'DecisionGovernanceBlock'
  | 'PendingContextReview'
  | 'PendingContextPromotion'
  | 'PendingCommitApproval'
  | 'PendingPushApproval'
  | 'UnknownState'
  | 'ConflictingEvidence'
  | 'RecoveryConflict'

export type WorkflowExecutionStatus =
  | 'NotStarted'
  | 'Running'
  | 'Completed'
  | 'Failed'
  | 'Cancelled'
  | 'AwaitingAcceptance'

export type WorkflowHandoffStatus = 'Missing' | 'Pending' | 'Accepted' | 'Rejected' | 'Invalid'

export type WorkflowDecisionStatus =
  | 'Missing'
  | 'Discovered'
  | 'Generated'
  | 'UnderReview'
  | 'AwaitingResolution'
  | 'Resolved'
  | 'Archived'
  | 'Superseded'

export type WorkflowOperationalContextStatus =
  | 'Missing'
  | 'Proposed'
  | 'UnderReview'
  | 'Accepted'
  | 'Edited'
  | 'Rejected'
  | 'ReadyForPromotion'
  | 'Promoted'
  | 'Archived'
  | 'NoContextRequired'

export type WorkflowGitStatus =
  | 'NotReady'
  | 'AwaitingCommit'
  | 'Committed'
  | 'AwaitingPush'
  | 'Pushed'
  | 'NoChangesProduced'
  | 'PushSkipped'
  | 'Failed'

export type WorkflowGateStatus = 'Open' | 'Satisfied' | 'Rejected' | 'Bypassed' | 'Expired' | 'Unknown'

export type WorkflowPreparationCommand =
  | 'None'
  | 'DiscoverDecisionCandidates'
  | 'GenerateDecisionProposal'
  | 'GenerateOperationalContextProposal'
  | 'PrepareExecutionCommit'

export type WorkflowTimelineEventType =
  | 'ExecutionStarted'
  | 'ExecutionCompleted'
  | 'ExecutionFailed'
  | 'ExecutionCancelled'
  | 'HandoffCreated'
  | 'HandoffValidated'
  | 'HandoffInvalid'
  | 'ExecutionHandoffAccepted'
  | 'ExecutionHandoffRejected'
  | 'DecisionDiscovered'
  | 'DecisionGenerated'
  | 'DecisionReviewed'
  | 'DecisionRefined'
  | 'DecisionResolved'
  | 'DecisionArchived'
  | 'DecisionSuperseded'
  | 'OperationalContextProposed'
  | 'OperationalContextReviewed'
  | 'OperationalContextAccepted'
  | 'OperationalContextEdited'
  | 'OperationalContextRejected'
  | 'OperationalContextPromoted'
  | 'OperationalContextArchived'
  | 'CommitPrepared'
  | 'CommitApproved'
  | 'CommitExecuted'
  | 'PushApproved'
  | 'PushSkipped'
  | 'PushExecuted'

export type WorkflowFingerprint = {
  value: string
}

export type WorkflowGateEvidence = {
  sourceDomain: string
  sourceArtifact: string
  summary: string
  observedAt: string
  fingerprint: string
}

export type WorkflowGate = {
  gateId: string
  type: WorkflowGateType
  repositoryId: string
  stage: WorkflowStage
  status: WorkflowGateStatus
  requiredAction: string
  satisfyingCommand: string
  satisfyingCommands: string[]
  sourceDomain: string
  sourceArtifact: string
  createdAt: string
  satisfiedAt: string | null
  satisfiedActor: string | null
  reason: string
  evidence: WorkflowGateEvidence[]
}

export type WorkflowGateResolution = {
  gateType: WorkflowGateType
  blockingCondition: WorkflowBlockingCondition
  requiredHumanAction: string
  isSatisfied: boolean
}

export type WorkflowTransition = {
  fromStage: WorkflowStage
  toStage: WorkflowStage
  requiredGate: WorkflowGateType
  blockingCondition: WorkflowBlockingCondition | null
  description: string
}

export type WorkflowTransitionResult = {
  transition: WorkflowTransition
  isValid: boolean
  isBlocked: boolean
  gateResolution: WorkflowGateResolution | null
  blockingCondition: WorkflowBlockingCondition | null
  reason: string
}

export type WorkflowStateMachineDiagnostics = {
  repositoryId: string
  currentStage: WorkflowStage
  progressState: WorkflowProgressState
  blockingGate: WorkflowGateType
  candidateStages: WorkflowStage[]
  validTransitions: WorkflowTransitionResult[]
  blockedTransitions: WorkflowTransitionResult[]
  reasoning: string[]
  rejectedTransitions: string[]
}

export type WorkflowExecutionDiagnostics = {
  repositoryId: string
  includedEvidence: string[]
  missingEvidence: string[]
  conflicts: string[]
  reasoning: string[]
}

export type WorkflowExecutionFailure = {
  reason: string
  failedAt: string | null
  sourceArtifact: string
}

export type WorkflowExecutionProjection = {
  repositoryId: string
  executionId: string | null
  status: WorkflowExecutionStatus
  startedAt: string | null
  completedAt: string | null
  failedAt: string | null
  acceptedAt: string | null
  rejectedAt: string | null
  committedAt: string | null
  pushedAt: string | null
  commitSha: string | null
  pushedCommitSha: string | null
  hasHandoff: boolean
  hasChanges: boolean
  failureReason: string | null
  isExecutionEligible: boolean
  failure: WorkflowExecutionFailure | null
  diagnostics: WorkflowExecutionDiagnostics
}

export type WorkflowHandoffValidation = {
  isValid: boolean
  checks: string[]
  failures: string[]
}

export type WorkflowHandoffDiagnostics = {
  repositoryId: string
  includedEvidence: string[]
  missingEvidence: string[]
  conflicts: string[]
  reasoning: string[]
}

export type WorkflowHandoffProjection = {
  repositoryId: string
  executionId: string | null
  handoffId: string | null
  handoffPath: string | null
  status: WorkflowHandoffStatus
  createdAt: string | null
  acceptedAt: string | null
  rejectedAt: string | null
  hasChanges: boolean
  summary: string | null
  validation: WorkflowHandoffValidation
  diagnostics: WorkflowHandoffDiagnostics
}

export type WorkflowDecisionDiagnostics = {
  repositoryId: string
  projectionInputs: string[]
  reasoning: string[]
  governanceSignals: string[]
  qualitySignals: string[]
  certificationSignals: string[]
  supersessionSignals: string[]
  conflicts: string[]
}

export type WorkflowDecisionProjection = {
  repositoryId: string
  decisionId: string | null
  candidateId: string | null
  candidateState: string | null
  proposalId: string | null
  packageId: string | null
  status: WorkflowDecisionStatus
  reviewState: string | null
  resolutionState: string | null
  humanAuthoringBurden: string
  createdAt: string | null
  resolvedAt: string | null
  isResolutionEligible: boolean
  isGovernanceBlocked: boolean
  governanceStatus: string | null
  qualityStatus: string | null
  certificationStatus: string | null
  replacementDecisionId: string | null
  diagnostics: WorkflowDecisionDiagnostics
}

export type WorkflowOperationalContextDiagnostics = {
  repositoryId: string
  projectionInputs: string[]
  reasoning: string[]
  reviewSignals: string[]
  promotionSignals: string[]
  linkageSignals: string[]
  conflicts: string[]
}

export type WorkflowOperationalContextProjection = {
  repositoryId: string
  proposalId: string | null
  status: WorkflowOperationalContextStatus
  reviewState: string | null
  promotionState: string | null
  createdAt: string | null
  reviewedAt: string | null
  promotedAt: string | null
  reviewer: string | null
  summary: string
  sourceDecisionId: string | null
  sourceExecutionId: string | null
  isReviewEligible: boolean
  isPromotionEligible: boolean
  isCommitEligible: boolean
  diagnostics: WorkflowOperationalContextDiagnostics
}

export type WorkflowCompletionEvaluation = {
  repositoryId: string
  isComplete: boolean
  completionReason: string
  completionArtifact: string | null
  evidence: string[]
  diagnostics: string[]
}

export type WorkflowGitDiagnostics = {
  repositoryId: string
  includedEvidence: string[]
  missingEvidence: string[]
  commitSignals: string[]
  pushSignals: string[]
  reasoning: string[]
  conflicts: string[]
}

export type WorkflowGitProjection = {
  repositoryId: string
  commitStatus: WorkflowGitStatus
  pushStatus: WorkflowGitStatus
  commitId: string | null
  branch: string
  commitTimestamp: string | null
  pushTimestamp: string | null
  hasPendingChanges: boolean
  hasUnpushedChanges: boolean
  isCommitRequired: boolean
  isPushRequired: boolean
  isCommitGateOpen: boolean
  isPushGateOpen: boolean
  completion: WorkflowCompletionEvaluation
  diagnostics: WorkflowGitDiagnostics
}

export type WorkflowTimelineEntry = {
  eventType: WorkflowTimelineEventType
  stage: WorkflowStage
  occurredAt: string
  summary: string
  sourceDomain: string
  sourceArtifact: string
  fingerprint: string
}

export type WorkflowTimeline = {
  repositoryId: string
  currentStage: WorkflowStage
  previousStage: WorkflowStage
  progressState: WorkflowProgressState
  blockingGate: WorkflowGateType
  generatedAt: string
  entries: WorkflowTimelineEntry[]
  fingerprint: string
}

export type WorkflowGateDiagnostics = {
  repositoryId: string
  blockingGate: WorkflowGateType
  openGates: WorkflowGate[]
  satisfiedGates: WorkflowGate[]
  gateCommandMap: string[]
  reasoning: string[]
  missingEvidence: string[]
  conflicts: string[]
}

export type WorkflowProjectionDiagnostics = {
  repositoryId: string
  projectionInputs: string[]
  chosenStage: WorkflowStage
  chosenGate: WorkflowGateType
  nextPossibleStages: WorkflowStage[]
  validTransitions: WorkflowTransitionResult[]
  blockedTransitions: WorkflowTransitionResult[]
  stateMachine: WorkflowStateMachineDiagnostics
  reasoning: string[]
  unknownStates: string[]
  conflicts: string[]
}

export type WorkflowTransferProjection = {
  transferId: string
  sourceSessionId: string
  targetSessionId: string | null
  startedAt: string
  completedAt: string | null
  succeeded: boolean
  continuityArtifactId: string | null
  status: string
  diagnostics: string[]
}

export type WorkflowContinuityArtifactProjection = {
  artifactId: string
  continuityFingerprint: string
  sourceSessionId: string
  targetSessionId: string | null
  createdAt: string
  decisionReferenceCount: number
  reasoningReferenceCount: number
  operationalContextReferenceCount: number
  diagnostics: string[]
}

export type WorkflowGovernanceHealthProjection = {
  name: string
  status: string
  findings: string[]
  evidence: string[]
}

export type WorkflowGovernanceInfluenceSignal = {
  category: string
  name: string
  score: number | null
  value: string
  description: string
  contributingFactors: string[]
}

export type WorkflowGovernanceInfluenceProjection = {
  repositoryId: string
  decisionSessionId: string | null
  lifecycleDecision: string | null
  transferEligibilityStatus: string | null
  signals: WorkflowGovernanceInfluenceSignal[]
  diagnostics: string[]
  generatedAt: string
}

export type WorkflowGovernanceSummary = {
  repositoryId: string
  decisionSessionId: string | null
  decisionSessionState: string | null
  lifecycleDecision: string | null
  transferEligibilityStatus: string | null
  estimatedTokenCount: number | null
  estimatedCacheTtl: string | null
  estimatedCacheMissRisk: number | null
  coherenceScore: number | null
  transferPressure: number | null
  healthStatus: string
  highlights: string[]
  generatedAt: string
}

export type WorkflowGovernanceReadiness = {
  hasActiveSession: boolean
  hasAnalysis: boolean
  hasPolicy: boolean
  hasEligibility: boolean
  isTransferRecommended: boolean
  isTransferEligible: boolean
  hasContinuityArtifact: boolean
  missingEvidence: string[]
  blockingSignals: string[]
}

export type DecisionSessionWorkflowDiagnostics = {
  repositoryId: string
  isValid: boolean
  evidence: string[]
  warnings: string[]
  errors: string[]
  generatedAt: string
}

export type WorkflowDecisionSessionProjection = {
  repositoryId: string
  decisionSessionId: string | null
  decisionSessionState: string | null
  estimatedTokenCount: number | null
  estimatedCacheTtl: string | null
  estimatedCacheMissRisk: number | null
  reuseScore: number | null
  transferScore: number | null
  coherenceScore: number | null
  transferPressure: number | null
  currentLifecycleDecision: string | null
  transferEligibilityStatus: string | null
  continuityArtifactId: string | null
  continuityFingerprint: string | null
  transferLineage: WorkflowTransferProjection[]
  continuityArtifactLineage: WorkflowContinuityArtifactProjection[]
  governanceHealthDimensions: WorkflowGovernanceHealthProjection[]
  summary: WorkflowGovernanceSummary
  readiness: WorkflowGovernanceReadiness
  diagnostics: DecisionSessionWorkflowDiagnostics
  generatedAt: string
}

export type WorkflowInstance = {
  repositoryId: string
  currentStage: WorkflowStage
  progressState: WorkflowProgressState
  blockingGate: WorkflowGateType
  requiredHumanAction: string
  currentExecution: WorkflowExecutionProjection
  executionStatus: WorkflowExecutionStatus
  isExecutionEligible: boolean
  executionFailure: WorkflowExecutionFailure | null
  executionDiagnostics: WorkflowExecutionDiagnostics
  currentHandoff: WorkflowHandoffProjection
  handoffStatus: WorkflowHandoffStatus
  handoffValidation: WorkflowHandoffValidation
  handoffDiagnostics: WorkflowHandoffDiagnostics
  currentDecision: WorkflowDecisionProjection
  decisionStatus: WorkflowDecisionStatus
  isDecisionResolutionEligible: boolean
  isDecisionGovernanceBlocked: boolean
  decisionDiagnostics: WorkflowDecisionDiagnostics
  currentOperationalContext: WorkflowOperationalContextProjection
  operationalContextStatus: WorkflowOperationalContextStatus
  isOperationalContextReviewEligible: boolean
  isOperationalContextPromotionEligible: boolean
  isOperationalContextCommitEligible: boolean
  operationalContextDiagnostics: WorkflowOperationalContextDiagnostics
  currentGit: WorkflowGitProjection
  gitCommitStatus: WorkflowGitStatus
  gitPushStatus: WorkflowGitStatus
  hasPendingGitChanges: boolean
  hasUnpushedGitChanges: boolean
  completionEvaluation: WorkflowCompletionEvaluation
  gitDiagnostics: WorkflowGitDiagnostics
  nextPossibleStages: WorkflowStage[]
  validTransitions: WorkflowTransitionResult[]
  blockedTransitions: WorkflowTransitionResult[]
  timeline: WorkflowTimelineEntry[]
  openGates: WorkflowGate[]
  satisfiedGates: WorkflowGate[]
  gateHistory: WorkflowGate[]
  gateDiagnostics: WorkflowGateDiagnostics
  diagnostics: WorkflowProjectionDiagnostics
  decisionSession: WorkflowDecisionSessionProjection | null
}

export type WorkflowGateCatalogProjection = {
  openGates: WorkflowGate[]
  satisfiedGates: WorkflowGate[]
  gateHistory: WorkflowGate[]
  diagnostics: WorkflowGateDiagnostics
}

export type WorkflowGateHistoryProjection = {
  repositoryId: string
  generatedAt: string
  gates: WorkflowGate[]
  markdown: string
}

export type WorkflowHistoryProjection = {
  repositoryId: string
  timeline: WorkflowTimeline
  gateHistory: string[]
  progressSummary: string[]
  recoverySummary: WorkflowRecoveryDiagnostics[]
}

export type WorkflowRecoveryDiagnostics = {
  repositoryId: string
  recoveredAt: string
  domainFingerprint: string
  persistedFingerprint: string | null
  rebuilt: boolean
  persistedEvidenceMatchedDomain: boolean
  recoveredArtifacts: string[]
  discardedArtifacts: string[]
  diagnostics: string[]
}

export type WorkflowRecoveryResult = {
  timeline: WorkflowTimeline
  diagnostics: WorkflowRecoveryDiagnostics
}

export type WorkflowContinuationDiagnostics = {
  repositoryId: string
  projectionInputs: string[]
  stateMachineReasoning: string[]
  gateReasoning: string[]
  completionEvidence: string[]
  reasoning: string[]
  stopReasons: string[]
  conflicts: string[]
  openGateCount: number
  satisfiedGateCount: number
  fingerprint: WorkflowFingerprint
}

export type WorkflowContinuationEvaluation = {
  repositoryId: string
  fromStage: WorkflowStage
  toStage: WorkflowStage | null
  progressState: WorkflowProgressState
  blockingGate: WorkflowGateType
  canAdvanceMechanically: boolean
  isWaitingForHuman: boolean
  isComplete: boolean
  requiredHumanAction: string
  outcome: string
  stopReason: string
  fingerprint: WorkflowFingerprint
  transition: WorkflowTransitionResult | null
  completion: WorkflowCompletionEvaluation
  diagnostics: WorkflowContinuationDiagnostics
}

export type WorkflowContinuationEvent = {
  repositoryId: string
  eventId: string
  occurredAt: string
  trigger: string
  fromStage: WorkflowStage
  toStage: WorkflowStage | null
  progressState: WorkflowProgressState
  blockingGate: WorkflowGateType
  decision: string
  reason: string
  inputFingerprint: WorkflowFingerprint
  isWaitingForHuman: boolean
  isComplete: boolean
  requiredHumanAction: string
  diagnostics: string[]
}

export type WorkflowPreparationDiagnostics = {
  repositoryId: string
  projectionInputs: string[]
  gateReasoning: string[]
  reasoning: string[]
  refusalReasons: string[]
  duplicateEvidence: string[]
  conflicts: string[]
  openGateCount: number
  satisfiedGateCount: number
  fingerprint: WorkflowFingerprint
}

export type WorkflowPreparationEvaluation = {
  repositoryId: string
  stage: WorkflowStage
  progressState: WorkflowProgressState
  blockingGate: WorkflowGateType
  canPrepare: boolean
  isWaitingForHuman: boolean
  hasDuplicateDomainEvidence: boolean
  command: WorkflowPreparationCommand
  commandName: string
  outcome: string
  reason: string
  duplicateEvidence: string[]
  fingerprint: WorkflowFingerprint
  diagnostics: WorkflowPreparationDiagnostics
}

export type WorkflowPreparationEvent = {
  repositoryId: string
  eventId: string
  occurredAt: string
  trigger: string
  stage: WorkflowStage
  progressState: WorkflowProgressState
  blockingGate: WorkflowGateType
  command: WorkflowPreparationCommand
  commandName: string
  decision: string
  reason: string
  inputFingerprint: WorkflowFingerprint
  isWaitingForHuman: boolean
  hasDuplicateDomainEvidence: boolean
  createdArtifactIds: string[]
  duplicateEvidence: string[]
  diagnostics: string[]
}

export type WorkflowHealthDimension = {
  name: string
  status: string
  reason: string
  evidence: string[]
  diagnostics: string[]
}

export type WorkflowInfluenceTrace = {
  repositoryId: string
  generatedAt: string
  currentStage: WorkflowStage
  progressState: WorkflowProgressState
  blockingGate: WorkflowGateType
  evidencePaths: string[]
  stageInfluences: string[]
  progressionInfluences: string[]
  preparationInfluences: string[]
  gateInfluences: string[]
  blockingInfluences: string[]
  conflicts: string[]
  fingerprint: string
  governanceInfluence: WorkflowGovernanceInfluenceProjection | null
}

export type WorkflowHealthAssessment = {
  repositoryId: string
  generatedAt: string
  overallStatus: string
  dimensions: WorkflowHealthDimension[]
  influenceTrace: WorkflowInfluenceTrace
  diagnostics: string[]
  governanceHealth: WorkflowGovernanceHealthProjection | null
}

export type WorkflowCertificationFinding = {
  id: string
  category: string
  passed: boolean
  summary: string
  detail: string
  evidence: string[]
  diagnostics: string[]
}

export type WorkflowCertificationResult = {
  id: string
  repositoryId: string
  generatedAt: string
  inputFingerprint: string
  certified: boolean
  currentStage: WorkflowStage
  progressState: WorkflowProgressState
  blockingGate: WorkflowGateType
  passedFindingCount: number
  failedFindingCount: number
  findings: WorkflowCertificationFinding[]
  failures: string[]
  diagnostics: string[]
}

export type RepositoryWorkflowReport = {
  repositoryId: string
  generatedAt: string
  currentStage: WorkflowStage
  progressState: WorkflowProgressState
  blockingGate: WorkflowGateType
  requiredHumanAction: string
  timelineEntryCount: number
  openGateCount: number
  satisfiedGateCount: number
  continuationEventCount: number
  preparationEventCount: number
  healthStatus: string
  certified: boolean
  failedCertificationFindingCount: number
  diagnostics: string[]
}

export type WorkflowProgressionReport = {
  repositoryId: string
  generatedAt: string
  currentStage: WorkflowStage
  progressState: WorkflowProgressState
  blockingGate: WorkflowGateType
  validTransitionCount: number
  blockedTransitionCount: number
  continuationEventCount: number
  validTransitions: string[]
  blockedTransitions: string[]
  continuationEvidence: string[]
  diagnostics: string[]
}

export type HumanGovernanceReport = {
  repositoryId: string
  generatedAt: string
  blockingGate: WorkflowGateType
  requiredHumanAction: string
  openGateCount: number
  satisfiedGateCount: number
  openGates: string[]
  satisfiedGates: string[]
  authorityFindings: string[]
  diagnostics: string[]
}

export type WorkflowReadinessReport = {
  repositoryId: string
  generatedAt: string
  ready: boolean
  certified: boolean
  healthStatus: string
  currentStage: WorkflowStage
  progressState: WorkflowProgressState
  blockingGate: WorkflowGateType
  blockingReasons: string[]
  failedCertificationFindings: string[]
  healthDiagnostics: string[]
}
