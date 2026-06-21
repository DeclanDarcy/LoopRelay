import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { RepositoryDashboardItemContent } from '../../features/repositories/RepositoryDashboardItemContent'
import type {
  ExecutionReadiness,
  ExecutionSessionSummary,
  RepositoryAvailability,
  RepositoryDashboardProjection,
  RepositoryExecutionState,
} from '../../types'

afterEach(() => {
  cleanup()
})

const availabilityLabels: Record<RepositoryAvailability, string> = {
  Available: 'Available',
  Missing: 'Missing',
  AccessDenied: 'Access denied',
}

const readinessLabels: Record<ExecutionReadiness, string> = {
  MissingPlan: 'Missing plan',
  MissingMilestones: 'Missing milestones',
  Ready: 'Ready',
}

const executionStateLabels: Record<RepositoryExecutionState, string> = {
  Ready: 'Ready',
  Executing: 'Executing',
  AwaitingAcceptance: 'Awaiting acceptance',
  Accepted: 'Accepted',
  AwaitingCommit: 'Awaiting commit',
  AwaitingPush: 'Awaiting push',
  Failed: 'Failed',
  Cancelled: 'Cancelled',
}

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
    ...overrides,
  }
}

function renderItem(repository: RepositoryDashboardProjection) {
  render(
    <RepositoryDashboardItemContent
      repository={repository}
      availabilityLabels={availabilityLabels}
      readinessLabels={readinessLabels}
      executionStateLabels={executionStateLabels}
    />,
  )
}

describe('repository dashboard item content rendering characterization', () => {
  it('renders projected repository labels and continuity metadata', () => {
    renderItem(repositoryDashboard())

    expect(screen.getByText('AlphaRepo')).toHaveClass('repository-name')
    expect(screen.getByText('C:/work/AlphaRepo')).toHaveClass('repository-path')
    expect(screen.getByText('Available')).toHaveClass('availability', 'availability-available')
    const readyLabels = screen.getAllByText('Ready')
    expect(readyLabels[0]).toHaveClass('readiness', 'readiness-ready')
    expect(readyLabels[1]).toHaveClass('execution-state', 'execution-state-ready')
    expect(screen.getByText('3 milestones')).toHaveClass('repository-metadata')
    expect(screen.getByText('Handoff present')).toBeInTheDocument()
    expect(screen.getByText('Decisions missing')).toBeInTheDocument()
    expect(screen.getByText('Context present')).toBeInTheDocument()
    expect(screen.getByText(/^Updated /)).toBeInTheDocument()
    expect(screen.getByText('Revisions 4')).toBeInTheDocument()
    expect(screen.getByText('Questions 2')).toBeInTheDocument()
    expect(screen.getByText('Risks 1')).toBeInTheDocument()
  })

  it('renders execution summary metadata only when present', () => {
    renderItem(
      repositoryDashboard({
        executionSummary: executionSummary({
          sessionId: 'session-42',
          state: 'Failed',
          failureReason: 'Provider exited',
        }),
      }),
    )

    expect(screen.getByText('Session session-42')).toBeInTheDocument()
    expect(screen.getByText('State Failed')).toBeInTheDocument()
    expect(screen.getByText(/^Activity /)).toBeInTheDocument()
    expect(screen.getByText('Failure Provider exited')).toHaveClass('failure-metadata')
  })

  it('preserves missing context and not-recorded timestamp fallbacks', () => {
    renderItem(
      repositoryDashboard({
        availability: 'AccessDenied',
        readiness: 'MissingPlan',
        executionState: 'Failed',
        hasCurrentHandoff: false,
        hasCurrentDecisions: false,
        continuitySummary: {
          operationalContextExists: false,
          operationalContextRevisionCount: 0,
          operationalContextLastUpdatedAt: null,
          openQuestionCount: 0,
          activeRiskCount: 0,
          pendingProposalExists: false,
        },
      }),
    )

    expect(screen.getByText('Access denied')).toHaveClass('availability-accessdenied')
    expect(screen.getByText('Missing plan')).toHaveClass('readiness-missingplan')
    expect(screen.getByText('Failed')).toHaveClass('execution-state-failed')
    expect(screen.getByText('Handoff missing')).toBeInTheDocument()
    expect(screen.getByText('Decisions missing')).toBeInTheDocument()
    expect(screen.getByText('Context missing')).toBeInTheDocument()
    expect(screen.getByText('Updated Not recorded')).toBeInTheDocument()
    expect(screen.queryByText(/^Session /)).not.toBeInTheDocument()
  })
})
