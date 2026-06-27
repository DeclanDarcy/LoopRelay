import { Button, EmptyState, Panel, SectionHeader, StatusBadge } from '../../components/design'
import { formatDateTime } from '../../lib'
import { repositoryExecutionStatus } from '../../lib/status'
import type {
  CommitPreparation,
  ExecutionSessionSummary,
  OperationalContextProjection,
  OperationalContextProposalSummary,
  RepositoryExecutionState,
  RepositoryGitStatus,
} from '../../types'

type OperationalContextSectionId =
  | 'operational-current'
  | 'proposal-review'
  | 'operational-stable-decisions'
  | 'operational-open-questions'
  | 'operational-active-risks'

type WorkspaceInspectorRailProps = {
  currentExecutionState: RepositoryExecutionState
  gitStatus: RepositoryGitStatus | null
  gitStatusPathCount: number
  isGitStatusLoading: boolean
  gitStatusError: string | null
  commitPreparation: CommitPreparation | null
  isCommitPreparationCurrent: boolean
  selectedCommitPathCount: number
  execution: ExecutionSessionSummary | null
  operationalContext: OperationalContextProjection | null
  proposalSummary: OperationalContextProposalSummary | null
  executionHistory: ExecutionSessionSummary[]
  onOpenOperationalContext: (sectionId: OperationalContextSectionId) => void
  onOpenContinuityWarnings?: () => void
  onOpenExecutionSession?: (session: ExecutionSessionSummary) => void
}

export function WorkspaceInspectorRail({
  currentExecutionState,
  gitStatus,
  gitStatusPathCount,
  isGitStatusLoading,
  gitStatusError,
  commitPreparation,
  isCommitPreparationCurrent,
  selectedCommitPathCount,
  execution,
  operationalContext,
  proposalSummary,
  executionHistory,
  onOpenOperationalContext,
  onOpenContinuityWarnings,
  onOpenExecutionSession,
}: WorkspaceInspectorRailProps) {
  return (
    <>
      <Panel className="workspace-inspector-panel" aria-label="Commit and push summary">
        <SectionHeader
          eyebrow="Workspace Git"
          title="Commit and Push"
          headingLevel={4}
          actions={<StatusBadge status={repositoryExecutionStatus[currentExecutionState]} />}
        />
        <CommitPushInspectorSummary
          currentExecutionState={currentExecutionState}
          gitStatus={gitStatus}
          gitStatusPathCount={gitStatusPathCount}
          isGitStatusLoading={isGitStatusLoading}
          gitStatusError={gitStatusError}
          commitPreparation={commitPreparation}
          isCommitPreparationCurrent={isCommitPreparationCurrent}
          selectedCommitPathCount={selectedCommitPathCount}
          execution={execution}
        />
      </Panel>

      <Panel className="workspace-inspector-panel" aria-label="Operational context summary">
        <SectionHeader
          eyebrow="Operational Context"
          title="Understanding"
          headingLevel={4}
          actions={
            <Button
              type="button"
              variant="secondary"
              className="secondary-action"
              onClick={() => onOpenOperationalContext('operational-current')}
            >
              Current
            </Button>
          }
        />
        <OperationalContextInspectorSummary
          operationalContext={operationalContext}
          proposalSummary={proposalSummary}
          onOpenOperationalContext={onOpenOperationalContext}
          onOpenContinuityWarnings={onOpenContinuityWarnings}
        />
        <div className="workspace-inspector-actions">
          <Button
            type="button"
            variant="secondary"
            className="secondary-action"
            onClick={() => onOpenOperationalContext('proposal-review')}
          >
            Proposal
          </Button>
        </div>
      </Panel>

      <ExecutionHistorySummaryPanel
        sessions={executionHistory}
        onOpenSession={onOpenExecutionSession}
      />
    </>
  )
}

type ExecutionHistorySummaryPanelProps = {
  sessions: ExecutionSessionSummary[]
  onOpenSession?: (session: ExecutionSessionSummary) => void
}

function ExecutionHistorySummaryPanel({
  sessions,
  onOpenSession,
}: ExecutionHistorySummaryPanelProps) {
  if (sessions.length === 0) {
    return null
  }

  const latestSession = sessions[0]
  const completedCount = sessions.filter((session) => session.state === 'Completed').length
  const failedCount = sessions.filter((session) => session.state === 'Failed').length

  return (
    <Panel className="workspace-inspector-panel" aria-label="Execution history summary">
      <SectionHeader
        eyebrow="Execution Sessions"
        title={`${sessions.length} recent sessions`}
        headingLevel={4}
        actions={
          onOpenSession ? (
            <Button
              type="button"
              variant="secondary"
              className="secondary-action"
              onClick={() => onOpenSession(latestSession)}
            >
              Open in Execution
            </Button>
          ) : null
        }
      />
      <div className="workspace-inspector-summary">
        <span>Latest session: {latestSession.sessionId}</span>
        <span>Latest state: {repositoryExecutionStatus[latestSession.repositoryState].label}</span>
        <span>Last activity: {formatDateTime(latestSession.lastActivityAt)}</span>
        <span>Completed: {completedCount}</span>
        <span>Failed: {failedCount}</span>
      </div>
    </Panel>
  )
}

