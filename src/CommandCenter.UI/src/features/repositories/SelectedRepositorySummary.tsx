import { formatDateTime, formatDuration } from '../../lib'
import type {
  ExecutionReadiness,
  ExecutionSessionSummary,
  RepositoryAvailability,
  RepositoryDashboardProjection,
  RepositoryExecutionState,
  RepositoryWorkspaceProjection,
} from '../../types'

type SelectedRepositorySummaryProps = {
  repository: RepositoryDashboardProjection
  workspace: RepositoryWorkspaceProjection | null
  executionDisplay: ExecutionSessionSummary | null
  currentExecutionState: RepositoryExecutionState
  availabilityLabels: Record<RepositoryAvailability, string>
  readinessLabels: Record<ExecutionReadiness, string>
  executionStateLabels: Record<RepositoryExecutionState, string>
}

export function SelectedRepositorySummary({
  repository,
  workspace,
  executionDisplay,
  currentExecutionState,
  availabilityLabels,
  readinessLabels,
  executionStateLabels,
}: SelectedRepositorySummaryProps) {
  return (
    <>
      <div className="details-title-row">
        <div>
          <p className="eyebrow">Selected repository</p>
          <h3>{repository.repository.name}</h3>
        </div>
        <span className={`availability availability-${repository.availability.toLowerCase()}`}>
          {availabilityLabels[repository.availability]}
        </span>
      </div>

      <dl className="details-list">
        <div>
          <dt>Path</dt>
          <dd>{repository.repository.path}</dd>
        </div>
        <div>
          <dt>Readiness</dt>
          <dd>{readinessLabels[workspace?.readiness ?? repository.readiness]}</dd>
        </div>
        <div>
          <dt>Execution</dt>
          <dd>{executionStateLabels[currentExecutionState]}</dd>
        </div>
        {executionDisplay ? (
          <>
            <div>
              <dt>Session</dt>
              <dd>{executionDisplay.sessionId}</dd>
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
              <dd>{executionDisplay.handoffPath || 'Not recorded'}</dd>
            </div>
          </>
        ) : null}
        <div>
          <dt>Milestones</dt>
          <dd>{workspace?.milestoneCount ?? repository.milestoneCount}</dd>
        </div>
      </dl>

      <div className="summary-grid">
        <span>Plan: {workspace?.hasPlan ? 'Present' : 'Missing'}</span>
        <span>
          Operational context: {workspace?.hasOperationalContext ? 'Present' : 'Missing'}
        </span>
        <span>Handoff: {workspace?.hasCurrentHandoff ? 'Present' : 'Missing'}</span>
        <span>Decisions: {workspace?.hasCurrentDecisions ? 'Present' : 'Missing'}</span>
      </div>
    </>
  )
}
