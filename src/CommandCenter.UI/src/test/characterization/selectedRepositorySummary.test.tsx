import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { SelectedRepositorySummary } from '../../features/repositories/SelectedRepositorySummary'
import type {
  ExecutionSessionSummary,
  RepositoryDashboardProjection,
  RepositoryExecutionState,
  RepositoryWorkspaceProjection,
  WorkflowInstance,
} from '../../types'

afterEach(() => {
  cleanup()
})

function executionSummary(overrides: Partial<ExecutionSessionSummary> = {}): ExecutionSessionSummary {
  return {
    sessionId: 'session-alpha',
    state: 'Completed',
    repositoryState: 'AwaitingCommit',
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

function workflowProjection(overrides: Partial<WorkflowInstance> = {}): WorkflowInstance {
  return {
    currentStage: 'Decision',
    progressState: 'WaitingForHuman',
    blockingGate: 'DecisionResolution',
    requiredHumanAction: 'Resolve the generated decision.',
    timeline: [
      {
        eventType: 'ExecutionCompleted',
        stage: 'Handoff',
        occurredAt: '2026-06-21T16:20:00.000Z',
        summary: 'Execution completed and produced a handoff.',
        sourceDomain: 'Execution',
        sourceArtifact: '.agents/handoffs/handoff.md',
        fingerprint: 'handoff-fingerprint',
      },
      {
        eventType: 'DecisionGenerated',
        stage: 'Decision',
        occurredAt: '2026-06-21T16:25:00.000Z',
        summary: 'Decision proposal generated from workflow evidence.',
        sourceDomain: 'Decisions',
        sourceArtifact: '.agents/decisions/decisions.md',
        fingerprint: 'decision-fingerprint',
      },
    ],
    ...overrides,
  } as WorkflowInstance
}

function renderSummary({
  repository = repositoryDashboard(),
  workspace = null,
  workflow = null,
  executionDisplay = null,
  currentExecutionState = 'Ready',
  onOpenExecution,
  onOpenGovernance,
  onOpenReasoning,
  onOpenContinuity,
  onOpenMilestones,
  onOpenOperationalContext,
  onOpenHandoffArtifact,
}: {
  repository?: RepositoryDashboardProjection
  workspace?: RepositoryWorkspaceProjection | null
  workflow?: WorkflowInstance | null
  executionDisplay?: ExecutionSessionSummary | null
  currentExecutionState?: RepositoryExecutionState
  onOpenExecution?: () => void
  onOpenGovernance?: () => void
  onOpenReasoning?: () => void
  onOpenContinuity?: () => void
  onOpenMilestones?: () => void
  onOpenOperationalContext?: () => void
  onOpenHandoffArtifact?: (handoffPath: string) => void
} = {}) {
  render(
    <SelectedRepositorySummary
      repository={repository}
      workspace={workspace}
      workflow={workflow}
      executionDisplay={executionDisplay}
      currentExecutionState={currentExecutionState}
      onOpenExecution={onOpenExecution}
      onOpenGovernance={onOpenGovernance}
      onOpenReasoning={onOpenReasoning}
      onOpenContinuity={onOpenContinuity}
      onOpenMilestones={onOpenMilestones}
      onOpenOperationalContext={onOpenOperationalContext}
      onOpenHandoffArtifact={onOpenHandoffArtifact}
    />,
  )
}

function detailsList() {
  const list = document.querySelector('.details-list')
  expect(list).not.toBeNull()
  return within(list as HTMLElement)
}

function dashboardSection(name: string) {
  return within(screen.getByRole('region', { name: `${name} dashboard summary` }))
}

function expectFact(
  section: ReturnType<typeof dashboardSection>,
  label: string,
  expected: string | RegExp,
) {
  expect(section.getByText(label).parentElement).toHaveTextContent(expected)
}

describe('selected repository summary rendering characterization', () => {
  it('renders selected repository identity and dashboard fallbacks when workspace is absent', () => {
    renderSummary()

    const details = detailsList()
    const repository = dashboardSection('Repository')
    const workflow = dashboardSection('Workflow')
    expect(screen.getByText('Selected repository')).toHaveClass('eyebrow')
    expect(screen.getByRole('heading', { level: 3, name: 'AlphaRepo' })).toBeInTheDocument()
    expect(screen.getByText('Available')).toHaveClass('cc-badge', 'cc-badge-success')
    expect(details.getByText('Path').nextElementSibling).toHaveTextContent('C:/work/AlphaRepo')
    expect(details.getByText('Readiness').nextElementSibling).toHaveTextContent('Ready')
    expect(details.getByText('Execution').nextElementSibling).toHaveTextContent('Ready')
    expect(details.getByText('Milestones').nextElementSibling).toHaveTextContent('3')
    expect(screen.getByRole('heading', { level: 4, name: 'Repository operating picture' })).toBeInTheDocument()
    expectFact(repository, 'Plan', 'Missing')
    expectFact(repository, 'Handoff', 'Missing')
    expectFact(repository, 'Decisions', 'Missing')
    expectFact(workflow, 'Stage', 'Not loaded')
  })

  it('uses workspace facts over dashboard facts when workspace is present', () => {
    renderSummary({
      repository: repositoryDashboard({ readiness: 'Ready', milestoneCount: 3 }),
      workspace: workspaceProjection(),
      currentExecutionState: 'AwaitingCommit',
    })

    const details = detailsList()
    const repository = dashboardSection('Repository')
    const workflow = dashboardSection('Workflow')
    const operationalContext = dashboardSection('Operational context')
    expect(details.getByText('Readiness').nextElementSibling).toHaveTextContent(
      'Missing milestones',
    )
    expect(details.getByText('Execution').nextElementSibling).toHaveTextContent('Awaiting commit')
    expectFact(workflow, 'Stage', 'Not loaded')
    expectFact(workflow, 'Gate', 'Not loaded')
    expect(details.getByText('Milestones').nextElementSibling).toHaveTextContent('5')
    expectFact(repository, 'Plan', 'Present')
    expectFact(operationalContext, 'Current context', 'Present')
    expectFact(repository, 'Handoff', 'Missing')
    expectFact(repository, 'Decisions', 'Present')
  })

  it('renders dashboard workflow summary from the authoritative workflow projection', () => {
    renderSummary({
      workspace: workspaceProjection(),
      workflow: workflowProjection(),
    })

    const workflow = dashboardSection('Workflow')
    expectFact(workflow, 'Stage', 'Decision')
    expectFact(workflow, 'Gate', 'DecisionResolution')
    expectFact(workflow, 'Required action', 'Resolve the generated decision.')
    expectFact(workflow, 'Timeline events', '2')
  })

  it('renders repository governance as a contextual decision-session summary', () => {
    const onOpenGovernance = vi.fn()

    renderSummary({
      onOpenGovernance,
      repository: repositoryDashboard({
        decisionSessionSummary: {
          ...decisionSessionSummary,
          decisionSessionId: 'governance-session-1',
          state: 'Active',
          lifecycleDecision: 'Transfer',
          transferEligibilityStatus: 'Blocked',
          coherenceScore: 0.82,
          transferPressure: 0.71,
          cacheMissRisk: 0.44,
          healthDimensions: [
            {
              name: 'Lifecycle',
              status: 'Warning',
              findings: ['Transfer recommended but blocked.'],
            },
            {
              name: 'Continuity',
              status: 'Healthy',
              findings: [],
            },
          ],
          diagnostics: ['Detailed governance health diagnostic belongs in Governance.'],
          generatedAt: '2026-06-21T17:38:00.000Z',
        },
      }),
    })

    const governance = dashboardSection('Governance')
    const health = dashboardSection('Health')
    expectFact(governance, 'Session', 'governance-session-1')
    expectFact(governance, 'State', 'Active')
    expectFact(governance, 'Lifecycle decision', 'Transfer')
    expectFact(governance, 'Transfer eligibility', 'Blocked')
    expectFact(health, 'Governance dimensions', '2')
    expectFact(health, 'Governance findings', '1')
    expectFact(health, 'Assessed', /\d/)
    expect(screen.queryByText('Transfer recommended but blocked.')).not.toBeInTheDocument()
    expect(screen.queryByText('Detailed governance health diagnostic belongs in Governance.')).not.toBeInTheDocument()
    expect(screen.queryByText('Coherence: 0.82')).not.toBeInTheDocument()
    expect(screen.queryByText('Transfer pressure: 0.71')).not.toBeInTheDocument()
    expect(screen.queryByText('Cache miss risk: 0.44')).not.toBeInTheDocument()

    fireEvent.click(governance.getByRole('button', { name: 'governance-session-1' }))
    fireEvent.click(governance.getByRole('button', { name: 'Open' }))
    expect(onOpenGovernance).toHaveBeenCalledTimes(2)
  })

  it('renders reasoning as a compact contextual summary with navigation only', () => {
    const onOpenReasoning = vi.fn()

    renderSummary({
      onOpenReasoning,
      workspace: workspaceProjection({
        reasoningSummary: {
          eventCount: 8,
          threadCount: 3,
          relationshipCount: 5,
          hypothesisEventCount: 2,
          alternativeEventCount: 1,
          contradictionEventCount: 1,
          directionEventCount: 1,
          decisionEvolutionEventCount: 1,
          assumptionEvolutionEventCount: 1,
          constraintEvolutionEventCount: 1,
          evidenceEventCount: 1,
          lastEventAt: '2026-06-21T17:35:00.000Z',
          lastThreadActivityAt: '2026-06-21T17:34:00.000Z',
          lastRelationshipAt: '2026-06-21T17:33:00.000Z',
          lastActivityAt: '2026-06-21T17:35:00.000Z',
          lastReconstructionAt: '2026-06-21T17:36:00.000Z',
          lastCertificationAt: '2026-06-21T17:37:00.000Z',
          certificationResult: 'Passed',
        },
      }),
    })

    const reasoning = dashboardSection('Reasoning')
    const certification = dashboardSection('Certification')
    expectFact(reasoning, 'Events', '8')
    expectFact(reasoning, 'Threads', '3')
    expectFact(reasoning, 'Relationships', '5')
    expectFact(reasoning, 'Latest activity', /\d/)
    expectFact(certification, 'Reasoning result', 'Passed')
    expectFact(certification, 'Latest run', /\d/)
    expect(screen.queryByText('Reconstruction confidence rationale')).not.toBeInTheDocument()
    expect(screen.queryByText('Known unreachable reconstruction evidence')).not.toBeInTheDocument()
    expect(screen.queryByText('Reasoning graph authority')).not.toBeInTheDocument()
    expect(screen.queryByText('Reasoning materialization authority')).not.toBeInTheDocument()

    fireEvent.click(reasoning.getByRole('button', { name: 'Open' }))
    expect(onOpenReasoning).toHaveBeenCalledTimes(1)
  })

  it('renders continuity as compact contextual status with diagnostics navigation only', () => {
    const onOpenContinuity = vi.fn()

    renderSummary({
      onOpenContinuity,
      workspace: workspaceProjection({
        operationalContextProposalSummary: {
          pendingProposalExists: true,
          latestProposalId: 'proposal-1',
          generatedAt: '2026-06-21T17:40:00.000Z',
          status: 'Pending',
          sourceInputCount: 4,
          contentByteCount: 2400,
          contentCharacterCount: 2200,
          lastPromotedAt: null,
          lastArchivedRelativePath: null,
        },
        operationalContext: {
          ...workspaceProjection().operationalContext,
          revisionCount: 7,
          currentRevisionNumber: 7,
          lastUpdatedAt: '2026-06-21T17:45:00.000Z',
          stableDecisions: [
            {
              id: 'decision-1',
              kind: 'StableDecision',
              text: 'Backend continuity services own semantic diff detail.',
              rationale: 'Detailed rationale belongs in Continuity.',
              sourceRelativePath: null,
            },
          ],
          openQuestions: [
            {
              id: 'question-1',
              kind: 'OpenQuestion',
              text: 'Should summaries repeat semantic diff evidence?',
              rationale: null,
              sourceRelativePath: null,
            },
          ],
          activeRisks: [
            {
              id: 'risk-1',
              kind: 'ActiveRisk',
              text: 'Secondary summary could duplicate diagnostics.',
              rationale: null,
              sourceRelativePath: null,
            },
          ],
          continuityWarnings: ['Detailed continuity warning stays in Continuity.'],
        },
      }),
    })

    const operationalContext = dashboardSection('Operational context')
    const diagnostics = dashboardSection('Diagnostics')
    expectFact(operationalContext, 'Revisions', '7')
    expectFact(diagnostics, 'Continuity warnings', '1')
    expectFact(operationalContext, 'Pending proposal', 'Present')
    expectFact(operationalContext, 'Latest activity', /\d/)
    expect(screen.queryByText('Backend continuity services own semantic diff detail.')).not.toBeInTheDocument()
    expect(screen.queryByText('Detailed rationale belongs in Continuity.')).not.toBeInTheDocument()
    expect(screen.queryByText('Should summaries repeat semantic diff evidence?')).not.toBeInTheDocument()
    expect(screen.queryByText('Secondary summary could duplicate diagnostics.')).not.toBeInTheDocument()
    expect(screen.queryByText('Detailed continuity warning stays in Continuity.')).not.toBeInTheDocument()

    fireEvent.click(diagnostics.getByRole('button', { name: 'Open' }))
    expect(onOpenContinuity).toHaveBeenCalledTimes(1)
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

    const scopedDetails = detailsList()
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

    const details = detailsList()
    const repository = dashboardSection('Repository')
    const operationalContext = dashboardSection('Operational context')

    fireEvent.click(details.getByRole('button', { name: 'Awaiting commit' }))
    fireEvent.click(details.getByRole('button', { name: 'session-alpha' }))
    fireEvent.click(repository.getByRole('button', { name: '5' }))
    fireEvent.click(operationalContext.getByRole('button', { name: 'Open' }))
    fireEvent.click(details.getByRole('button', { name: '.agents/handoffs/handoff.md' }))

    expect(onOpenExecution).toHaveBeenCalledTimes(2)
    expect(onOpenMilestones).toHaveBeenCalledTimes(1)
    expect(onOpenOperationalContext).toHaveBeenCalledTimes(1)
    expect(onOpenHandoffArtifact).toHaveBeenCalledWith('.agents/handoffs/handoff.md')
  })
})
