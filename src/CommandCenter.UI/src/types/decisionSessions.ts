import type {
  WorkflowDecisionSessionProjection,
  WorkflowGovernanceHealthProjection,
  WorkflowGovernanceInfluenceProjection,
  WorkflowGovernanceSummary,
} from './workflow'

export type DecisionSessionState = 'Created' | 'Active' | 'TransferPending' | 'Transferred' | 'Retired'

export type DecisionSessionProjection = {
  id: string
  repositoryId: string
  state: DecisionSessionState
  createdAt: string
  activatedAt: string | null
  retiredAt: string | null
  createdBy: string
}

export type DecisionSessionDiagnostics = {
  repositoryId: string
  isValid: boolean
  sessionCount: number
  activeSessionCount: number
  errors: string[]
  warnings: string[]
  generatedAt: string
}

export type DecisionSessionMetrics = {
  estimatedTokenCount: number
  contextByteSize: number
  reasoningEventCount: number
  reasoningThreadCount: number
  reasoningRelationshipCount: number
  decisionCount: number
  decisionCandidateCount: number
  decisionProposalCount: number
  operationalContextRevisionCount: number
  lastActivityAt: string
  measuredAt: string
}

export type DecisionSessionStatistics = {
  sessionAge: string
  sessionElapsedDuration: string
  idleDuration: string
  growthRate: number
  activityRate: number
}

export type DecisionSessionEconomics = {
  estimatedReuseValue: number
  estimatedTransferValue: number
  estimatedContextCost: number
  estimatedReasoningCost: number
  estimatedContinuityBenefit: number
  estimatedCacheBenefit: number
  estimatedCacheMissRisk: number
}

export type DecisionSessionCoherence = {
  coherenceScore: number
  fragmentationScore: number
  densityScore: number
  continuityScore: number
  transferPressure: number
}

export type DecisionSessionAnalysisDiagnostics = {
  repositoryId: string
  generatedAt: string
  metrics: unknown
  economics: unknown
  coherence: unknown
  warnings: string[]
}

export type DecisionSessionLifecycleDecision = 'Continue' | 'Transfer'

export type DecisionSessionLifecycleEvaluation = {
  decision: DecisionSessionLifecycleDecision
  reuseScore: number
  transferScore: number
  reason: string
  contributingFactors: string[]
  evaluatedAt: string
}

export type DecisionSessionLifecycleDiagnostics = {
  repositoryId: string
  generatedAt: string
  inputs: unknown
  reuseScore: unknown
  transferScore: unknown
  assumptions: string[]
  warnings: string[]
}

export type DecisionSessionTransferEligibilityStatus =
  | 'NotApplicable'
  | 'Eligible'
  | 'Blocked'
  | 'Deferred'

export type DecisionSessionTransferEligibilityFinding = {
  code: string
  severity: string
  message: string
}

export type DecisionSessionTransferEligibility = {
  status: DecisionSessionTransferEligibilityStatus
  policyEvaluation: DecisionSessionLifecycleEvaluation
  sourceSessionId: string | null
  findings: DecisionSessionTransferEligibilityFinding[]
  checkedAt: string
}

export type DecisionSessionTransferEligibilityDiagnostics = {
  repositoryId: string
  generatedAt: string
  inputs: unknown
  assumptions: string[]
  warnings: string[]
}

export type DecisionSessionContinuityReference = {
  source: string
  referenceType: string
  itemCount: number
  byteCount: number
  lastActivityAt: string | null
  fingerprint: string
}

export type DecisionSessionContinuityArtifact = {
  artifactId: string
  repositoryId: string
  sourceSessionId: string
  targetSessionId: string | null
  createdAt: string
  policyEvaluation: DecisionSessionLifecycleEvaluation
  metrics: DecisionSessionMetrics
  economics: DecisionSessionEconomics
  coherence: DecisionSessionCoherence
  cache: unknown
  decisionReferences: DecisionSessionContinuityReference[]
  reasoningReferences: DecisionSessionContinuityReference[]
  operationalContextReferences: DecisionSessionContinuityReference[]
  continuityFingerprint: string
  diagnostics: string[]
}

export type DecisionSessionTransferEvent = {
  eventId: string
  eventType: 'Started' | 'Completed' | 'Failed'
  repositoryId: string
  sourceSessionId: string
  targetSessionId: string | null
  continuityArtifactId: string | null
  occurredAt: string
  message: string
  diagnostics: string[]
}

export type DecisionSessionTransfer = {
  transferId: string
  repositoryId: string
  sourceSessionId: string
  targetSessionId: string | null
  continuityArtifactId: string | null
  startedAt: string
  completedAt: string | null
  succeeded: boolean
  events: DecisionSessionTransferEvent[]
  diagnostics: string[]
}

export type DecisionSessionTransferDiagnostics = {
  repositoryId: string
  generatedAt: string
  eligibility: DecisionSessionTransferEligibility
  events: DecisionSessionTransferEvent[]
  warnings: string[]
}

export type DecisionSessionTransferResult = {
  succeeded: boolean
  transfer: DecisionSessionTransfer | null
  diagnostics: DecisionSessionTransferDiagnostics
  sourceSession: unknown | null
  replacementSession: unknown | null
  continuityArtifact: DecisionSessionContinuityArtifact | null
}

