import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { SelectedRepositorySummary } from '../../features/repositories/SelectedRepositorySummary'
import type {
  ExecutionSessionSummary,
  RepositoryDashboardProjection,
  RepositoryExecutionState,
  RepositoryWorkspaceProjection,
} from '../../types'

afterEach(() => {
  cleanup()
})

function executionSummary(overrides: Partial<ExecutionSessionSummary> = {}): ExecutionSessionSummary {
  return {
    sessionId: 'session-alpha',
    state: 'Completed',
    repositoryState: 'AwaitingCommit',
    milestonePath: '.agents/milestones/m0.md',
    startedAt: '2026-06-21T16:00:00.000Z',
    completedAt: '2026-06-21T16:20:00.000Z',
    duration: '00:20:00',
    acceptedAt: null,
    rejectedAt: null,
    decisionNote: null,
    lastActivityAt: '2026-06-21T16:20:00.000Z',
    providerName: 'codex',
    providerExecutablePath: null,
    providerProcessId: null,
    providerStartedAt: null,
    handoffPath: '.agents/handoffs/handoff.md',
    commitSha: null,
    committedAt: null,
    commitMessage: null,
    preparationSnapshotId: null,
    pushAttemptedAt: null,
    pushedAt: null,
    pushedCommitSha: null,
    pushRemoteName: null,
    pushBranchName: null,
    failureReason: null,
    ...overrides,
  }
}

const decisionSessionSummary = {
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
} satisfies RepositoryDashboardProjection['decisionSessionSummary']

