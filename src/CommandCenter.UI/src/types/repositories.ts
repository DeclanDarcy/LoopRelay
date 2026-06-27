import type {
  RepositoryDashboardConsumerCandidateProjection,
  RepositoryDashboardConsumerCandidateProjectionDecisionSessionSummary,
  RepositoryDashboardConsumerCandidateProjectionDecisionSessionSummaryHealthDimensions,
  RepositoryDashboardConsumerCandidateProjectionDecisionSessionSummaryRecentTransferLineage,
} from '../contracts/generated/repository-dashboard.generated'
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

export type RepositoryDecisionSessionHealthDimension =
  RepositoryDashboardConsumerCandidateProjectionDecisionSessionSummaryHealthDimensions

export type RepositoryDecisionSessionTransferSummary =
  RepositoryDashboardConsumerCandidateProjectionDecisionSessionSummaryRecentTransferLineage

export type RepositoryDecisionSessionSummary =
  RepositoryDashboardConsumerCandidateProjectionDecisionSessionSummary

export type RepositoryDashboardProjection = RepositoryDashboardConsumerCandidateProjection

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
