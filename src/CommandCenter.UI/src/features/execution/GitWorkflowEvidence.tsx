import type {
  CommitChangeType,
  CommitPreparation,
  CommitScopeItem,
  ExecutionGitActionEligibility,
  ExplanationDiagnostic,
  ExplanationEvidence,
  RepositoryGitStatus,
  ExecutionSessionSummary,
} from '../../types'
import { formatDateTime } from '../../lib'
import { ActionEligibilityView, DiagnosticList, InteractionPatternView } from '../../components/explainability'
import {
  executionGitEligibilityToActions,
  executionGitEligibilityToDiagnostics,
} from '../../lib/explainability'
import { GitPathBucket } from './GitPathBucket'

type CommitPreparationSummaryProps = {
  preparation: CommitPreparation
  selectedPathCount: number
}

export function CommitPreparationSummary({
  preparation,
  selectedPathCount,
}: CommitPreparationSummaryProps) {
  const executionGeneratedCount = preparation.scopeItems.filter(
    (item) => item.origin === 'ExecutionGenerated',
  ).length
  const preExistingCount = preparation.scopeItems.filter(
    (item) => item.origin === 'PreExisting',
  ).length

  return (
    <div className="context-summary">
      <span>Preparation: {preparation.id}</span>
      <span>Snapshot: {preparation.statusSnapshot.id}</span>
      <span>Branch: {preparation.statusSnapshot.branch || '(detached)'}</span>
      <span>Generated: {formatDateTime(preparation.generatedAt)}</span>
      <span>Changed paths: {preparation.scopeItems.length}</span>
      <span>Selected: {selectedPathCount}</span>
      <span>Execution-generated: {executionGeneratedCount}</span>
      <span>Pre-existing paths: {preExistingCount}</span>
      <span>Pre-existing: {preparation.hasPreExistingChanges ? 'Present' : 'None detected'}</span>
      <span>Captured: {formatDateTime(preparation.statusSnapshot.capturedAt)}</span>
    </div>
  )
}

const changeTypeBuckets: CommitChangeType[] = [
  'Staged',
  'Modified',
  'Added',
  'Deleted',
  'Renamed',
  'Untracked',
]

type CommitPreparationChangeBucketsProps = {
  preparation: CommitPreparation
}

export function CommitPreparationChangeBuckets({ preparation }: CommitPreparationChangeBucketsProps) {
  return (
    <div className="context-columns" aria-label="Classified commit paths">
      {changeTypeBuckets.map((changeType) => (
        <GitPathBucket
          key={changeType}
          label={changeType}
          items={preparation.scopeItems
            .filter((item) => item.changeType === changeType)
            .map(toBucketItem)}
        />
      ))}
    </div>
  )
}

