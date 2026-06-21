import { formatDateTime } from '../../lib'
import { StatusBadge } from '../../components/design'
import {
  executionReadinessStatus,
  repositoryAvailabilityStatus,
  repositoryExecutionStatus,
} from '../../lib/status'
import type { RepositoryDashboardProjection } from '../../types'

type RepositoryDashboardItemContentProps = {
  repository: RepositoryDashboardProjection
}

export function RepositoryDashboardItemContent({ repository }: RepositoryDashboardItemContentProps) {
  return (
    <>
      <span className="repository-name">{repository.repository.name}</span>
      <span className="repository-path">{repository.repository.path}</span>
      <StatusBadge status={repositoryAvailabilityStatus[repository.availability]} />
      <StatusBadge status={executionReadinessStatus[repository.readiness]} />
      <StatusBadge status={repositoryExecutionStatus[repository.executionState]} />
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
