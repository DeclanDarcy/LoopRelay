import { formatDateTime } from '../../lib'
import type {
  ExecutionReadiness,
  RepositoryAvailability,
  RepositoryDashboardProjection,
  RepositoryExecutionState,
} from '../../types'

type RepositoryDashboardItemContentProps = {
  repository: RepositoryDashboardProjection
  availabilityLabels: Record<RepositoryAvailability, string>
  readinessLabels: Record<ExecutionReadiness, string>
  executionStateLabels: Record<RepositoryExecutionState, string>
}

export function RepositoryDashboardItemContent({
  repository,
  availabilityLabels,
  readinessLabels,
  executionStateLabels,
}: RepositoryDashboardItemContentProps) {
  return (
    <>
      <span className="repository-name">{repository.repository.name}</span>
      <span className="repository-path">{repository.repository.path}</span>
      <span className={`availability availability-${repository.availability.toLowerCase()}`}>
        {availabilityLabels[repository.availability]}
      </span>
      <span className={`readiness readiness-${repository.readiness.toLowerCase()}`}>
        {readinessLabels[repository.readiness]}
      </span>
      <span className={`execution-state execution-state-${repository.executionState.toLowerCase()}`}>
        {executionStateLabels[repository.executionState]}
      </span>
      <span className="repository-metadata">{repository.milestoneCount} milestones</span>
      {repository.executionSummary ? (
        <>
          <span className="repository-metadata">Session {repository.executionSummary.sessionId}</span>
          <span className="repository-metadata">State {repository.executionSummary.state}</span>
          {repository.executionSummary.lastActivityAt ? (
            <span className="repository-metadata">
              Activity {formatDateTime(repository.executionSummary.lastActivityAt)}
            </span>
          ) : null}
          {repository.executionSummary.failureReason ? (
            <span className="repository-metadata failure-metadata">
              Failure {repository.executionSummary.failureReason}
            </span>
          ) : null}
        </>
      ) : null}
      <span className="repository-metadata">
        Handoff {repository.hasCurrentHandoff ? 'present' : 'missing'}
      </span>
      <span className="repository-metadata">
        Decisions {repository.hasCurrentDecisions ? 'present' : 'missing'}
      </span>
      <span className="repository-metadata">
        Context {repository.continuitySummary.operationalContextExists ? 'present' : 'missing'}
      </span>
      <span className="repository-metadata">
        Updated {formatDateTime(repository.continuitySummary.operationalContextLastUpdatedAt)}
      </span>
      <span className="repository-metadata">
        Revisions {repository.continuitySummary.operationalContextRevisionCount}
      </span>
      <span className="repository-metadata">
        Questions {repository.continuitySummary.openQuestionCount}
      </span>
      <span className="repository-metadata">
        Risks {repository.continuitySummary.activeRiskCount}
      </span>
    </>
  )
}
