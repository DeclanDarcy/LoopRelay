import { formatDateTime } from '../../lib'
import { EmptyState, Panel, SectionHeader } from '../../components/design'
import type { ExecutionEvent } from '../../types'

type ExecutionEventFeedProps = {
  events: ExecutionEvent[]
}

export function ExecutionEventFeed({ events }: ExecutionEventFeedProps) {
  return (
    <Panel className="execution-output-panel" aria-label="Execution output">
      <SectionHeader eyebrow="Execution Output" title={`${events.length} events`} headingLevel={4} />
      <div className="execution-event-feed">
        {events.length === 0 ? (
          <EmptyState className="empty-state">No execution events recorded.</EmptyState>
        ) : (
          events.map((executionEvent) => (
            <div className="execution-event-row" key={executionEvent.sequence}>
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
