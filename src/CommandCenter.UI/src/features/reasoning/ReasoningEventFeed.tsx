import { EmptyState, Tooltip } from '../../components/design'
import type { ReasoningEvent, ReasoningEventFamily } from '../../types'
import { ReasoningDiagnosticGroups } from './ReasoningDiagnosticGroups'

type ReasoningEventFeedProps = {
  events: ReasoningEvent[]
  selectedThreadId: string | null
  selectedFamilies: ReasoningEventFamily[]
  isLoading: boolean
}

export function ReasoningEventFeed({
  events,
  selectedThreadId,
  selectedFamilies,
  isLoading,
}: ReasoningEventFeedProps) {
  const selectedFamilySet = new Set(selectedFamilies)
  const visibleEvents = selectedThreadId
    ? events.filter((event) => event.threadIds.includes(selectedThreadId))
    : events
  const filteredEvents = selectedFamilySet.size > 0
    ? visibleEvents.filter((event) => selectedFamilySet.has(event.family))
    : visibleEvents

  return (
    <section className="reasoning-panel reasoning-event-feed-panel" id="reasoning-event-feed" aria-label="Reasoning event feed">
      <div className="decision-panel-heading">
        <h5>Event Feed</h5>
        <span>{filteredEvents.length} events</span>
      </div>

      {filteredEvents.length > 0 ? (
        <div className="reasoning-event-feed">
          {filteredEvents.map((event) => (
            <article className="reasoning-event-row" key={event.id}>
              {/* Only the title + summary stay visible; classification, provenance, and the nested
                  capture diagnostics collapse into the tooltip so the feed stays scannable. */}
              <Tooltip className="reasoning-event-tooltip" triggerLabel={`${event.id} details`}>
                <div className="reasoning-event-detail">
                  <div className="reasoning-badge-row" aria-label={`${event.id} classification`}>
                    <span>{event.family}</span>
                    <span>{event.type}</span>
                    <span>{formatDate(event.createdAt)}</span>
                    <span>{event.captureProvenance.mode}</span>
                  </div>
                  {event.narrative.details ? <small>{event.narrative.details}</small> : null}
                  <dl className="reasoning-provenance" aria-label={`${event.id} capture provenance`}>
                    <div>
                      <dt>Event</dt>
                      <dd>{event.id}</dd>
                    </div>
                    <div>
                      <dt>Capture</dt>
                      <dd>{event.captureProvenance.sourceKind} by {event.captureProvenance.capturedBy}</dd>
                    </div>
                    <div>
                      <dt>Reason</dt>
                      <dd>{event.captureProvenance.captureReason}</dd>
                    </div>
                    {event.captureProvenance.sourceTransition ? (
                      <div>
                        <dt>Transition</dt>
                        <dd>{event.captureProvenance.sourceTransition}</dd>
                      </div>
                    ) : null}
                    {event.captureProvenance.sourceArtifact ? (
                      <div>
                        <dt>Source</dt>
                        <dd>{event.captureProvenance.sourceArtifact}</dd>
                      </div>
                    ) : null}
                    {event.captureProvenance.sourceTimestamp ? (
                      <div>
                        <dt>Source Time</dt>
                        <dd>{formatDate(event.captureProvenance.sourceTimestamp)}</dd>
                      </div>
                    ) : null}
                    {event.captureProvenance.duplicateSignal ? (
                      <div>
                        <dt>Duplicate Signal</dt>
                        <dd>{event.captureProvenance.duplicateSignal}</dd>
                      </div>
                    ) : null}
                    {event.captureProvenance.skipReason ? (
                      <div>
                        <dt>Skipped</dt>
                        <dd>{event.captureProvenance.skipReason}</dd>
                      </div>
                    ) : null}
                    {event.captureProvenance.existingEventReference ? (
                      <div>
                        <dt>Existing Event</dt>
                        <dd>{event.captureProvenance.existingEventReference.id}</dd>
                      </div>
                    ) : null}
                  </dl>
                  <ReasoningDiagnosticGroups
                    groups={event.captureProvenance.diagnosticGroups}
                    label={`${event.id} capture diagnostics`}
                  />
                </div>
              </Tooltip>
              <div className="reasoning-event-primary">
                <strong>{event.title}</strong>
                <p>{event.narrative.summary}</p>
              </div>
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
