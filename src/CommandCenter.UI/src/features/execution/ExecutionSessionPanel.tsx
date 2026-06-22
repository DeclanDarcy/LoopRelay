import { formatDateTime, formatDuration } from '../../lib'
import { Button, Panel, SectionHeader, StatusBadge } from '../../components/design'
import { executionSessionStatus, repositoryExecutionStatus } from '../../lib/status'
import type { ExecutionSessionSummary } from '../../types'

type ExecutionSessionPanelProps = {
  session: ExecutionSessionSummary
  onOpenMilestone?: () => void
  onOpenHandoff?: () => void
}

export function ExecutionSessionPanel({
  session,
  onOpenMilestone,
  onOpenHandoff,
}: ExecutionSessionPanelProps) {
  return (
    <Panel className="execution-session-panel" aria-label="Execution session">
      <SectionHeader
        eyebrow={session.repositoryState === 'Executing' ? 'Active Execution' : 'Execution Session'}
        title={session.milestonePath ?? 'Selected milestone'}
        headingLevel={4}
        actions={
          <div className="execution-session-actions">
            {onOpenMilestone ? (
              <Button
                type="button"
                variant="secondary"
                className="secondary-action"
                onClick={onOpenMilestone}
              >
                Milestone
              </Button>
            ) : null}
            {onOpenHandoff ? (
              <Button
                type="button"
                variant="secondary"
                className="secondary-action"
                onClick={onOpenHandoff}
              >
                Handoff
              </Button>
            ) : null}
          </div>
        }
      />
      <div className="execution-session-grid">
        <span>Session: {session.sessionId}</span>
        <span>Provider: {session.providerName || 'Unknown'}</span>
        <span>
          State: <StatusBadge status={executionSessionStatus[session.state]} />
        </span>
        <span>
          Repository state: <StatusBadge status={repositoryExecutionStatus[session.repositoryState]} />
        </span>
        <span>Started: {formatDateTime(session.startedAt)}</span>
        <span>Completed: {formatDateTime(session.completedAt)}</span>
        <span>Duration: {formatDuration(session.duration)}</span>
        <span>Accepted: {formatDateTime(session.acceptedAt)}</span>
        <span>Rejected: {formatDateTime(session.rejectedAt)}</span>
        <span>Last activity: {formatDateTime(session.lastActivityAt)}</span>
        <span>Provider start: {formatDateTime(session.providerStartedAt)}</span>
        <span>PID: {session.providerProcessId ?? 'Not recorded'}</span>
        <span>Executable: {session.providerExecutablePath || 'Not recorded'}</span>
        <span>Handoff: {session.handoffPath || 'Not recorded'}</span>
        <span>Commit: {session.commitSha || 'Not recorded'}</span>
        <span>Committed: {formatDateTime(session.committedAt)}</span>
        <span>Pushed: {formatDateTime(session.pushedAt)}</span>
        <span>Pushed commit: {session.pushedCommitSha || 'Not recorded'}</span>
        {session.failureReason ? <span className="execution-failure">Failure: {session.failureReason}</span> : null}
      </div>
    </Panel>
  )
}
