import { formatDateTime } from '../../lib'
import { EmptyState, Panel, SectionHeader, StatusBadge } from '../../components/design'
import { executionSessionStatus } from '../../lib/status'
import type { ExecutionEvent } from '../../types'
import type { ExecutionSessionSummary } from '../../types'

type ExecutionEventFeedProps = {
  events: ExecutionEvent[]
  session?: ExecutionSessionSummary | null
  ariaLabel?: string
  eyebrow?: string
}

export function ExecutionEventFeed({
  events,
  session = null,
  ariaLabel = 'Execution output',
  eyebrow = 'Execution Output',
}: ExecutionEventFeedProps) {
  return (
    <Panel className="execution-output-panel" aria-label={ariaLabel}>
      <SectionHeader eyebrow={eyebrow} title={`${events.length} events`} headingLevel={4} />
      <div className="execution-event-feed">
        {events.length === 0 ? (
          <EmptyState className="empty-state">No execution events recorded.</EmptyState>
        ) : (
          events.map((executionEvent) => (
            <div
              className="execution-event-row"
              data-event-sequence={executionEvent.sequence}
              data-event-type={executionEvent.type}
              data-event-timestamp={executionEvent.timestamp}
              key={executionEvent.sequence}
            >
              <span className="execution-event-sequence">#{executionEvent.sequence}</span>
              <span className="execution-event-time">{formatDateTime(executionEvent.timestamp)}</span>
              <span className="execution-event-type">{executionEvent.type}</span>
              <span className="execution-event-provider">
                {session?.providerName || 'Provider not recorded'}
              </span>
              <span className="execution-event-status">
                {session ? <StatusBadge status={executionSessionStatus[session.state]} /> : 'Status not recorded'}
              </span>
              <span className="execution-event-session">
                {session?.sessionId ?? 'Session not recorded'}
              </span>
              <pre>{executionEvent.message}</pre>
            </div>
          ))
        )}
      </div>
    </Panel>
  )
}
