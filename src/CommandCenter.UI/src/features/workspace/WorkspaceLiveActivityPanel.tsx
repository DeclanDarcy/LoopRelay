import { ExecutionEventFeed } from '../execution/ExecutionEventFeed'
import type { ExecutionEvent } from '../../types'

type WorkspaceLiveActivityPanelProps = {
  events: ExecutionEvent[]
  onOpenExecutionActivity?: () => void
}

export function WorkspaceLiveActivityPanel({
  events,
  onOpenExecutionActivity,
}: WorkspaceLiveActivityPanelProps) {
  return (
    <div className="workspace-cross-link-panel">
      <ExecutionEventFeed
        ariaLabel="Workspace live activity"
        eyebrow="Live Activity"
        events={events}
      />
      {onOpenExecutionActivity ? (
        <button
          type="button"
          className="workspace-cross-link secondary-action"
          onClick={onOpenExecutionActivity}
        >
          Open in Execution
        </button>
      ) : null}
    </div>
  )
}
