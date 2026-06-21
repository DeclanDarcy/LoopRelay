import { formatDateTime, formatDuration } from '../../lib'
import type { ExecutionSessionSummary } from '../../types'

type ExecutionSessionPanelProps = {
  session: ExecutionSessionSummary
  repositoryStateLabel: string
}

export function ExecutionSessionPanel({ session, repositoryStateLabel }: ExecutionSessionPanelProps) {
  return (
    <section className="execution-session-panel" aria-label="Execution session">
      <div>
        <p className="eyebrow">{session.repositoryState === 'Executing' ? 'Active Execution' : 'Execution Session'}</p>
        <h4>{session.milestonePath ?? 'Selected milestone'}</h4>
      </div>
      <div className="execution-session-grid">
        <span>Session: {session.sessionId}</span>
        <span>Provider: {session.providerName || 'Unknown'}</span>
        <span>State: {session.state}</span>
        <span>Repository state: {repositoryStateLabel}</span>
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
    </section>
  )
}
