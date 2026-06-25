import { cleanup, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import {
  CommitPreparationSummary,
  CommitPreparationChangeBuckets,
  GitEligibilitySummary,
  GitStatusDetails,
  PushReviewSummary,
} from '../../features/execution/GitWorkflowEvidence'
import type {
  CommitPreparation,
  ExecutionGitActionEligibility,
  ExecutionSessionSummary,
  RepositoryGitStatus,
} from '../../types'

afterEach(() => {
  cleanup()
})

const dirtyGitStatus: RepositoryGitStatus = {
  branch: 'main',
  aheadCount: 2,
  behindCount: 1,
  capturedAt: '2026-06-21T16:00:00.000Z',
  dirtyState: {
    stagedPaths: ['staged.ts'],
    modifiedPaths: ['modified.ts'],
    addedPaths: ['added.ts'],
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
    ...dirtyGitStatus,
    id: 'snapshot-1',
    branch: '',
    capturedAt: '2026-06-21T16:05:00.000Z',
  },
  scopeItems: [
    {
      path: 'src/App.tsx',
      changeType: 'Modified',
      origin: 'ExecutionGenerated',
      originBasis: 'Path was absent from the launch-time dirty snapshot and appeared after execution.',
      isSelected: true,
    },
    {
      path: '.agents/handoff.md',
      changeType: 'Added',
      origin: 'PreExisting',
      originBasis: 'Path was dirty in the launch-time repository snapshot captured before execution.',
      isSelected: true,
    },
  ],
}

const executionSummary: ExecutionSessionSummary = {
  sessionId: 'session-1',
  state: 'Completed',
  repositoryState: 'AwaitingPush',
  milestonePath: '.agents/milestones/m0.md',
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
  preparationSnapshotId: null,
  pushAttemptedAt: null,
  pushedAt: null,
  pushedCommitSha: null,
  pushRemoteName: null,
  pushBranchName: 'feature/git-workflow',
  failureReason: null,
}

const gitEligibility: ExecutionGitActionEligibility = {
  sessionId: 'session-1',
  sessionExists: true,
  repositoryState: 'AwaitingPush',
  commitPreparationLoaded: true,
  commitPreparationCurrent: false,
  commitPreparationId: 'prep-1',
  preparedStatusSnapshotId: 'snapshot-1',
  currentStatusSnapshotId: 'snapshot-2',
  selectedPathCount: 1,
  preparedPathCount: 2,
  unknownSelectedPaths: ['unknown.ts'],
  commitMessagePresent: false,
  repositoryAllowsCommit: false,
  awaitingPush: true,
  commitShaExists: true,
  commitSha: 'abc123',
  previousPushAttemptedAt: '2026-06-21T16:30:00.000Z',
  previousPushFailure: 'git push failed: rejected by remote',
  remoteBranchState: {
    branch: 'main',
    aheadCount: 1,
    behindCount: 1,
    hasUnpushedChanges: true,
    hasRemoteDivergence: true,
    capturedAt: '2026-06-21T16:31:00.000Z',
  },
  canCommit: false,
  canPush: false,
  commitDisabledReasons: ['Commit preparation is stale.', 'Commit message is required.'],
  pushDisabledReasons: ['Remote branch has new commits; review branch state before pushing.'],
  diagnostics: ['Commit status snapshot unavailable: git status failed'],
}

describe('git workflow evidence rendering characterization', () => {
  it('renders commit preparation metadata with current labels and fallbacks', () => {
    render(<CommitPreparationSummary preparation={commitPreparation} selectedPathCount={1} />)

    expect(screen.getByText('Preparation: prep-1')).toBeInTheDocument()
    expect(screen.getByText('Snapshot: snapshot-1')).toBeInTheDocument()
    expect(screen.getByText('Branch: (detached)')).toBeInTheDocument()
    expect(screen.getByText('Changed paths: 2')).toBeInTheDocument()
    expect(screen.getByText('Selected: 1')).toBeInTheDocument()
    expect(screen.getByText('Execution-generated: 1')).toBeInTheDocument()
    expect(screen.getByText('Pre-existing paths: 1')).toBeInTheDocument()
    expect(screen.getByText('Pre-existing: Present')).toBeInTheDocument()
    expect(screen.getByText(/^Generated:/)).toBeInTheDocument()
    expect(screen.getByText(/^Captured:/)).toBeInTheDocument()
  })

  it('renders classified commit path buckets with backend origin basis', () => {
    render(<CommitPreparationChangeBuckets preparation={commitPreparation} />)

    const buckets = screen.getByLabelText('Classified commit paths')
    expect(within(buckets).getByRole('heading', { level: 5, name: 'Modified' })).toBeInTheDocument()
    expect(within(buckets).getByRole('heading', { level: 5, name: 'Added' })).toBeInTheDocument()
    expect(within(buckets).getByText('src/App.tsx')).toBeInTheDocument()
    expect(within(buckets).getByText('.agents/handoff.md')).toBeInTheDocument()
    expect(within(buckets).getByText('Execution generated')).toBeInTheDocument()
    expect(within(buckets).getByText('Pre-existing')).toBeInTheDocument()
    expect(
      within(buckets).getByText(
        'Path was absent from the launch-time dirty snapshot and appeared after execution.',
      ),
    ).toBeInTheDocument()
    expect(
      within(buckets).getByText(
        'Path was dirty in the launch-time repository snapshot captured before execution.',
      ),
    ).toBeInTheDocument()
  })

  it('renders push review metadata from git status when loaded', () => {
    render(<PushReviewSummary execution={executionSummary} gitStatus={dirtyGitStatus} />)

    expect(screen.getByText('Commit: abc123')).toBeInTheDocument()
    expect(screen.getByText('Snapshot: Not recorded')).toBeInTheDocument()
    expect(screen.getByText('Branch: main')).toBeInTheDocument()
    expect(screen.getByText('Ahead: 2')).toBeInTheDocument()
    expect(screen.getByText('State: Awaiting push')).toBeInTheDocument()
    expect(screen.getByText('Last push attempt: Not recorded')).toBeInTheDocument()
    expect(screen.getByText('Previous push failure: None recorded')).toBeInTheDocument()
    expect(screen.getByText(/^Committed:/)).toBeInTheDocument()
  })

  it('renders previous push failure and retry timestamp', () => {
    render(
      <PushReviewSummary
        execution={{
          ...executionSummary,
          lastActivityAt: '2026-06-21T16:30:00.000Z',
          pushAttemptedAt: '2026-06-21T16:30:00.000Z',
          failureReason: 'git push failed: rejected by remote',
        }}
        gitStatus={dirtyGitStatus}
      />,
    )

    expect(screen.getByText(/^Last push attempt:/)).toBeInTheDocument()
    expect(screen.getByText('Previous push failure: git push failed: rejected by remote')).toBeInTheDocument()
  })

  it('renders backend-owned git eligibility and disabled reasons', () => {
    render(<GitEligibilitySummary eligibility={gitEligibility} mode="push" />)

    expect(screen.getByLabelText('Git eligibility')).toBeInTheDocument()
    expect(screen.getByText('Eligibility: Blocked')).toBeInTheDocument()
    expect(screen.getByText('Preparation current: No')).toBeInTheDocument()
    expect(screen.getByText('Commit message: Missing')).toBeInTheDocument()
    expect(screen.getByText('Previous push failure: git push failed: rejected by remote')).toBeInTheDocument()
    expect(screen.getByText('Remote branch: main')).toBeInTheDocument()
    expect(screen.getByText('Remote behind: 1')).toBeInTheDocument()
    expect(screen.getAllByText('Remote branch has new commits; review branch state before pushing.')).toHaveLength(2)
    expect(screen.getAllByText('unknown.ts')).toHaveLength(3)
    expect(screen.getByText('Commit status snapshot unavailable: git status failed')).toBeInTheDocument()
    expect(screen.getByText('Push Eligibility')).toBeInTheDocument()
    expect(screen.getByText('Git Eligibility Diagnostics')).toBeInTheDocument()
  })

  it('renders push review branch and ahead fallbacks when git status is missing', () => {
    render(<PushReviewSummary execution={executionSummary} gitStatus={null} />)

    expect(screen.getByText('Branch: feature/git-workflow')).toBeInTheDocument()
    expect(screen.getByText('Ahead: Not loaded')).toBeInTheDocument()
  })

  it('renders git status summary and dirty path buckets', () => {
    render(<GitStatusDetails gitStatus={dirtyGitStatus} changedPathCount={4} />)

    expect(screen.getByText('Branch: main')).toBeInTheDocument()
    expect(screen.getByText('State: Dirty')).toBeInTheDocument()
    expect(screen.getByText('Ahead: 2')).toBeInTheDocument()
    expect(screen.getByText('Behind: 1')).toBeInTheDocument()
    expect(screen.getByText('Changed paths: 4')).toBeInTheDocument()
    expect(screen.getByText(/^Captured:/)).toBeInTheDocument()

    const columns = screen.getByText('Staged').closest('.context-columns')
    expect(columns).not.toBeNull()
    if (!columns) {
      return
    }

    const columnContainer = columns as HTMLElement
    expect(within(columnContainer).getByText('staged.ts')).toBeInTheDocument()
    expect(within(columnContainer).getByText('modified.ts')).toBeInTheDocument()
    expect(within(columnContainer).getByText('added.ts')).toBeInTheDocument()
    expect(within(columnContainer).getByText('notes.md')).toBeInTheDocument()
  })
})
