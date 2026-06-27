import { describe, expect, it } from 'vitest'
import {
  buildNavigationTargets,
  getTabForSection,
  globalNavigationItems,
  navigationSectionTargets,
  workspaceTabDefinitions,
} from '../../lib'
import type { RepositoryDashboardProjection, RepositoryWorkspaceProjection } from '../../types'

const executionSummary = {
  sessionId: 'session-1',
  state: 'Completed',
  repositoryState: 'AwaitingCommit',
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
          id: 'repository-repo-alpha-reasoning',
          label: 'AlphaRepo Reasoning',
          tab: 'reasoning',
        }),
        expect.objectContaining({
          id: 'section-reasoning-event-feed',
          label: 'Reasoning Event Feed',
          sectionId: 'reasoning-event-feed',
          tab: 'reasoning',
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
    expect(getTabForSection('reasoning-thread-view')).toBe('reasoning')
    expect(getTabForSection('workflow-operations')).toBe('workspace')
    expect(getTabForSection('workspace-milestones')).toBe('workspace')
  })

  it('keeps one primary workspace destination per major capability', () => {
    expect(workspaceTabDefinitions.map((tab) => tab.id)).toEqual([
      'workspace',
      'execution',
      'operational-context',
      'governance',
      'decisions',
      'reasoning',
      'continuity',
    ])

    const primaryTabs = workspaceTabDefinitions.filter((tab) => tab.classification === 'primary')
    expect(primaryTabs).toHaveLength(workspaceTabDefinitions.length)
    expect(new Set(primaryTabs.map((tab) => tab.id)).size).toBe(primaryTabs.length)
  })

  it('keeps deep-link sections contextual and uniquely anchored', () => {
    expect(navigationSectionTargets.every((target) => target.classification === 'contextual')).toBe(true)
    expect(
      new Set(navigationSectionTargets.map((target) => target.sectionId)).size,
    ).toBe(navigationSectionTargets.length)

    expect(navigationSectionTargets).toEqual(
      expect.arrayContaining([
        expect.objectContaining({ sectionId: 'governance-workspace', tab: 'governance' }),
        expect.objectContaining({ sectionId: 'workflow-operations', tab: 'workspace' }),
        expect.objectContaining({ sectionId: 'decision-lifecycle', tab: 'decisions' }),
        expect.objectContaining({ sectionId: 'reasoning-trajectory', tab: 'reasoning' }),
        expect.objectContaining({ sectionId: 'continuity-diagnostics', tab: 'continuity' }),
      ]),
    )
  })

  it('only exposes implemented global navigation entries', () => {
    expect(globalNavigationItems).toEqual([
      {
        id: 'repositories',
        label: 'Repositories',
        classification: 'primary',
      },
    ])
  })
})
