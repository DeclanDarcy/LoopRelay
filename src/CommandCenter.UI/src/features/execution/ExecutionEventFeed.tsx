import { memo } from 'react'
import { formatDateTime } from '../../lib'
import { EmptyState, Panel, SectionHeader, StatusBadge, Tooltip } from '../../components/design'
import { executionSessionStatus } from '../../lib/status'
import type { ExecutionEvent } from '../../types'
import type { ExecutionSessionSummary } from '../../types'

type ExecutionEventFeedProps = {
  events: ExecutionEvent[]
  session?: ExecutionSessionSummary | null
  ariaLabel?: string
  eyebrow?: string
}

function ExecutionEventFeedImpl({
  events,
  session = null,
  ariaLabel = 'Execution output',
  eyebrow = 'Execution Output',
}: ExecutionEventFeedProps) {
  const groupedEvents = groupExecutionEvents(events)

  return (
    <Panel className="execution-output-panel" aria-label={ariaLabel}>
      <SectionHeader eyebrow={eyebrow} title={`${events.length} events`} headingLevel={4} />
      <div className="execution-event-feed">
        {events.length === 0 ? (
          <EmptyState className="empty-state">No execution events recorded.</EmptyState>
        ) : (
          groupedEvents.map((group) => (
            <section
              className="execution-event-group"
              aria-label={`${group.category} execution events`}
              key={group.category}
            >
              <div className="execution-event-group-header">
                <span>{group.category}</span>
                <span>{group.events.length} event{group.events.length === 1 ? '' : 's'}</span>
              </div>
              {group.events.map((executionEvent) => (
                <div
                  className="execution-event-row"
                  data-event-sequence={executionEvent.sequence}
                  data-event-type={executionEvent.type}
                  data-event-category={eventCategory(executionEvent)}
                  data-event-timestamp={executionEvent.timestamp}
                  key={executionEvent.sequence}
                >
                  {/* The message is the only always-visible content; every other detail (sequence,
                      time, type, provider, status, session, consequence) lives in the tooltip so a
                      long stream stays scannable. */}
                  <Tooltip
                    className="execution-event-tooltip"
                    triggerLabel={`Event #${executionEvent.sequence} details`}
                  >
                    <dl className="execution-event-details">
                      <div className="execution-event-detail">
                        <dt>Sequence</dt>
                        <dd className="execution-event-sequence">#{executionEvent.sequence}</dd>
                      </div>
                      <div className="execution-event-detail">
                        <dt>Time</dt>
                        <dd className="execution-event-time">{formatDateTime(executionEvent.timestamp)}</dd>
                      </div>
                      <div className="execution-event-detail">
                        <dt>Type</dt>
                        <dd className="execution-event-type">{executionEvent.type}</dd>
                      </div>
                      <div className="execution-event-detail">
                        <dt>Provider</dt>
                        <dd className="execution-event-provider">
                          {session?.providerName || 'Provider not recorded'}
                        </dd>
                      </div>
                      <div className="execution-event-detail">
                        <dt>Status</dt>
                        <dd className="execution-event-status">
                          {session ? (
                            <StatusBadge status={executionSessionStatus[session.state]} />
                          ) : (
                            'Status not recorded'
                          )}
                        </dd>
                      </div>
                      <div className="execution-event-detail">
                        <dt>Session</dt>
                        <dd className="execution-event-session">
                          {session?.sessionId ?? 'Session not recorded'}
                        </dd>
                      </div>
                      <div className="execution-event-detail">
                        <dt>Consequence</dt>
                        <dd className="execution-event-consequence">{eventConsequence(executionEvent)}</dd>
                      </div>
                    </dl>
                  </Tooltip>
                  <pre className="execution-event-message">{executionEvent.message}</pre>
                </div>
              ))}
            </section>
          ))
        )}
      </div>
    </Panel>
  )
}

type ExecutionEventGroup = {
  category: string
  events: ExecutionEvent[]
}

function groupExecutionEvents(events: ExecutionEvent[]): ExecutionEventGroup[] {
  return events.reduce<ExecutionEventGroup[]>((groups, executionEvent) => {
    const category = eventCategory(executionEvent)
    const existing = groups.find((group) => group.category === category)
    if (existing) {
      existing.events.push(executionEvent)
      return groups
    }

    groups.push({ category, events: [executionEvent] })
    return groups
  }, [])
}

function eventCategory(executionEvent: ExecutionEvent) {
  return executionEvent.category?.trim() || 'Monitoring'
}

function eventConsequence(executionEvent: ExecutionEvent) {
  return executionEvent.consequence?.trim() || 'Execution monitoring recorded activity.'
}

export const ExecutionEventFeed = memo(ExecutionEventFeedImpl)
