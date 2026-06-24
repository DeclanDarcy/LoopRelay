import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { Header } from '../../components/shell'
import type { RepositoryDashboardProjection, WorkflowInstance } from '../../types'

afterEach(() => {
  cleanup()
})

const repository = {
  repository: {
    id: 'repo-alpha',
    name: 'AlphaRepo',
    path: 'C:/work/AlphaRepo',
  },
  availability: 'Available',
  readiness: 'Ready',
  executionState: 'AwaitingCommit',
  activeExecutionSession: null,
  executionSummary: null,
  executionHistory: [],
  milestoneCount: 1,
  hasCurrentHandoff: true,
  hasCurrentDecisions: true,
  continuitySummary: {
    operationalContextExists: true,
    operationalContextRevisionCount: 1,
    operationalContextLastUpdatedAt: null,
    openQuestionCount: 0,
    activeRiskCount: 0,
    pendingProposalExists: false,
  },
  reasoningSummary: {
    eventCount: 0,
    threadCount: 0,
    relationshipCount: 0,
    hypothesisEventCount: 0,
    alternativeEventCount: 0,
    contradictionEventCount: 0,
    directionEventCount: 0,
    decisionEvolutionEventCount: 0,
    assumptionEvolutionEventCount: 0,
    constraintEvolutionEventCount: 0,
    evidenceEventCount: 0,
    lastEventAt: null,
    lastThreadActivityAt: null,
    lastRelationshipAt: null,
    lastActivityAt: null,
    lastReconstructionAt: null,
    lastCertificationAt: null,
    certificationResult: null,
  },
  decisionSessionSummary: {
    decisionSessionId: null,
    state: null,
    lifecycleDecision: null,
    transferEligibilityStatus: null,
    estimatedTokenCount: null,
    estimatedCacheTtl: null,
    cacheMissRisk: null,
    coherenceScore: null,
    transferPressure: null,
    healthDimensions: [],
    recentTransferLineage: [],
    diagnostics: [],
    generatedAt: null,
  },
} satisfies RepositoryDashboardProjection

function renderHeader(workflow: WorkflowInstance | null) {
  render(
    <Header
      selectedRepository={repository}
      workflow={workflow}
      isWorkspaceLoading={false}
      isAddingRepository={false}
      onRefreshRepositories={vi.fn()}
      onRefreshWorkspace={vi.fn()}
      onAddRepository={vi.fn()}
    />,
  )
}

describe('shell header workflow status', () => {
  it('uses the authoritative workflow projection for selected repository status', () => {
    renderHeader({
      currentStage: 'Decision',
      progressState: 'WaitingForHuman',
    } as WorkflowInstance)

    expect(screen.getByText('Decision / WaitingForHuman')).toHaveClass('cc-badge-warning')
    expect(screen.queryByText('Awaiting commit')).not.toBeInTheDocument()
  })

  it('shows that workflow status is unloaded instead of falling back to execution state', () => {
    renderHeader(null)

    expect(screen.getByText('Workflow not loaded')).toHaveClass('cc-badge-neutral')
    expect(screen.queryByText('Awaiting commit')).not.toBeInTheDocument()
  })
})
