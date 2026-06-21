import { formatDateTime } from '../../lib'
import type { ExecutionEvent } from '../../types'

type ExecutionEventFeedProps = {
  events: ExecutionEvent[]
}

export function ExecutionEventFeed({ events }: ExecutionEventFeedProps) {
  return (
    <section className="execution-output-panel" aria-label="Execution output">
      <div>
        <p className="eyebrow">Execution Output</p>
        <h4>{events.length} events</h4>
      </div>
      <div className="execution-event-feed">
        {events.length === 0 ? (
          <p className="empty-state">No execution events recorded.</p>
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
    </section>
  )
}
