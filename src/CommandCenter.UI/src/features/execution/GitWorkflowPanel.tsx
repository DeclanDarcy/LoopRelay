import { EmptyState, Panel, SectionHeader } from '../../components/design'
import { repositoryExecutionStatus } from '../../lib/status'
import type {
  CommitPreparation,
  CommitScopeItem,
  ExecutionSessionSummary,
  RepositoryExecutionState,
  RepositoryGitStatus,
} from '../../types'
import {
  CommitPreparationSummary,
  GitStatusDetails,
  PushReviewSummary,
} from './GitWorkflowEvidence'

type GitWorkflowPanelProps = {
  shouldShow: boolean
  currentExecutionState: RepositoryExecutionState
  execution: ExecutionSessionSummary | null
  gitStatus: RepositoryGitStatus | null
  gitStatusPathCount: number
  commitPreparation: CommitPreparation | null
  isCommitPreparationCurrent: boolean
  selectedCommitScopeItems: CommitScopeItem[]
  selectedCommitPaths: Set<string>
  commitMessage: string
  canCommitPreparedScope: boolean
  canPushExecution: boolean
  hasRefreshTarget: boolean
  isGitStatusLoading: boolean
  isCommitPreparationLoading: boolean
  isCommitting: boolean
  isPushing: boolean
  onRefresh: () => void
  onCommitMessageChange: (message: string) => void
  onSelectAllCommitPaths: () => void
  onSelectNoCommitPaths: () => void
  onSetCommitPathSelection: (path: string, isSelected: boolean) => void
  onCommitPreparedScope: () => void
  onPushExecution: () => void
}

export function GitWorkflowPanel({
  shouldShow,
  currentExecutionState,
  execution,
  gitStatus,
  gitStatusPathCount,
  commitPreparation,
  isCommitPreparationCurrent,
  selectedCommitScopeItems,
  selectedCommitPaths,
  commitMessage,
  canCommitPreparedScope,
  canPushExecution,
  hasRefreshTarget,
  isGitStatusLoading,
  isCommitPreparationLoading,
  isCommitting,
  isPushing,
  onRefresh,
  onCommitMessageChange,
  onSelectAllCommitPaths,
  onSelectNoCommitPaths,
  onSetCommitPathSelection,
  onCommitPreparedScope,
  onPushExecution,
}: GitWorkflowPanelProps) {
  if (!shouldShow) {
    return null
  }

  return (
    <Panel id="git-workflow" className="git-status-panel" aria-label="Git status">
      <SectionHeader
        className="git-status-header"
        eyebrow="Git Workflow"
        title={repositoryExecutionStatus[currentExecutionState].label}
        headingLevel={4}
        actions={
          <button
            type="button"
            className="secondary-action"
            onClick={onRefresh}
            disabled={!hasRefreshTarget || isGitStatusLoading || isCommitPreparationLoading}
          >
            {isGitStatusLoading || isCommitPreparationLoading ? 'Refreshing...' : 'Refresh'}
          </button>
        }
      />
      {currentExecutionState === 'AwaitingCommit' ? (
        isCommitPreparationCurrent && commitPreparation ? (
          <div className="commit-review-panel">
            <CommitPreparationSummary
              preparation={commitPreparation}
              selectedPathCount={selectedCommitScopeItems.length}
            />
            <label className="commit-message-editor">
              <span>Commit message</span>
              <textarea
                value={commitMessage}
                onChange={(event) => onCommitMessageChange(event.target.value)}
                spellCheck={false}
              />
            </label>
            <div className="commit-scope-toolbar">
              <button
                type="button"
                className="secondary-action"
                onClick={onSelectAllCommitPaths}
                disabled={commitPreparation.scopeItems.length === 0}
              >
                Select All
              </button>
              <button
                type="button"
                className="secondary-action"
                onClick={onSelectNoCommitPaths}
                disabled={commitPreparation.scopeItems.length === 0}
              >
                Select None
              </button>
              <button
                type="button"
                className="primary-action"
                onClick={onCommitPreparedScope}
                disabled={!canCommitPreparedScope}
              >
                {isCommitting ? 'Committing...' : 'Commit Selected'}
              </button>
            </div>
            {commitPreparation.scopeItems.length === 0 ? (
              <EmptyState className="empty-state">No changed paths are available for commit.</EmptyState>
            ) : (
              <div className="commit-scope-list" aria-label="Commit scope">
                {commitPreparation.scopeItems.map((item) => (
                  <label className="commit-scope-item" key={item.path}>
                    <input
                      type="checkbox"
                      checked={selectedCommitPaths.has(item.path)}
                      onChange={(event) =>
                        onSetCommitPathSelection(item.path, event.currentTarget.checked)
                      }
                    />
                    <span>{item.path}</span>
                    <small>{item.changeType}</small>
                    <small>{item.origin === 'PreExisting' ? 'Pre-existing' : 'Execution generated'}</small>
                  </label>
                ))}
              </div>
            )}
          </div>
        ) : (
          <EmptyState className="empty-state">
            {isCommitPreparationLoading
              ? 'Preparing commit review...'
              : 'Commit preparation is not loaded.'}
          </EmptyState>
        )
      ) : currentExecutionState === 'AwaitingPush' && execution?.commitSha ? (
        <div className="commit-review-panel">
          <PushReviewSummary execution={execution} gitStatus={gitStatus} />
          <div className="commit-scope-toolbar">
            <button
              type="button"
              className="primary-action"
              onClick={onPushExecution}
              disabled={!canPushExecution}
            >
              {isPushing ? 'Pushing...' : 'Push Commit'}
            </button>
          </div>
        </div>
      ) : gitStatus ? (
        <GitStatusDetails gitStatus={gitStatus} changedPathCount={gitStatusPathCount} />
      ) : (
        <EmptyState className="empty-state">
          {isGitStatusLoading ? 'Loading Git status...' : 'Git status is not loaded.'}
        </EmptyState>
      )}
    </Panel>
  )
}
