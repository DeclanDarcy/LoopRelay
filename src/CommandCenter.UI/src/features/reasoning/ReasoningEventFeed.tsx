import { EmptyState } from '../../components/design'
import type { ReasoningEvent } from '../../types'

type ReasoningEventFeedProps = {
  events: ReasoningEvent[]
  selectedThreadId: string | null
  isLoading: boolean
}

export function ReasoningEventFeed({
  events,
  selectedThreadId,
  isLoading,
}: ReasoningEventFeedProps) {
  const visibleEvents = selectedThreadId
    ? events.filter((event) => event.threadIds.includes(selectedThreadId))
    : events

  return (
    <section className="reasoning-panel reasoning-event-feed-panel" id="reasoning-event-feed" aria-label="Reasoning event feed">
      <div className="decision-panel-heading">
        <h5>Event Feed</h5>
        <span>{visibleEvents.length} events</span>
      </div>

      {visibleEvents.length > 0 ? (
        <div className="reasoning-event-feed">
          {visibleEvents.map((event) => (
            <article className="reasoning-event-row" key={event.id}>
              <div className="reasoning-event-heading">
                <strong>{event.title}</strong>
                <span>{event.id}</span>
              </div>
              <div className="reasoning-badge-row" aria-label={`${event.id} classification`}>
                <span>{event.family}</span>
                <span>{event.type}</span>
                <span>{formatDate(event.createdAt)}</span>
              </div>
              <p>{event.narrative.summary}</p>
              {event.narrative.details ? <small>{event.narrative.details}</small> : null}
              <dl className="reasoning-provenance">
                <div>
                  <dt>Provenance</dt>
                  <dd>{event.provenance.sourceKind} by {event.provenance.capturedBy}</dd>
                </div>
                {event.provenance.relativePath ? (
                  <div>
                    <dt>Source</dt>
                    <dd>{event.provenance.relativePath}</dd>
                  </div>
                ) : null}
              </dl>
            </article>
          ))}
        </div>
      ) : (
        <EmptyState className="empty-state">
          {isLoading ? 'Loading reasoning events...' : 'No reasoning events recorded.'}
        </EmptyState>
      )}
    </section>
  )
}

function formatDate(value: string) {
  if (!value) {
    return 'No timestamp'
  }

  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString()
}