function toBucketItem(item: CommitScopeItem) {
  return {
    path: item.path,
    origin: item.origin,
    originBasis: item.originBasis,
  }
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

type ExecutionGitInteractionSummaryProps = {
  commitMessage?: string
  eligibility: ExecutionGitActionEligibility | null
  error?: string | null
  execution: ExecutionSessionSummary | null
  gitStatus: RepositoryGitStatus | null
  isLoading?: boolean
  mode: 'commit' | 'push'
  preparation?: CommitPreparation | null
  selectedPathCount?: number
}

export function ExecutionGitInteractionSummary({
  commitMessage = '',
  eligibility,
  error = null,
  execution,
  gitStatus,
  isLoading = false,
  mode,
  preparation = null,
  selectedPathCount = 0,
}: ExecutionGitInteractionSummaryProps) {
  const actions = eligibility
    ? executionGitEligibilityToActions(eligibility).filter((action) =>
        mode === 'commit' ? action.command === 'executionCommit' : action.command === 'executionPush',
      )
    : []
  const diagnostics = eligibility ? executionGitEligibilityToDiagnostics(eligibility) : []
  const interactionDiagnostics = [
    ...diagnostics,
    ...gitInteractionDiagnostics(eligibility, error, isLoading, mode),
  ]

  return (
    <InteractionPatternView
      actions={actions}
      diagnostics={interactionDiagnostics}
      evidence={gitInteractionEvidence({
        commitMessage,
        eligibility,
        execution,
        gitStatus,
        mode,
        preparation,
        selectedPathCount,
      })}
      result={gitInteractionResult(eligibility, execution, mode)}
      subject={mode === 'commit' ? 'Execution commit' : 'Execution push'}
      title={mode === 'commit' ? 'Commit Interaction Summary' : 'Push Interaction Summary'}
    />
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

  const canRun = mode === 'commit' ? eligibility.canCommit : eligibility.canPush
  const actions = executionGitEligibilityToActions(eligibility).filter((action) =>
    mode === 'commit' ? action.command === 'executionCommit' : action.command === 'executionPush',
  )
  const diagnostics = executionGitEligibilityToDiagnostics(eligibility)

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
      <ActionEligibilityView
        actions={actions}
        title={mode === 'commit' ? 'Commit Eligibility' : 'Push Eligibility'}
      />
      <DiagnosticList
        diagnostics={diagnostics}
        title="Git Eligibility Diagnostics"
        emptyLabel="No git eligibility diagnostics recorded."
      />
    </div>
  )
}

type GitInteractionEvidenceArgs = {
  commitMessage: string
  eligibility: ExecutionGitActionEligibility | null
  execution: ExecutionSessionSummary | null
  gitStatus: RepositoryGitStatus | null
  mode: 'commit' | 'push'
  preparation: CommitPreparation | null
  selectedPathCount: number
}

function gitInteractionEvidence({
  commitMessage,
  eligibility,
  execution,
  gitStatus,
  mode,
  preparation,
  selectedPathCount,
}: GitInteractionEvidenceArgs): ExplanationEvidence[] {
  const evidence: ExplanationEvidence[] = []

  if (execution) {
    evidence.push({
      id: `${execution.sessionId}-${mode}-session`,
      label: 'Execution session',
      detail: `${execution.state} | ${execution.repositoryState}`,
      source: execution.sessionId,
    })
  }

  if (preparation) {
    evidence.push(
      {
        id: `${preparation.id}-preparation`,
        label: 'Commit preparation',
        detail: `${preparation.scopeItems.length} prepared paths | ${selectedPathCount} selected`,
        source: preparation.repositoryPath,
      },
      {
        id: `${preparation.id}-snapshot`,
        label: 'Prepared snapshot',
        detail: `${preparation.statusSnapshot.id} | ${preparation.statusSnapshot.branch || '(detached)'}`,
      },
    )
  }

  if (mode === 'commit') {
    evidence.push({
      label: 'Commit message',
      detail: commitMessage.trim() || 'Commit message is empty.',
    })
  }

  if (eligibility) {
    evidence.push(
      {
        id: `${eligibility.sessionId}-${mode}-eligibility`,
        label: 'Backend git eligibility',
        detail: mode === 'commit'
          ? eligibility.canCommit ? 'Commit allowed' : 'Commit blocked'
          : eligibility.canPush ? 'Push allowed' : 'Push blocked',
      },
      {
        label: 'Selected scope',
        detail: `${eligibility.selectedPathCount} selected | ${eligibility.preparedPathCount} prepared`,
      },
      {
        label: 'Commit SHA',
        detail: eligibility.commitSha ?? 'No commit SHA recorded.',
        fingerprint: eligibility.commitSha ?? undefined,
      },
    )
  }

  if (gitStatus) {
    evidence.push({
      label: 'Current git status',
      detail: `${gitStatus.branch || '(detached)'} | ahead ${gitStatus.aheadCount} | behind ${gitStatus.behindCount}`,
    })
  }

  if (eligibility?.remoteBranchState) {
    evidence.push({
      label: 'Remote branch state',
      detail: `${eligibility.remoteBranchState.branch || '(unknown)'} | ahead ${eligibility.remoteBranchState.aheadCount} | behind ${eligibility.remoteBranchState.behindCount}`,
    })
  }

  if (execution?.pushAttemptedAt || eligibility?.previousPushAttemptedAt) {
    evidence.push({
      label: 'Push attempt',
      detail: formatDateTime(execution?.pushAttemptedAt ?? eligibility?.previousPushAttemptedAt ?? null),
    })
  }

  if (execution?.failureReason || eligibility?.previousPushFailure) {
    evidence.push({
      label: 'Push failure context',
      detail: execution?.failureReason ?? eligibility?.previousPushFailure ?? 'No push failure recorded.',
    })
  }

  return evidence
}

function gitInteractionDiagnostics(
  eligibility: ExecutionGitActionEligibility | null,
  error: string | null,
  isLoading: boolean,
  mode: 'commit' | 'push',
): ExplanationDiagnostic[] {
  if (eligibility) {
    return []
  }

  if (error) {
    return [
      {
        label: 'Git eligibility',
        detail: error,
        tone: 'warning',
      },
    ]
  }

  return [
    {
      label: 'Git eligibility',
      detail: isLoading
        ? `${mode === 'commit' ? 'Commit' : 'Push'} eligibility is loading.`
        : `${mode === 'commit' ? 'Commit' : 'Push'} eligibility has not been loaded.`,
      tone: 'info',
    },
  ]
}

function gitInteractionResult(
  eligibility: ExecutionGitActionEligibility | null,
  execution: ExecutionSessionSummary | null,
  mode: 'commit' | 'push',
) {
  if (mode === 'commit') {
    if (execution?.commitSha) {
      return `Committed ${execution.commitSha}.`
    }

    return eligibility?.canCommit
      ? 'Commit can run for the prepared selected scope.'
      : 'Commit has not been recorded.'
  }

  if (execution?.pushedAt) {
    return `Pushed ${execution.pushedCommitSha ?? execution.commitSha ?? 'recorded commit'}.`
  }

  const failure = execution?.failureReason ?? eligibility?.previousPushFailure
  if (failure) {
    return `Previous push failed: ${failure}`
  }

  return eligibility?.canPush ? 'Push can run for the recorded commit.' : 'Push has not been recorded.'
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
