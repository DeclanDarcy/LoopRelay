import { formatDateTime } from '../../lib'
import { EmptyState, Panel, SectionHeader } from '../../components/design'
import type { ExecutionEvent } from '../../types'

type ExecutionEventFeedProps = {
  events: ExecutionEvent[]
  ariaLabel?: string
  eyebrow?: string
}

export function ExecutionEventFeed({
  events,
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
              <pre>{executionEvent.message}</pre>
            </div>
          ))
        )}
      </div>
    </Panel>
  )
}