type CommitPushInspectorSummaryProps = {
  currentExecutionState: RepositoryExecutionState
  gitStatus: RepositoryGitStatus | null
  gitStatusPathCount: number
  isGitStatusLoading: boolean
  gitStatusError: string | null
  commitPreparation: CommitPreparation | null
  isCommitPreparationCurrent: boolean
  selectedCommitPathCount: number
  execution: ExecutionSessionSummary | null
}

function CommitPushInspectorSummary({
  currentExecutionState,
  gitStatus,
  gitStatusPathCount,
  isGitStatusLoading,
  gitStatusError,
  commitPreparation,
  isCommitPreparationCurrent,
  selectedCommitPathCount,
  execution,
}: CommitPushInspectorSummaryProps) {
  if (currentExecutionState === 'AwaitingCommit') {
    if (isCommitPreparationCurrent && commitPreparation) {
      return (
        <div className="workspace-inspector-summary">
          <span>Preparation summary: {commitPreparation.id}</span>
          <span>Snapshot summary: {commitPreparation.statusSnapshot.id}</span>
          <span>Branch: {commitPreparation.statusSnapshot.branch || '(detached)'}</span>
          <span>Changed paths: {commitPreparation.scopeItems.length}</span>
          <span>Selected scope: {selectedCommitPathCount}</span>
          <span>
            Pre-existing:{' '}
            {commitPreparation.hasPreExistingChanges ? 'Present' : 'None detected'}
          </span>
          <span>Generated: {formatDateTime(commitPreparation.generatedAt)}</span>
        </div>
      )
    }

    return <EmptyState className="empty-state">Commit preparation summary is not loaded.</EmptyState>
  }

  if (currentExecutionState === 'AwaitingPush') {
    if (execution?.commitSha) {
      return (
        <div className="workspace-inspector-summary">
          <span>Commit: {execution.commitSha}</span>
          <span>Committed: {formatDateTime(execution.committedAt)}</span>
          <span>Snapshot: {execution.preparationSnapshotId ?? 'Not recorded'}</span>
          <span>Branch: {gitStatus?.branch || execution.pushBranchName || '(unknown)'}</span>
          <span>Ahead: {gitStatus?.aheadCount ?? 'Not loaded'}</span>
          <span>Behind: {gitStatus?.behindCount ?? 'Not loaded'}</span>
        </div>
      )
    }

    return <EmptyState className="empty-state">No committed execution is available to push.</EmptyState>
  }

  if (gitStatus) {
    return (
      <div className="workspace-inspector-summary">
        <span>Branch: {gitStatus.branch || '(detached)'}</span>
        <span>State: {gitStatus.dirtyState.isClean ? 'Clean' : 'Dirty'}</span>
        <span>Ahead: {gitStatus.aheadCount}</span>
        <span>Behind: {gitStatus.behindCount}</span>
        <span>Changed paths: {gitStatusPathCount}</span>
        <span>Captured: {formatDateTime(gitStatus.capturedAt)}</span>
      </div>
    )
  }

  return (
    <EmptyState className="empty-state">
      {gitStatusError ?? (isGitStatusLoading ? 'Loading Git status...' : 'Git status is not loaded.')}
    </EmptyState>
  )
}

type OperationalContextInspectorSummaryProps = {
  operationalContext: OperationalContextProjection | null
  proposalSummary: OperationalContextProposalSummary | null
  onOpenOperationalContext?: (sectionId: OperationalContextSectionId) => void
  onOpenContinuityWarnings?: () => void
}

function OperationalContextInspectorSummary({
  operationalContext,
  proposalSummary,
  onOpenOperationalContext,
  onOpenContinuityWarnings,
}: OperationalContextInspectorSummaryProps) {
  if (!operationalContext || !proposalSummary) {
    return <EmptyState className="empty-state">Operational context is not loaded.</EmptyState>
  }

  const warningCount = operationalContext.continuityWarnings.length

  return (
    <div className="workspace-inspector-summary">
      <span>Revisions: {operationalContext.revisionCount}</span>
      <span>Current revision: {operationalContext.currentRevisionNumber}</span>
      <span>
        Continuity warnings:{' '}
        {warningCount > 0 && onOpenContinuityWarnings ? (
          <button
            type="button"
            className="workspace-cross-link inline-cross-link warning-link"
            onClick={onOpenContinuityWarnings}
          >
            {warningCount}
          </button>
        ) : (
          warningCount
        )}
      </span>
      <span>
        Pending proposal:{' '}
        {proposalSummary.pendingProposalExists && onOpenOperationalContext ? (
          <button
            type="button"
            className="workspace-cross-link inline-cross-link"
            onClick={() => onOpenOperationalContext('proposal-review')}
          >
            Present
          </button>
        ) : proposalSummary.pendingProposalExists ? (
          'Present'
        ) : (
          'None'
        )}
      </span>
      <span>Status: {proposalSummary.status ?? 'None'}</span>
      <span>Last updated: {formatDateTime(operationalContext.lastUpdatedAt)}</span>
      <span>Last promoted: {formatDateTime(proposalSummary.lastPromotedAt)}</span>
    </div>
  )
}
