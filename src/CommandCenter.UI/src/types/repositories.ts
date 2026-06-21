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
}
