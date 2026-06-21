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
import { ExecutionHistoryPanel } from '../execution/ExecutionHistoryPanel'

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
  onOpenOperationalContext: () => void
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
              onClick={onOpenOperationalContext}
            >
              Open
            </Button>
          }
        />
        <OperationalContextInspectorSummary
          operationalContext={operationalContext}
          proposalSummary={proposalSummary}
        />
      </Panel>

      <ExecutionHistoryPanel sessions={executionHistory} />
    </>
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
}

function OperationalContextInspectorSummary({
  operationalContext,
  proposalSummary,
}: OperationalContextInspectorSummaryProps) {
  if (!operationalContext || !proposalSummary) {
    return <EmptyState className="empty-state">Operational context is not loaded.</EmptyState>
  }

  return (
    <div className="workspace-inspector-summary">
      <span>Revisions: {operationalContext.revisionCount}</span>
      <span>Stable decisions: {operationalContext.stableDecisions.length}</span>
      <span>Open questions: {operationalContext.openQuestions.length}</span>
      <span>Active risks: {operationalContext.activeRisks.length}</span>
      <span>
        Pending proposal: {proposalSummary.pendingProposalExists ? 'Present' : 'None'}
      </span>
      <span>Status: {proposalSummary.status ?? 'None'}</span>
      <span>Last promoted: {formatDateTime(proposalSummary.lastPromotedAt)}</span>
    </div>
  )
}
