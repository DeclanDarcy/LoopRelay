import { formatDateTime, formatDuration } from '../../lib'
import { Panel, SectionHeader, StatusBadge } from '../../components/design'
import { repositoryExecutionStatus } from '../../lib/status'
import type { ExecutionSessionSummary } from '../../types'

type ExecutionHistoryPanelProps = {
  sessions: ExecutionSessionSummary[]
}

export function ExecutionHistoryPanel({ sessions }: ExecutionHistoryPanelProps) {
  if (sessions.length === 0) {
    return null
  }

  return (
    <Panel className="execution-history-panel" aria-label="Execution history">
      <SectionHeader eyebrow="Session History" title={`${sessions.length} recent sessions`} headingLevel={4} />
      <div className="execution-history-list">
        {sessions.map((session) => (
          <div className="execution-history-row" key={session.sessionId}>
            <span>{session.milestonePath ?? 'Milestone not recorded'}</span>
            <small>
              <StatusBadge status={repositoryExecutionStatus[session.repositoryState]} />
            </small>
            <small>Started {formatDateTime(session.startedAt)}</small>
            <small>Duration {formatDuration(session.duration)}</small>
            <small>Commit {session.commitSha ?? 'Not recorded'}</small>
            <small>Push {session.pushedAt ? formatDateTime(session.pushedAt) : 'Not recorded'}</small>
          </div>
        ))}
      </div>
    </Panel>
  )
}
