import type {
  CommitPreparation,
  ExecutionGitActionEligibility,
  RepositoryGitStatus,
  ExecutionSessionSummary,
} from '../../types'
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
      <span>Last push attempt: {formatDateTime(execution.pushAttemptedAt)}</span>
      <span>Previous push failure: {execution.failureReason ?? 'None recorded'}</span>
    </div>
  )
}

type GitEligibilitySummaryProps = {
  eligibility: ExecutionGitActionEligibility | null
  mode: 'commit' | 'push'
  isLoading?: boolean
  error?: string | null
}

export function GitEligibilitySummary({
  eligibility,
  mode,
  isLoading = false,
  error = null,
}: GitEligibilitySummaryProps) {
  if (error && !eligibility) {
    return <div className="execution-rail-warning">Git eligibility: {error}</div>
  }

  if (!eligibility) {
    return (
      <div className="context-summary" aria-label="Git eligibility">
        <span>{isLoading ? 'Eligibility: Loading' : 'Eligibility: Not loaded'}</span>
      </div>
    )
  }

  const disabledReasons =
    mode === 'commit' ? eligibility.commitDisabledReasons : eligibility.pushDisabledReasons
  const canRun = mode === 'commit' ? eligibility.canCommit : eligibility.canPush

  return (
    <div className="git-eligibility" aria-label="Git eligibility">
      <div className="context-summary">
        <span>Eligibility: {canRun ? 'Allowed' : 'Blocked'}</span>
        <span>Preparation loaded: {eligibility.commitPreparationLoaded ? 'Yes' : 'No'}</span>
        <span>Preparation current: {eligibility.commitPreparationCurrent ? 'Yes' : 'No'}</span>
        <span>Selected paths: {eligibility.selectedPathCount}</span>
        <span>Commit message: {eligibility.commitMessagePresent ? 'Present' : 'Missing'}</span>
        <span>Awaiting push: {eligibility.awaitingPush ? 'Yes' : 'No'}</span>
        <span>Commit SHA: {eligibility.commitShaExists ? eligibility.commitSha : 'Missing'}</span>
        <span>Previous push failure: {eligibility.previousPushFailure ?? 'None recorded'}</span>
        <span>Previous push attempt: {formatDateTime(eligibility.previousPushAttemptedAt)}</span>
        <span>Remote branch: {eligibility.remoteBranchState?.branch || 'Not loaded'}</span>
        <span>Remote ahead: {eligibility.remoteBranchState?.aheadCount ?? 'Not loaded'}</span>
        <span>Remote behind: {eligibility.remoteBranchState?.behindCount ?? 'Not loaded'}</span>
      </div>
      {disabledReasons.length > 0 ? (
        <ul className="diagnostic-list" aria-label={`${mode} disabled reasons`}>
          {disabledReasons.map((reason) => (
            <li key={reason}>{reason}</li>
          ))}
        </ul>
      ) : null}
      {eligibility.unknownSelectedPaths.length > 0 ? (
        <ul className="diagnostic-list" aria-label="Unknown selected paths">
          {eligibility.unknownSelectedPaths.map((path) => (
            <li key={path}>{path}</li>
          ))}
        </ul>
      ) : null}
      {eligibility.diagnostics.length > 0 ? (
        <ul className="diagnostic-list" aria-label="Git eligibility diagnostics">
          {eligibility.diagnostics.map((diagnostic) => (
            <li key={diagnostic}>{diagnostic}</li>
          ))}
        </ul>
      ) : null}
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
