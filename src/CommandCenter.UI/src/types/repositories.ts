import type { ArtifactInventory } from './artifacts'
import type { ExecutionReadiness, ExecutionSessionSummary, RepositoryExecutionState } from './execution'
import type { OperationalContextProjection, OperationalContextProposalSummary } from './operationalContext'

export type RepositoryAvailability = 'Available' | 'Missing' | 'AccessDenied'

export type Repository = {
  id: string
  name: string
  path: string
}

export type RepositoryContinuitySummary = {
  operationalContextExists: boolean
  operationalContextRevisionCount: number
  operationalContextLastUpdatedAt: string | null
  openQuestionCount: number
  activeRiskCount: number
  pendingProposalExists: boolean
}

export type RepositoryReasoningSummary = {
  eventCount: number
  threadCount: number
  relationshipCount: number
  hypothesisEventCount: number
  alternativeEventCount: number
  contradictionEventCount: number
  directionEventCount: number
  decisionEvolutionEventCount: number
  assumptionEvolutionEventCount: number
  constraintEvolutionEventCount: number
  evidenceEventCount: number
  lastEventAt: string | null
  lastThreadActivityAt: string | null
  lastRelationshipAt: string | null
  lastActivityAt: string | null
  lastReconstructionAt: string | null
  lastCertificationAt: string | null
  certificationResult: string | null
}

export type RepositoryDecisionSessionHealthDimension = {
  name: string
  status: string
  findings: string[]
}

export type RepositoryDecisionSessionTransferSummary = {
  transferId: string
  sourceSessionId: string
  targetSessionId: string | null
  continuityArtifactId: string | null
  startedAt: string
  completedAt: string | null
  succeeded: boolean
}

export type RepositoryDecisionSessionSummary = {
  decisionSessionId: string | null
  state: string | null
  lifecycleDecision: string | null
  transferEligibilityStatus: string | null
  estimatedTokenCount: number | null
  estimatedCacheTtl: string | null
  cacheMissRisk: number | null
  coherenceScore: number | null
  transferPressure: number | null
  healthDimensions: RepositoryDecisionSessionHealthDimension[]
  recentTransferLineage: RepositoryDecisionSessionTransferSummary[]
  diagnostics: string[]
  generatedAt: string | null
}

export type RepositoryDashboardProjection = {
  repository: Repository
  availability: RepositoryAvailability
  readiness: ExecutionReadiness
  executionState: RepositoryExecutionState
  activeExecutionSession: ExecutionSessionSummary | null
  executionSummary: ExecutionSessionSummary | null
  executionHistory: ExecutionSessionSummary[]
  milestoneCount: number
  hasCurrentHandoff: boolean
  hasCurrentDecisions: boolean
  continuitySummary: RepositoryContinuitySummary
  reasoningSummary: RepositoryReasoningSummary
  decisionSessionSummary: RepositoryDecisionSessionSummary
}

export type RepositoryWorkspaceProjection = {
  repository: Repository
  availability: RepositoryAvailability
  readiness: ExecutionReadiness
  executionState: RepositoryExecutionState
  executionSummary: ExecutionSessionSummary | null
  executionHistory: ExecutionSessionSummary[]
  artifactInventory: ArtifactInventory
  milestoneCount: number
  hasPlan: boolean
  hasOperationalContext: boolean
  hasCurrentHandoff: boolean
  hasCurrentDecisions: boolean
  operationalContextProposalSummary: OperationalContextProposalSummary
  operationalContext: OperationalContextProjection
  reasoningSummary: RepositoryReasoningSummary
  decisionSessionSummary: RepositoryDecisionSessionSummary
}
