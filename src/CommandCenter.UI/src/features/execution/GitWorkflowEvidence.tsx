import type { CommitPreparation, ExecutionSessionSummary, RepositoryGitStatus } from '../../types'
import { formatDateTime } from '../../lib'
import { GitPathBucket } from './GitPathBucket'

type CommitPreparationSummaryProps = {
  preparation: CommitPreparation
  selectedPathCount: number
}

export function CommitPreparationSummary({
  preparation,
  selectedPathCount,
}: CommitPreparationSummaryProps) {
  return (
    <div className="context-summary">
      <span>Preparation: {preparation.id}</span>
      <span>Snapshot: {preparation.statusSnapshot.id}</span>
      <span>Branch: {preparation.statusSnapshot.branch || '(detached)'}</span>
      <span>Generated: {formatDateTime(preparation.generatedAt)}</span>
      <span>Changed paths: {preparation.scopeItems.length}</span>
      <span>Selected: {selectedPathCount}</span>
      <span>Pre-existing: {preparation.hasPreExistingChanges ? 'Present' : 'None detected'}</span>
      <span>Captured: {formatDateTime(preparation.statusSnapshot.capturedAt)}</span>
    </div>
  )
}

type PushReviewSummaryProps = {
  execution: ExecutionSessionSummary
  gitStatus: RepositoryGitStatus | null
}

export function PushReviewSummary({ execution, gitStatus }: PushReviewSummaryProps) {
  return (
    <div className="context-summary">
      <span>Commit: {execution.commitSha}</span>
      <span>Committed: {formatDateTime(execution.committedAt)}</span>
      <span>Snapshot: {execution.preparationSnapshotId ?? 'Not recorded'}</span>
      <span>Branch: {gitStatus?.branch || execution.pushBranchName || '(unknown)'}</span>
      <span>Ahead: {gitStatus?.aheadCount ?? 'Not loaded'}</span>
      <span>State: Awaiting push</span>
    </div>
  )
}

type GitStatusDetailsProps = {
  gitStatus: RepositoryGitStatus
  changedPathCount: number
}

export function GitStatusDetails({ gitStatus, changedPathCount }: GitStatusDetailsProps) {
  return (
    <>
      <div className="context-summary">
        <span>Branch: {gitStatus.branch || '(detached)'}</span>
        <span>State: {gitStatus.dirtyState.isClean ? 'Clean' : 'Dirty'}</span>
        <span>Ahead: {gitStatus.aheadCount}</span>
        <span>Behind: {gitStatus.behindCount}</span>
        <span>Changed paths: {changedPathCount}</span>
        <span>Captured: {formatDateTime(gitStatus.capturedAt)}</span>
      </div>
      <div className="context-columns">
        <GitPathBucket label="Staged" paths={gitStatus.dirtyState.stagedPaths} />
        <GitPathBucket label="Modified" paths={gitStatus.dirtyState.modifiedPaths} />
        <GitPathBucket label="Added" paths={gitStatus.dirtyState.addedPaths} />
        <GitPathBucket label="Deleted" paths={gitStatus.dirtyState.deletedPaths} />
        <GitPathBucket label="Renamed" paths={gitStatus.dirtyState.renamedPaths} />
        <GitPathBucket label="Untracked" paths={gitStatus.dirtyState.untrackedPaths} />
      </div>
    </>
  )
}
