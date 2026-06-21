import { formatDateTime, formatDuration } from '../../lib'
import type { ExecutionSessionSummary, RepositoryExecutionState } from '../../types'

type ExecutionHistoryPanelProps = {
  sessions: ExecutionSessionSummary[]
  repositoryStateLabels: Record<RepositoryExecutionState, string>
}

export function ExecutionHistoryPanel({ sessions, repositoryStateLabels }: ExecutionHistoryPanelProps) {
  if (sessions.length === 0) {
    return null
  }

  return (
    <section className="execution-history-panel" aria-label="Execution history">
      <div>
        <p className="eyebrow">Session History</p>
        <h4>{sessions.length} recent sessions</h4>
      </div>
      <div className="execution-history-list">
        {sessions.map((session) => (
          <div className="execution-history-row" key={session.sessionId}>
            <span>{session.milestonePath ?? 'Milestone not recorded'}</span>
            <small>{repositoryStateLabels[session.repositoryState]}</small>
            <small>Started {formatDateTime(session.startedAt)}</small>
            <small>Duration {formatDuration(session.duration)}</small>
            <small>Commit {session.commitSha ?? 'Not recorded'}</small>
            <small>Push {session.pushedAt ? formatDateTime(session.pushedAt) : 'Not recorded'}</small>
          </div>
        ))}
      </div>
    </section>
  )
}
