import { formatDateTime, formatDuration } from '../../lib'
import { StatusBadge } from '../../components/design'
import {
  executionReadinessStatus,
  repositoryAvailabilityStatus,
  repositoryExecutionStatus,
} from '../../lib/status'
import type {
  ExecutionSessionSummary,
  RepositoryDashboardProjection,
  RepositoryExecutionState,
  RepositoryWorkspaceProjection,
  WorkflowInstance,
} from '../../types'

type SelectedRepositorySummaryProps = {
  repository: RepositoryDashboardProjection
  workspace: RepositoryWorkspaceProjection | null
  workflow: WorkflowInstance | null
  executionDisplay: ExecutionSessionSummary | null
  currentExecutionState: RepositoryExecutionState
  onOpenExecution?: () => void
  onOpenMilestones?: () => void
  onOpenOperationalContext?: () => void
  onOpenHandoffArtifact?: (handoffPath: string) => void
}

export function SelectedRepositorySummary({
  repository,
  workspace,
  workflow,
  executionDisplay,
  currentExecutionState,
  onOpenExecution,
  onOpenMilestones,
  onOpenOperationalContext,
  onOpenHandoffArtifact,
}: SelectedRepositorySummaryProps) {
  const readiness = workspace?.readiness ?? repository.readiness
  const milestoneCount = workspace?.milestoneCount ?? repository.milestoneCount
  const hasOperationalContext = Boolean(workspace?.hasOperationalContext)

  return (
    <>
      <div className="details-title-row">
        <div>
          <p className="eyebrow">Selected repository</p>
          <h3>{repository.repository.name}</h3>
        </div>
        <StatusBadge status={repositoryAvailabilityStatus[repository.availability]} />
      </div>

      <dl className="details-list">
        <div>
          <dt>Path</dt>
          <dd>{repository.repository.path}</dd>
        </div>
        <div>
          <dt>Readiness</dt>
          <dd>
            <StatusBadge status={executionReadinessStatus[readiness]} />
          </dd>
        </div>
        <div>
          <dt>Execution</dt>
          <dd>
            {onOpenExecution ? (
              <button
                type="button"
                className="workspace-cross-link inline-cross-link"
                onClick={onOpenExecution}
              >
                <StatusBadge status={repositoryExecutionStatus[currentExecutionState]} />
              </button>
            ) : (
              <StatusBadge status={repositoryExecutionStatus[currentExecutionState]} />
            )}
          </dd>
        </div>
        {executionDisplay ? (
          <>
            <div>
              <dt>Session</dt>
              <dd>
                {onOpenExecution ? (
                  <button
                    type="button"
                    className="workspace-cross-link inline-cross-link"
                    onClick={onOpenExecution}
                  >
                    {executionDisplay.sessionId}
                  </button>
                ) : (
                  executionDisplay.sessionId
                )}
              </dd>
            </div>
            <div>
              <dt>Provider</dt>
              <dd>{executionDisplay.providerName || 'Unknown'}</dd>
            </div>
            <div>
              <dt>Started</dt>
              <dd>{formatDateTime(executionDisplay.startedAt)}</dd>
            </div>
            <div>
              <dt>Last activity</dt>
              <dd>{formatDateTime(executionDisplay.lastActivityAt)}</dd>
            </div>
            <div>
              <dt>Duration</dt>
              <dd>{formatDuration(executionDisplay.duration)}</dd>
            </div>
            <div>
              <dt>Accepted</dt>
              <dd>{formatDateTime(executionDisplay.acceptedAt)}</dd>
            </div>
            <div>
              <dt>Rejected</dt>
              <dd>{formatDateTime(executionDisplay.rejectedAt)}</dd>
            </div>
            <div>
              <dt>Decision note</dt>
              <dd>{executionDisplay.decisionNote || 'Not recorded'}</dd>
            </div>
            <div>
              <dt>PID</dt>
              <dd>{executionDisplay.providerProcessId ?? 'Not recorded'}</dd>
            </div>
            <div>
              <dt>Executable</dt>
              <dd>{executionDisplay.providerExecutablePath || 'Not recorded'}</dd>
            </div>
            <div>
              <dt>Failure</dt>
              <dd>{executionDisplay.failureReason || 'None'}</dd>
            </div>
            <div>
              <dt>Handoff</dt>
              <dd>
                {executionDisplay.handoffPath && onOpenHandoffArtifact ? (
                  <button
                    type="button"
                    className="workspace-cross-link inline-cross-link"
                    onClick={() => onOpenHandoffArtifact(executionDisplay.handoffPath as string)}
                  >
                    {executionDisplay.handoffPath}
                  </button>
                ) : (
                  executionDisplay.handoffPath || 'Not recorded'
                )}
              </dd>
            </div>
          </>
        ) : null}
        <div>
          <dt>Milestones</dt>
          <dd>
            {milestoneCount > 0 && onOpenMilestones ? (
              <button
                type="button"
                className="workspace-cross-link inline-cross-link"
                onClick={onOpenMilestones}
              >
                {milestoneCount}
              </button>
            ) : (
              milestoneCount
            )}
          </dd>
        </div>
      </dl>

      <div className="summary-grid">
        <span>Plan: {workspace?.hasPlan ? 'Present' : 'Missing'}</span>
        <span>
          Operational context:{' '}
          {hasOperationalContext && onOpenOperationalContext ? (
            <button
              type="button"
              className="workspace-cross-link inline-cross-link"
              onClick={onOpenOperationalContext}
            >
              Present
            </button>
          ) : hasOperationalContext ? (
            'Present'
          ) : (
            'Missing'
          )}
        </span>
        <span>Handoff: {workspace?.hasCurrentHandoff ? 'Present' : 'Missing'}</span>
        <span>Decisions: {workspace?.hasCurrentDecisions ? 'Present' : 'Missing'}</span>
        <span>Workflow stage: {workflow?.currentStage ?? 'Not loaded'}</span>
        <span>Workflow gate: {workflow?.blockingGate ?? 'Not loaded'}</span>
        <span>
          Required action:{' '}
          {workflow?.requiredHumanAction && workflow.requiredHumanAction.trim().length > 0
            ? workflow.requiredHumanAction
            : workflow
              ? 'None'
              : 'Not loaded'}
        </span>
        <span>Timeline events: {workflow ? workflow.timeline.length : 'Not loaded'}</span>
      </div>
    </>
  )
}
