import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { WorkspaceInspectorRail } from '../../features/workspace/WorkspaceInspectorRail'
import type {
  CommitPreparation,
  ExecutionSessionSummary,
  OperationalContextProjection,
  OperationalContextProposalSummary,
  RepositoryGitStatus,
} from '../../types'

afterEach(() => {
  cleanup()
})

const gitStatus: RepositoryGitStatus = {
  branch: 'main',
  aheadCount: 2,
  behindCount: 1,
  capturedAt: '2026-06-21T16:00:00.000Z',
  dirtyState: {
    stagedPaths: ['staged.ts'],
    modifiedPaths: ['modified.ts'],
    addedPaths: [],
    deletedPaths: [],
    renamedPaths: [],
    untrackedPaths: ['notes.md'],
    isClean: false,
  },
}

const commitPreparation: CommitPreparation = {
  id: 'prep-1',
  sessionId: 'session-1',
  repositoryId: 'repo-1',
  repositoryPath: 'C:\\repo',
  proposedMessage: 'Update execution artifacts',
  generatedAt: '2026-06-21T16:10:00.000Z',
  hasPreExistingChanges: true,
  statusSnapshot: {
    ...gitStatus,
    id: 'snapshot-1',
    capturedAt: '2026-06-21T16:05:00.000Z',
  },
  scopeItems: [
    {
      path: 'src/App.tsx',
      changeType: 'Modified',
      origin: 'ExecutionGenerated',
      isSelected: true,
    },
    {
      path: '.agents/handoff.md',
      changeType: 'Added',
      origin: 'PreExisting',
      isSelected: true,
    },
  ],
}

const proposalSummary: OperationalContextProposalSummary = {
  pendingProposalExists: true,
  latestProposalId: 'proposal-1',
  generatedAt: '2026-06-21T16:15:00.000Z',
  status: 'Pending',
  sourceInputCount: 3,
  contentByteCount: 1200,
  contentCharacterCount: 1100,
  lastPromotedAt: null,
  lastArchivedRelativePath: null,
}

const operationalContext: OperationalContextProjection = {
  exists: true,
  currentRelativePath: '.agents/operational_context.md',
  revisionCount: 4,
  currentRevisionNumber: 4,
  lastUpdatedAt: '2026-06-21T16:00:00.000Z',
  lastPromotionAt: null,
  currentUnderstandingSummary: [],
  architecture: [],
  authorityBoundaries: [],
  constraints: [],
  stableDecisions: [
    {
      id: 'decision-1',
      kind: 'StableDecision',
      text: 'Keep backend authority.',
      rationale: null,
      sourceRelativePath: null,
    },
    {
      id: 'decision-2',
      kind: 'StableDecision',
      text: 'Keep React presentational.',
      rationale: null,
      sourceRelativePath: null,
    },
  ],
  decisionRationale: [],
  openQuestions: [
    {
      id: 'question-1',
      kind: 'OpenQuestion',
      text: 'Which link anchors remain?',
      rationale: null,
      sourceRelativePath: null,
    },
  ],
  activeRisks: [
    {
      id: 'risk-1',
      kind: 'ActiveRisk',
      text: 'Inspector overreach.',
      rationale: null,
      sourceRelativePath: null,
    },
  ],
  recentUnderstandingChanges: [],
  pendingProposalSummary: proposalSummary,
  latestReviewState: 'PendingReview',
  continuityWarnings: [],
}

const executionSummary: ExecutionSessionSummary = {
  sessionId: 'session-1',
  state: 'Completed',
  repositoryState: 'AwaitingPush',
  milestonePath: '.agents/milestones/m3.md',
  startedAt: '2026-06-21T15:00:00.000Z',
  completedAt: '2026-06-21T16:00:00.000Z',
  duration: '01:00:00',
  acceptedAt: '2026-06-21T16:02:00.000Z',
  rejectedAt: null,
  decisionNote: null,
  lastActivityAt: '2026-06-21T16:05:00.000Z',
  providerName: 'codex',
  providerExecutablePath: null,
  providerProcessId: null,
  providerStartedAt: null,
  handoffPath: '.agents/handoffs/handoff.md',
  commitSha: 'abc123',
  committedAt: '2026-06-21T16:20:00.000Z',
  commitMessage: 'Update execution artifacts',
  preparationSnapshotId: 'snapshot-1',
  pushAttemptedAt: null,
  pushedAt: null,
  pushedCommitSha: null,
  pushRemoteName: null,
  pushBranchName: 'feature/workspace',
  failureReason: null,
}

