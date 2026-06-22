import { describe, expect, it } from 'vitest'
import { buildNavigationTargets, getTabForSection } from '../../lib'
import type { RepositoryDashboardProjection, RepositoryWorkspaceProjection } from '../../types'

const executionSummary = {
  sessionId: 'session-1',
  state: 'Completed',
  repositoryState: 'AwaitingCommit',
  milestonePath: '.agents/milestones/m7.md',
  startedAt: null,
  completedAt: null,
  duration: null,
  acceptedAt: null,
  rejectedAt: null,
  decisionNote: null,
  lastActivityAt: null,
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
} as const

const repository = {
  repository: {
    id: 'repo-alpha',
    name: 'AlphaRepo',
    path: 'C:/repos/alpha',
  },
  availability: 'Available',
  readiness: 'Ready',
  executionState: 'AwaitingCommit',
  activeExecutionSession: null,
  executionSummary,
  executionHistory: [executionSummary],
  milestoneCount: 1,
  hasCurrentHandoff: true,
  hasCurrentDecisions: true,
  continuitySummary: {
    operationalContextExists: true,
    operationalContextRevisionCount: 3,
    operationalContextLastUpdatedAt: null,
    openQuestionCount: 1,
    activeRiskCount: 1,
    pendingProposalExists: true,
  },
} satisfies RepositoryDashboardProjection

const proposalSummary = {
  pendingProposalExists: true,
  latestProposalId: 'proposal-1',
  generatedAt: null,
  status: 'Pending',
  sourceInputCount: 1,
  contentByteCount: 10,
  contentCharacterCount: 10,
  lastPromotedAt: null,
  lastArchivedRelativePath: null,
} as const

const workspace = {
  ...repository,
  artifactInventory: {
    plan: null,
    operationalContext: null,
    historicalOperationalContexts: [],
    milestones: [
      {
        relativePath: '.agents/milestones/m7.md',
        name: 'm7.md',
        type: 'Milestone',
        family: 'Milestone',
        versionKind: 'Current',
      },
    ],
    currentHandoff: null,
    historicalHandoffs: [],
    currentDecisions: null,
    historicalDecisions: [],
  },
  hasPlan: true,
  hasOperationalContext: true,
  operationalContextProposalSummary: proposalSummary,
  operationalContext: {
    exists: true,
    currentRelativePath: '.agents/operational_context.md',
    revisionCount: 3,
    currentRevisionNumber: 3,
    lastUpdatedAt: null,
    lastPromotionAt: null,
    currentUnderstandingSummary: [],
    architecture: [],
    authorityBoundaries: [],
    constraints: [],
    stableDecisions: [
      {
        id: 'decision-1',
        kind: 'decision',
        text: 'Navigation is not workflow mutation.',
        rationale: null,
        sourceRelativePath: null,
      },
    ],
    decisionRationale: [],
    openQuestions: [
      {
        id: 'question-1',
        kind: 'question',
        text: 'Which anchors need fallback?',
        rationale: null,
        sourceRelativePath: null,
      },
    ],
    activeRisks: [
      {
        id: 'risk-1',
        kind: 'risk',
        text: 'Fragmented links can drift.',
        rationale: null,
        sourceRelativePath: null,
      },
    ],
    recentUnderstandingChanges: [],
    pendingProposalSummary: proposalSummary,
    latestReviewState: 'PendingReview',
    continuityWarnings: ['Decision rationale needs review.'],
  },
} satisfies RepositoryWorkspaceProjection

describe('navigation targets', () => {
  it('builds palette and discovery targets from existing projections', () => {
    const targets = buildNavigationTargets({
      repositories: [repository],
      selectedRepositoryId: 'repo-alpha',
      workspace,
      executionHistory: [executionSummary],
      continuityDiagnostics: null,
    })

    expect(targets).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          id: 'repository-repo-alpha',
          kind: 'repository',
          repositoryId: 'repo-alpha',
        }),
        expect.objectContaining({
          id: 'repository-repo-alpha-execution',
          tab: 'execution',
        }),
        expect.objectContaining({
          id: 'current-repository-execution',
          label: 'Execution tab',
          tab: 'execution',
        }),
        expect.objectContaining({
          id: 'milestone-.agents/milestones/m7.md',
          kind: 'milestone',
          milestonePath: '.agents/milestones/m7.md',
          sectionId: 'workspace-execution-context',
        }),
        expect.objectContaining({
          id: 'execution-session-session-1',
          kind: 'execution-session',
          tab: 'execution',
        }),
        expect.objectContaining({
          id: 'discovery-pending-proposal',
          sectionId: 'proposal-review',
        }),
        expect.objectContaining({
          id: 'discovery-AwaitingCommit',
          sectionId: 'git-workflow',
        }),
        expect.objectContaining({
          label: 'Which anchors need fallback?',
          sectionId: 'operational-open-questions',
        }),
        expect.objectContaining({
          label: 'Fragmented links can drift.',
          sectionId: 'operational-active-risks',
        }),
        expect.objectContaining({
          label: 'Navigation is not workflow mutation.',
          sectionId: 'operational-stable-decisions',
        }),
        expect.objectContaining({
          label: 'Decision rationale needs review.',
          sectionId: 'continuity-warnings',
        }),
      ]),
    )
  })

  it('maps section anchors to their owning workspace tab', () => {
    expect(getTabForSection('proposal-review')).toBe('operational-context')
    expect(getTabForSection('continuity-warnings')).toBe('continuity')
    expect(getTabForSection('workspace-milestones')).toBe('workspace')
  })
})
