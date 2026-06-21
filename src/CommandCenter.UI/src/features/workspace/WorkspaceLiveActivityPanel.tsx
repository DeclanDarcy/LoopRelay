import { ExecutionEventFeed } from '../execution/ExecutionEventFeed'
import type { ExecutionEvent } from '../../types'

type WorkspaceLiveActivityPanelProps = {
  events: ExecutionEvent[]
}

export function WorkspaceLiveActivityPanel({ events }: WorkspaceLiveActivityPanelProps) {
  return (
    <ExecutionEventFeed
      ariaLabel="Workspace live activity"
      eyebrow="Live Activity"
      events={events}
    />
  )
}
