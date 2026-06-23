import { EmptyState } from '../../components/design'
import type { ReasoningEvent, ReasoningRelationship, ReasoningThread } from '../../types'

type ReasoningTracePanelProps = {
  events: ReasoningEvent[]
  relationships: ReasoningRelationship[]
  selectedThread: ReasoningThread | null
  isLoading: boolean
}

export function ReasoningTracePanel({
  events,
  relationships,
  selectedThread,
  isLoading,
}: ReasoningTracePanelProps) {
  const eventIds = new Set(selectedThread?.eventIds ?? events.map((event) => event.id))
  const visibleRelationships = relationships.filter(
    (relationship) =>
      isReasoningEventReferenceInSet(relationship.source, eventIds) ||
      isReasoningEventReferenceInSet(relationship.target, eventIds) ||
      (selectedThread && isReasoningThreadReference(relationship.source, selectedThread.id)) ||
      (selectedThread && isReasoningThreadReference(relationship.target, selectedThread.id)),
  )
  const derivedFamilies = Array.from(
    new Set(events.filter((event) => eventIds.has(event.id)).map((event) => event.family)),
  )

  return (
    <section className="reasoning-panel reasoning-trace-panel" id="reasoning-trace" aria-label="Reasoning trace">
      <div className="decision-panel-heading">
        <h5>Trace</h5>
        <span>{visibleRelationships.length} relationships</span>
      </div>

      <div className="reasoning-derived-status" aria-label="Derived reasoning status">
        <span>Derived display only</span>
        <strong>{derivedFamilies.length ? derivedFamilies.join(', ') : 'No families'}</strong>
      </div>

      {visibleRelationships.length > 0 ? (
        <div className="decision-row-list">
          {visibleRelationships.map((relationship) => (
            <article className="decision-row reasoning-relationship-row" key={relationship.id}>
              <strong>{relationship.type}</strong>
              <span>
                {relationship.source.kind} {relationship.source.id} {'->'} {relationship.target.kind} {relationship.target.id}
              </span>
              <p>{relationship.narrative.summary}</p>
              <small>{relationship.provenance.sourceKind} by {relationship.provenance.capturedBy}</small>
            </article>
          ))}
        </div>
      ) : (
        <EmptyState className="empty-state">
          {isLoading ? 'Loading reasoning relationships...' : 'No reasoning relationships recorded.'}
        </EmptyState>
      )}
    </section>
  )
}

function isReasoningEventReferenceInSet(
  reference: ReasoningRelationship['source'],
  eventIds: Set<string>,
) {
  return reference.kind === 'ReasoningEvent' && eventIds.has(reference.id)
}

function isReasoningThreadReference(reference: ReasoningRelationship['source'], threadId: string) {
  return reference.kind === 'ReasoningThread' && reference.id === threadId
}