function renderRail(
  overrides: Partial<Parameters<typeof WorkspaceInspectorRail>[0]> = {},
) {
  const onOpenOperationalContext = vi.fn()
  const onOpenContinuityWarnings = vi.fn()
  const onOpenExecutionSession = vi.fn()

  render(
    <WorkspaceInspectorRail
      currentExecutionState="Ready"
      gitStatus={gitStatus}
      gitStatusPathCount={3}
      isGitStatusLoading={false}
      gitStatusError={null}
      commitPreparation={null}
      isCommitPreparationCurrent={false}
      selectedCommitPathCount={0}
      execution={executionSummary}
      operationalContext={operationalContext}
      proposalSummary={proposalSummary}
      executionHistory={[executionSummary]}
      onOpenOperationalContext={onOpenOperationalContext}
      onOpenContinuityWarnings={onOpenContinuityWarnings}
      onOpenExecutionSession={onOpenExecutionSession}
      {...overrides}
    />,
  )

  return { onOpenOperationalContext, onOpenContinuityWarnings, onOpenExecutionSession }
}

describe('WorkspaceInspectorRail', () => {
  it('renders git status, operational context counts, and execution history summaries', () => {
    renderRail()

    expect(screen.getByText('Branch: main')).toBeInTheDocument()
    expect(screen.getByText('State: Dirty')).toBeInTheDocument()
    expect(screen.getByText('Ahead: 2')).toBeInTheDocument()
    expect(screen.getByText('Behind: 1')).toBeInTheDocument()
    expect(screen.getByText('Changed paths: 3')).toBeInTheDocument()
    expect(screen.getByText('Revisions: 4')).toBeInTheDocument()
    expect(screen.getByText((_, element) => element?.textContent === 'Stable decisions: 2')).toBeInTheDocument()
    expect(screen.getByText((_, element) => element?.textContent === 'Open questions: 1')).toBeInTheDocument()
    expect(screen.getByText((_, element) => element?.textContent === 'Active risks: 1')).toBeInTheDocument()
    expect(screen.getByText((_, element) => element?.textContent === 'Pending proposal: Present')).toBeInTheDocument()
    expect(screen.getAllByText('.agents/milestones/m3.md').length).toBeGreaterThan(0)
  })

  it('renders current commit preparation without exposing commit actions', () => {
    renderRail({
      currentExecutionState: 'AwaitingCommit',
      commitPreparation,
      isCommitPreparationCurrent: true,
      selectedCommitPathCount: 1,
    })

    expect(screen.getByText('Preparation summary: prep-1')).toBeInTheDocument()
    expect(screen.getByText('Snapshot summary: snapshot-1')).toBeInTheDocument()
    expect(screen.getByText('Changed paths: 2')).toBeInTheDocument()
    expect(screen.getByText('Selected scope: 1')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /commit selected/i })).not.toBeInTheDocument()
  })

  it('renders push evidence without exposing push actions', () => {
    renderRail({
      currentExecutionState: 'AwaitingPush',
      gitStatus: null,
    })

    expect(screen.getByText('Commit: abc123')).toBeInTheDocument()
    expect(screen.getByText('Snapshot: snapshot-1')).toBeInTheDocument()
    expect(screen.getByText('Branch: feature/workspace')).toBeInTheDocument()
    expect(screen.getByText('Ahead: Not loaded')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /push commit/i })).not.toBeInTheDocument()
  })

  it('uses the supplied navigation callbacks for operational context sections', () => {
    const { onOpenOperationalContext } = renderRail()

    fireEvent.click(screen.getByRole('button', { name: 'Current' }))
    fireEvent.click(screen.getByRole('button', { name: 'Proposal' }))
    fireEvent.click(screen.getByRole('button', { name: '2' }))
    fireEvent.click(screen.getAllByRole('button', { name: '1' })[0])
    fireEvent.click(screen.getByRole('button', { name: 'Present' }))

    expect(onOpenOperationalContext).toHaveBeenCalledTimes(5)
    expect(onOpenOperationalContext).toHaveBeenNthCalledWith(1, 'operational-current')
    expect(onOpenOperationalContext).toHaveBeenNthCalledWith(2, 'proposal-review')
    expect(onOpenOperationalContext).toHaveBeenNthCalledWith(3, 'operational-stable-decisions')
    expect(onOpenOperationalContext).toHaveBeenNthCalledWith(4, 'operational-open-questions')
    expect(onOpenOperationalContext).toHaveBeenNthCalledWith(5, 'proposal-review')
  })

  it('uses navigation callbacks for continuity warning snippets and execution history', () => {
    const { onOpenContinuityWarnings, onOpenExecutionSession } = renderRail({
      operationalContext: {
        ...operationalContext,
        continuityWarnings: ['Decision continuity warning'],
      },
    })

    fireEvent.click(screen.getByRole('button', { name: 'Decision continuity warning' }))
    fireEvent.click(screen.getByRole('button', { name: /m3\.md/ }))

    expect(onOpenContinuityWarnings).toHaveBeenCalledTimes(1)
    expect(onOpenExecutionSession).toHaveBeenCalledWith(executionSummary)
  })
})
