import { memo } from 'react'
import { Button, EmptyState, Panel, SectionHeader } from '../../components/design'
import { formatDateTime } from '../../lib'
import type { ExecutionEvent } from '../../types'

type WorkspaceLiveActivityPanelProps = {
  events: ExecutionEvent[]
  onOpenExecutionActivity?: () => void
}

function WorkspaceLiveActivityPanelImpl({
  events,
  onOpenExecutionActivity,
}: WorkspaceLiveActivityPanelProps) {
  const latestEvent = events.at(-1) ?? null
  const categories = Array.from(
    new Set(events.map((event) => event.category?.trim() || 'Monitoring')),
  )

  return (
    <Panel className="workspace-cross-link-panel workspace-live-activity-summary" aria-label="Workspace live activity">
      <SectionHeader
        eyebrow="Live Activity"
        title={`${events.length} events`}
        headingLevel={4}
        actions={
          onOpenExecutionActivity ? (
            <Button
              type="button"
              variant="secondary"
              className="secondary-action"
              onClick={onOpenExecutionActivity}
            >
              Open in Execution
            </Button>
          ) : null
        }
      />
      {latestEvent ? (
        <div className="workspace-inspector-summary">
          <span>Latest: {latestEvent.type}</span>
          <span>Recorded: {formatDateTime(latestEvent.timestamp)}</span>
          <span>Categories: {categories.join(', ')}</span>
          <span>Consequence: {latestEvent.consequence?.trim() || 'Execution monitoring recorded activity.'}</span>
        </div>
      ) : null}
      {events.length === 0 ? (
        <EmptyState className="empty-state">No execution events recorded.</EmptyState>
      ) : null}
    </Panel>
  )
}

export const WorkspaceLiveActivityPanel = memo(WorkspaceLiveActivityPanelImpl)