function repositoryDashboard(
  overrides: Partial<RepositoryDashboardProjection> = {},
): RepositoryDashboardProjection {
  return {
    repository: {
      id: 'repo-alpha',
      name: 'AlphaRepo',
      path: 'C:/work/AlphaRepo',
    },
    availability: 'Available',
    readiness: 'Ready',
    executionState: 'Ready',
    activeExecutionSession: null,
    executionSummary: null,
    executionHistory: [],
    milestoneCount: 3,
    hasCurrentHandoff: true,
    hasCurrentDecisions: false,
    continuitySummary: {
      operationalContextExists: true,
      operationalContextRevisionCount: 4,
      operationalContextLastUpdatedAt: '2026-06-21T17:30:00.000Z',
      openQuestionCount: 2,
      activeRiskCount: 1,
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
    decisionSessionSummary,
    ...overrides,
  }
}

function workspaceProjection(
  overrides: Partial<RepositoryWorkspaceProjection> = {},
): RepositoryWorkspaceProjection {
  const repository = {
    id: 'repo-alpha',
    name: 'AlphaRepo',
    path: 'C:/work/AlphaRepo',
  }

  return {
    repository,
    availability: 'Available',
    readiness: 'MissingMilestones',
    executionState: 'AwaitingCommit',
    executionSummary: null,
    executionHistory: [],
    artifactInventory: {
      plan: null,
      operationalContext: null,
      historicalOperationalContexts: [],
      milestones: [],
      currentHandoff: null,
      historicalHandoffs: [],
      currentDecisions: null,
      historicalDecisions: [],
    },
    milestoneCount: 5,
    hasPlan: true,
    hasOperationalContext: true,
    hasCurrentHandoff: false,
    hasCurrentDecisions: true,
    operationalContextProposalSummary: {
      pendingProposalExists: false,
      latestProposalId: null,
      generatedAt: null,
      status: null,
      sourceInputCount: 0,
      contentByteCount: 0,
      contentCharacterCount: 0,
      lastPromotedAt: null,
      lastArchivedRelativePath: null,
    },
    operationalContext: {
      exists: true,
      currentRelativePath: '.agents/operational-context/context.md',
      revisionCount: 4,
      currentRevisionNumber: 4,
      lastUpdatedAt: '2026-06-21T17:30:00.000Z',
      lastPromotionAt: null,
      currentUnderstandingSummary: ['Current understanding'],
      architecture: [],
      authorityBoundaries: [],
      constraints: [],
      stableDecisions: [],
      decisionRationale: [],
      openQuestions: [],
      activeRisks: [],
      recentUnderstandingChanges: [],
      pendingProposalSummary: {
        pendingProposalExists: false,
        latestProposalId: null,
        generatedAt: null,
        status: null,
        sourceInputCount: 0,
        contentByteCount: 0,
        contentCharacterCount: 0,
        lastPromotedAt: null,
        lastArchivedRelativePath: null,
      },
      latestReviewState: null,
      continuityWarnings: [],
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
    decisionSessionSummary,
    ...overrides,
  }
}

function renderSummary({
  repository = repositoryDashboard(),
  workspace = null,
  executionDisplay = null,
  currentExecutionState = 'Ready',
  onOpenExecution,
  onOpenMilestones,
  onOpenOperationalContext,
  onOpenHandoffArtifact,
}: {
  repository?: RepositoryDashboardProjection
  workspace?: RepositoryWorkspaceProjection | null
  executionDisplay?: ExecutionSessionSummary | null
  currentExecutionState?: RepositoryExecutionState
  onOpenExecution?: () => void
  onOpenMilestones?: () => void
  onOpenOperationalContext?: () => void
  onOpenHandoffArtifact?: (handoffPath: string) => void
} = {}) {
  render(
    <SelectedRepositorySummary
      repository={repository}
      workspace={workspace}
      executionDisplay={executionDisplay}
      currentExecutionState={currentExecutionState}
      onOpenExecution={onOpenExecution}
      onOpenMilestones={onOpenMilestones}
      onOpenOperationalContext={onOpenOperationalContext}
      onOpenHandoffArtifact={onOpenHandoffArtifact}
    />,
  )
}

describe('selected repository summary rendering characterization', () => {
  it('renders selected repository identity and dashboard fallbacks when workspace is absent', () => {
    renderSummary()

    expect(screen.getByText('Selected repository')).toHaveClass('eyebrow')
    expect(screen.getByRole('heading', { level: 3, name: 'AlphaRepo' })).toBeInTheDocument()
    expect(screen.getByText('Available')).toHaveClass('cc-badge', 'cc-badge-success')
    expect(screen.getByText('Path').nextElementSibling).toHaveTextContent('C:/work/AlphaRepo')
    expect(screen.getByText('Readiness').nextElementSibling).toHaveTextContent('Ready')
    expect(screen.getByText('Execution').nextElementSibling).toHaveTextContent('Ready')
    expect(screen.getByText('Milestones').nextElementSibling).toHaveTextContent('3')
    expect(screen.getByText('Plan: Missing')).toBeInTheDocument()
    expect(screen.getByText('Operational context: Missing')).toBeInTheDocument()
    expect(screen.getByText('Handoff: Missing')).toBeInTheDocument()
    expect(screen.getByText('Decisions: Missing')).toBeInTheDocument()
  })

  it('uses workspace facts over dashboard facts when workspace is present', () => {
    renderSummary({
      repository: repositoryDashboard({ readiness: 'Ready', milestoneCount: 3 }),
      workspace: workspaceProjection(),
      currentExecutionState: 'AwaitingCommit',
    })

    expect(screen.getByText('Readiness').nextElementSibling).toHaveTextContent(
      'Missing milestones',
    )
    expect(screen.getByText('Execution').nextElementSibling).toHaveTextContent('Awaiting commit')
    expect(screen.getByText('Milestones').nextElementSibling).toHaveTextContent('5')
    expect(screen.getByText('Plan: Present')).toBeInTheDocument()
    expect(screen.getByText('Operational context: Present')).toBeInTheDocument()
    expect(screen.getByText('Handoff: Missing')).toBeInTheDocument()
    expect(screen.getByText('Decisions: Present')).toBeInTheDocument()
  })

  it('renders execution display details and existing not-recorded fallbacks', () => {
    renderSummary({
      executionDisplay: executionSummary({
        sessionId: 'session-42',
        providerName: '',
        duration: null,
        acceptedAt: null,
        rejectedAt: null,
        decisionNote: null,
        providerProcessId: null,
        providerExecutablePath: null,
        failureReason: null,
        handoffPath: null,
      }),
    })

    const details = screen.getByText('Session').closest('dl')
    expect(details).not.toBeNull()
    const scopedDetails = within(details!)
    expect(scopedDetails.getByText('Session').nextElementSibling).toHaveTextContent('session-42')
    expect(scopedDetails.getByText('Provider').nextElementSibling).toHaveTextContent('Unknown')
    expect(scopedDetails.getByText('Started').nextElementSibling).toHaveTextContent(/\d/)
    expect(scopedDetails.getByText('Last activity').nextElementSibling).toHaveTextContent(/\d/)
    expect(scopedDetails.getByText('Duration').nextElementSibling).toHaveTextContent(
      'Not recorded',
    )
    expect(scopedDetails.getByText('Accepted').nextElementSibling).toHaveTextContent(
      'Not recorded',
    )
    expect(scopedDetails.getByText('Rejected').nextElementSibling).toHaveTextContent(
      'Not recorded',
    )
    expect(scopedDetails.getByText('Decision note').nextElementSibling).toHaveTextContent(
      'Not recorded',
    )
    expect(scopedDetails.getByText('PID').nextElementSibling).toHaveTextContent('Not recorded')
    expect(scopedDetails.getByText('Executable').nextElementSibling).toHaveTextContent(
      'Not recorded',
    )
    expect(scopedDetails.getByText('Failure').nextElementSibling).toHaveTextContent('None')
    expect(scopedDetails.getByText('Handoff').nextElementSibling).toHaveTextContent(
      'Not recorded',
    )
  })

  it('uses optional navigation callbacks for projected summary destinations', () => {
    const onOpenExecution = vi.fn()
    const onOpenMilestones = vi.fn()
    const onOpenOperationalContext = vi.fn()
    const onOpenHandoffArtifact = vi.fn()

    renderSummary({
      workspace: workspaceProjection(),
      executionDisplay: executionSummary(),
      currentExecutionState: 'AwaitingCommit',
      onOpenExecution,
      onOpenMilestones,
      onOpenOperationalContext,
      onOpenHandoffArtifact,
    })

    fireEvent.click(screen.getByRole('button', { name: 'Awaiting commit' }))
    fireEvent.click(screen.getByRole('button', { name: 'session-alpha' }))
    fireEvent.click(screen.getByRole('button', { name: '5' }))
    fireEvent.click(screen.getByRole('button', { name: 'Present' }))
    fireEvent.click(screen.getByRole('button', { name: '.agents/handoffs/handoff.md' }))

    expect(onOpenExecution).toHaveBeenCalledTimes(2)
    expect(onOpenMilestones).toHaveBeenCalledTimes(1)
    expect(onOpenOperationalContext).toHaveBeenCalledTimes(1)
    expect(onOpenHandoffArtifact).toHaveBeenCalledWith('.agents/handoffs/handoff.md')
  })
})