export type DecisionSessionRecoveryFinding = {
  code: string
  severity: string
  message: string
  sessionId: string | null
  evidenceId: string | null
}

export type TransferRecoveryAssessment = {
  transferId: string | null
  sourceSessionId: string
  targetSessionId: string | null
  continuityArtifactId: string | null
  status: string
  message: string
  events: DecisionSessionTransferEvent[]
}

export type DecisionSessionRecoveryEvent = {
  eventId: string
  repositoryId: string
  eventType: string
  occurredAt: string
  message: string
  diagnostics: string[]
}

export type DecisionSessionRecoveryDiagnostics = {
  repositoryId: string
  generatedAt: string
  registryDiagnostics: DecisionSessionDiagnostics
  transferAssessments: TransferRecoveryAssessment[]
  warnings: string[]
}

export type DecisionSessionRecoveryResult = {
  recoveryId: string
  repositoryId: string
  succeeded: boolean
  activeSessionId: string | null
  activeSessionCount: number
  findings: DecisionSessionRecoveryFinding[]
  diagnostics: DecisionSessionRecoveryDiagnostics
  events: DecisionSessionRecoveryEvent[]
  recoveredAt: string
}

export type DecisionSessionRecoveryHistory = {
  repositoryId: string
  results: DecisionSessionRecoveryResult[]
  generatedAt: string
}

export type DecisionSessionLifecycleProjection = {
  repositoryId: string
  activeSession: DecisionSessionProjection | null
  sessions: DecisionSessionProjection[]
  metrics: unknown | null
  size: unknown | null
  economics: unknown | null
  coherence: unknown | null
  policy: unknown | null
  transferEligibility: unknown | null
  currentContinuityArtifact: DecisionSessionContinuityArtifact | null
  continuityArtifacts: unknown[]
  recentTransfers: DecisionSessionTransfer[]
  recentTransferEvents: DecisionSessionTransferEvent[]
  transferEvents: unknown[]
  recentRecoveryResults: DecisionSessionRecoveryResult[]
  diagnostics: DecisionSessionDiagnostics
  generatedAt: string
}

export type DecisionSessionLifecycleHistoryEvent = {
  eventType: string
  occurredAt: string
  sessionId: string | null
  relatedSessionId: string | null
  continuityArtifactId: string | null
  transferId: string | null
  recoveryId: string | null
  message: string
  diagnostics: string[]
}

export type DecisionSessionLifecycleHistory = {
  repositoryId: string
  events: DecisionSessionLifecycleHistoryEvent[]
  generatedAt: string
}

export type DecisionSessionInfluenceTrace = {
  repositoryId: string
  activeSessionId: string | null
  policyDecision: string | null
  transferEligibilityStatus: string | null
  signals: unknown[]
  diagnostics: string[]
  generatedAt: string
}

export type DecisionSessionHealthAssessment = {
  repositoryId: string
  dimensions: WorkflowGovernanceHealthProjection[]
  influenceTrace: DecisionSessionInfluenceTrace
  generatedAt: string
}

export type DecisionSessionCertificationFinding = {
  id: string
  severity: string
  message: string
  evidence: string[]
}

export type DecisionSessionCertificationResult = {
  passed: boolean
  findings: DecisionSessionCertificationFinding[]
  diagnostics: string[]
}

export type DecisionSessionCertificationReport = {
  reportId: string
  repositoryId: string
  generatedAt: string
  result: DecisionSessionCertificationResult
}

export type DecisionSessionGovernanceSnapshot = {
  sessions: DecisionSessionProjection[]
  activeSession: DecisionSessionProjection | null
  diagnostics: DecisionSessionDiagnostics | null
  metrics: DecisionSessionMetrics | null
  statistics: DecisionSessionStatistics | null
  economics: DecisionSessionEconomics | null
  coherence: DecisionSessionCoherence | null
  analysisDiagnostics: DecisionSessionAnalysisDiagnostics | null
  lifecyclePolicy: DecisionSessionLifecycleEvaluation | null
  lifecyclePolicyDiagnostics: DecisionSessionLifecycleDiagnostics | null
  transferEligibility: DecisionSessionTransferEligibility | null
  transferEligibilityDiagnostics: DecisionSessionTransferEligibilityDiagnostics | null
  lifecycleProjection: DecisionSessionLifecycleProjection | null
  lifecycleHistory: DecisionSessionLifecycleHistory | null
  lifecycleInfluence: DecisionSessionInfluenceTrace | null
  health: DecisionSessionHealthAssessment | null
  continuityArtifacts: DecisionSessionContinuityArtifact[]
  transfers: DecisionSessionTransfer[]
  transferHistory: DecisionSessionTransfer[]
  transferDiagnostics: DecisionSessionTransferDiagnostics | null
  recovery: DecisionSessionRecoveryResult | null
  recoveryHistory: DecisionSessionRecoveryHistory | null
  recoveryDiagnostics: DecisionSessionRecoveryDiagnostics | null
  workflow: WorkflowDecisionSessionProjection | null
  workflowSummary: WorkflowGovernanceSummary | null
  workflowHealth: WorkflowGovernanceHealthProjection | null
  workflowInfluence: WorkflowGovernanceInfluenceProjection | null
  certification: DecisionSessionCertificationReport | null
  certificationReport: DecisionSessionCertificationReport | null
}
