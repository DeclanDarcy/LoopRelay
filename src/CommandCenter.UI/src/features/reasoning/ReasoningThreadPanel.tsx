import { EmptyState } from '../../components/design'
import type { ReasoningEvent, ReasoningThread } from '../../types'

type ReasoningThreadPanelProps = {
  threads: ReasoningThread[]
  events: ReasoningEvent[]
  selectedThreadId: string | null
  isLoading: boolean
  onSelectThread: (threadId: string | null) => void
}

export function ReasoningThreadPanel({
  threads,
  events,
  selectedThreadId,
  isLoading,
  onSelectThread,
}: ReasoningThreadPanelProps) {
  const eventFamilyCounts = new Map(events.map((event) => [event.id, event.family]))

  return (
    <section className="reasoning-panel reasoning-thread-panel" id="reasoning-thread-view" aria-label="Reasoning threads">
      <div className="decision-panel-heading">
        <h5>Threads</h5>
        <button type="button" className="decision-filter" onClick={() => onSelectThread(null)}>
          All events
        </button>
      </div>

      {threads.length > 0 ? (
        <div className="decision-row-list">
          {threads.map((thread) => {
            const selected = thread.id === selectedThreadId
            const families = Array.from(
              new Set(thread.eventIds.map((eventId) => eventFamilyCounts.get(eventId)).filter(Boolean)),
            )

            return (
              <button
                type="button"
                className={['decision-row decision-row-button', selected ? 'selected' : ''].filter(Boolean).join(' ')}
                key={thread.id}
                onClick={() => onSelectThread(thread.id)}
              >
                <strong>{thread.title}</strong>
                <span>{thread.id} / {thread.theme}</span>
                <p>{thread.summary}</p>
                <span>{thread.eventIds.length} events{families.length ? ` / ${families.join(', ')}` : ''}</span>
              </button>
            )
          })}
        </div>
      ) : (
        <EmptyState className="empty-state">
          {isLoading ? 'Loading reasoning threads...' : 'No reasoning threads recorded.'}
        </EmptyState>
      )}
    </section>
  )
}
